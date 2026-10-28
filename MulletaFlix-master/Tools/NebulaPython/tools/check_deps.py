"""Runtime dependency guard for NebulaFTP.

Salvamos o fast-fail do arranque (`ImportError: pyrogram`, `bcrypt`, etc.)
garantindo que as bibliotecas listadas em `requirements.lock` estejam
presentes no interpretador antes de qualquer parte do servidor ser
tocada.

Estratégia:

1. Para cada módulo do `RUNTIME_REQUIRED`, tenta ``importlib.util.find_spec``
   e ``importlib.import_module`` (este último em subprocess explícito
   quando há risco conhecido de side-effect top-level.
2. Com lock, compara todos os pins ativos com as versões instaladas. Imports
   quebrados, dependências ausentes ou drift permitem reparo só dentro de venv.
3. Fora de venv, falha sem chamar pip e orienta executar o instalador.
4. Sem lock, compatibilidade antiga também instala apenas dentro de venv.
5. Revalida imports e versões. Se ainda houver divergência, aborta com mensagem clara (em vez do
   ``Traceback`` enigmático que aparecia antes).

Esta função é chamada no topo de ``main.main()`` **antes** de qualquer
import runtime ser executado. Importá-la não dispara side-effect algum
em libs de runtime — é Python stdlib + ``subprocess`` puro.
"""
from __future__ import annotations

import importlib
import importlib.metadata as metadata
import importlib.util
import json
import os
import re
import subprocess
import sys
from collections.abc import Iterable

# Cada item: nome do módulo teste, nome do pacote pip (geralmente o mesmo).
# Comentários marcam libs que disparam side-effects top-level sensíveis a
# Python 3.14 (sem event loop no import-time).
RUNTIME_REQUIRED: tuple[tuple[str, str], ...] = (
    ("bcrypt", "bcrypt"),
    ("cryptography", "cryptography"),
    ("dns.resolver", "dnspython"),
    ("dotenv", "python-dotenv"),
    ("aiofiles", "aiofiles"),
    ("pymongo", "pymongo"),
    # ``motor`` exige pymongo, importa top-level sem side-effects.
    ("motor", "motor"),
    # ``aiohttp`` top-level é seguro.
    ("aiohttp", "aiohttp"),
    # Pyrofork keeps the public ``pyrogram`` namespace.
    ("pyrogram", "pyrofork"),
    ("tgcrypto", "tgcrypto-pyrofork"),
    ("pyaes", "pyaes"),
    # PySocks distribui como ``pysocks`` mas expõe ``import socks``;
    # alinhamos o módulo real aqui, e mantemos o nome pip em lowercase
    # para casar com `_read_pinned_specs`.
    ("socks", "pysocks"),
    ("pyftpdlib", "pyftpdlib"),
    ("PIL", "Pillow"),
)


class _LockParserUnavailableError(RuntimeError):
    """The runtime cannot parse lock entries before its repair attempt."""


def _read_pinned_specs(path: str = "requirements.txt") -> dict[str, str]:
    """Extrai specs de requirements e segue includes relativos sem ciclos.

    Comentários (``#``) e linhas em branco são ignorados. Aceita múltiplas
    flags (e.g. ``foo>=1,<2,!=1.4``) e devolve a string completa para o
    pip como spec.
    """
    return _read_pinned_specs_recursive(path, set())


def _read_pinned_specs_recursive(path: str, visited: set[str]) -> dict[str, str]:
    absolute_path = os.path.abspath(path)
    if absolute_path in visited or not os.path.exists(absolute_path):
        return {}
    visited.add(absolute_path)

    out: dict[str, str] = {}
    with open(absolute_path, encoding="utf-8") as requirements_file:
        for raw in requirements_file:
            line = raw.strip()
            if line.startswith(("-r ", "--requirement ")):
                include_path = line.split(maxsplit=1)[1].strip().strip("\"'")
                if not os.path.isabs(include_path):
                    include_path = os.path.join(os.path.dirname(absolute_path), include_path)
                out.update(_read_pinned_specs_recursive(include_path, visited))
                continue
            if not line or line.startswith("#") or line.startswith("--"):
                continue
            match = re.match(r"([A-Za-z0-9_.\-]+)\s*(.*)", line)
            if not match:
                continue
            name, spec = match.group(1), match.group(2).strip()
            if spec:
                out[name.lower()] = spec
    return out


def _probe(module_name: str) -> tuple[bool, str | None]:
    """Deteta se ``module_name`` está importável no interpretador atual.

    Retorna ``(ok, error_message)``. Não invoca ``import_module`` em
    pacotes que disparam side-effects pesados no carregamento: usa
    ``find_spec`` (sem executar) como deteção barata.
    """
    try:
        if importlib.util.find_spec(module_name) is None:
            return False, "find_spec returned None"
    except (ImportError, ValueError) as exc:
        return False, f"find_spec failed: {exc}"
    # Confirma import real (alguns meta-path finders mentem).
    try:
        importlib.import_module(module_name)
    except Exception as exc:  # noqa: BLE001 - queremos qualquer falha
        return False, f"{type(exc).__name__}: {exc}"
    return True, None


def _pip_install(spec: str) -> int:
    """Despacha ``pip install`` num subprocess isolado.

    O subprocess garante que o nosso interpretador não fica bloqueado
    por outros ``pip`` pendentes e que o path de import é o mesmo do
    ``sys.executable``.
    """
    cmd = [
        sys.executable,
        "-m",
        "pip",
        "install",
        "--quiet",
        "--disable-pip-version-check",
        *spec.split(),
    ]
    # ``spec`` é sempre construído a partir da nossa tabela interna
    # ``RUNTIME_REQUIRED``, não de input externo. O subprocess é seguro.
    proc = subprocess.run(  # noqa: S603 - input vindo de RUNTIME_REQUIRED
        cmd, capture_output=True, text=True, check=False
    )
    if proc.returncode != 0:
        sys.stderr.write(
            f"[deps] pip install falhou para {spec!r}\n"
            f"[deps]   stdout: {proc.stdout.strip()}\n"
            f"[deps]   stderr: {proc.stderr.strip()}\n"
        )
    return proc.returncode


def _pip_install_locked(requirements_path: str) -> int:
    """Installs the complete runtime graph from a hash-checked lock file."""
    cmd = [
        sys.executable,
        "-m",
        "pip",
        "install",
        "--quiet",
        "--disable-pip-version-check",
        "--require-hashes",
        "-r",
        requirements_path,
    ]
    proc = subprocess.run(  # noqa: S603 - executable and args are fixed
        cmd, capture_output=True, text=True, check=False
    )
    if proc.returncode != 0:
        sys.stderr.write(
            f"[deps] instalação pelo lock falhou: {requirements_path}\n"
            f"[deps]   stdout: {proc.stdout.strip()}\n"
            f"[deps]   stderr: {proc.stderr.strip()}\n"
        )
    return proc.returncode


def _read_locked_versions(requirements_path: str) -> dict[str, str]:
    """Reads exact pins active for this interpreter from a pip hash lock."""
    try:
        from packaging.requirements import Requirement
        from packaging.utils import canonicalize_name
    except ImportError:
        try:
            from pip._vendor.packaging.requirements import Requirement
            from pip._vendor.packaging.utils import canonicalize_name
        except ImportError as exc:  # old runtimes may only expose pip's vendored parser.
            raise _LockParserUnavailableError(
                "Parser PEP 508 indisponível para validar requirements.lock"
            ) from exc

    versions: dict[str, str] = {}
    with open(requirements_path, encoding="utf-8") as lock_file:
        for line_number, raw in enumerate(lock_file, start=1):
            line = raw.strip()
            if not line or line.startswith(("#", "--hash=")):
                continue
            if line.endswith("\\"):
                line = line[:-1].rstrip()
            line = re.split(r"\s+--hash=", line, maxsplit=1)[0].rstrip()
            try:
                requirement = Requirement(line)
            except Exception as exc:  # noqa: BLE001 - malformed lock must fail closed
                raise ValueError(f"Linha {line_number} inválida em {requirements_path}: {line!r}") from exc

            if requirement.marker is not None and not requirement.marker.evaluate():
                continue

            exact_pins = [item.version for item in requirement.specifier if item.operator == "=="]
            if len(exact_pins) != 1 or any(item.operator != "==" for item in requirement.specifier):
                raise ValueError(f"Linha {line_number} sem pin exato em {requirements_path}: {line!r}")
            version = exact_pins[0]
            if version.endswith(".*"):
                raise ValueError(f"Linha {line_number} usa pin wildcard em {requirements_path}: {line!r}")

            name = canonicalize_name(requirement.name)
            previous = versions.get(name)
            if previous is not None and previous != version:
                raise ValueError(
                    f"Pins ativos conflitantes para {name} em {requirements_path}: {previous} e {version}"
                )
            versions[name] = version

    if not versions:
        raise ValueError(f"Nenhum pin ativo foi encontrado em {requirements_path}")
    return versions


def _locked_version_drift(requirements_path: str) -> list[tuple[str, str, str | None]]:
    """Returns locked packages that are missing or differ from their exact pin."""
    drift: list[tuple[str, str, str | None]] = []
    for package, expected in sorted(_read_locked_versions(requirements_path).items()):
        try:
            installed = metadata.version(package)
        except metadata.PackageNotFoundError:
            installed = None
        if installed != expected:
            drift.append((package, expected, installed))
    return drift


def _is_isolated_environment() -> bool:
    """Return whether this process belongs to a Python virtual environment."""
    return sys.prefix != sys.base_prefix


def _require_isolated_environment() -> None:
    """Prevent startup repair from modifying a shared/global Python install."""
    if not _is_isolated_environment():
        raise RuntimeError(
            "O runtime Nebula não está isolado. Nenhuma dependência foi instalada no Python global; "
            "execute novamente o instalador do MulletaFlix Server para preparar a venv do Nebula."
        )


def ensure_runtime_dependencies(
    required: Iterable[tuple[str, str]] = RUNTIME_REQUIRED,
    requirements_path: str = "requirements.txt",
) -> None:
    """Garante imports e pins sem modificar Python fora de venv.

    Args:
        required: tupla de pares ``(modulo, pacote_pip)``.
        requirements_path: caminho para o ``requirements.txt``; um lock ao
            lado, quando existe, é a fonte exata das versões esperadas.

    Levanta ``RuntimeError`` se o lock for inválido, a recuperação falhar ou
    uma divergência persistir. Em caso de interrupção do utilizador
    (``KeyboardInterrupt``), propaga.
    """
    required = tuple(required)
    pins = _read_pinned_specs(requirements_path)
    lock_path = os.path.join(os.path.dirname(os.path.abspath(requirements_path)), "requirements.lock")
    locked_drift: list[tuple[str, str, str | None]] = []
    lock_parser_unavailable = False
    if os.path.isfile(lock_path):
        try:
            locked_drift = _locked_version_drift(lock_path)
        except _LockParserUnavailableError:
            # Repair the locked runtime before importing any potentially stale modules.
            lock_parser_unavailable = True
        except (OSError, RuntimeError, ValueError) as exc:
            raise RuntimeError(f"Não foi possível validar o lock de dependências: {exc}") from exc

    missing: list[tuple[str, str, str | None]] = []  # (module, pip_name, reason)

    # If versions have drifted, don't import old modules before pip replaces
    # their files. The process must load the locked code after repair.
    if not locked_drift and not lock_parser_unavailable:
        for module_name, pip_name in required:
            ok, reason = _probe(module_name)
            if not ok:
                missing.append((module_name, pip_name, reason))

    if not missing and not locked_drift and not lock_parser_unavailable:
        return

    _require_isolated_environment()

    sys.stderr.write(
        f"[deps] {len(missing)} dependencia(s) ausente(s)/quebrada(s), "
        f"{len(locked_drift)} versão(ões) fora do lock"
        f"{' e parser indisponível' if lock_parser_unavailable else ''}; a reparar...\n"
    )

    if os.path.isfile(lock_path):
        rc = _pip_install_locked(lock_path)
        if rc != 0:
            raise RuntimeError(
                "Falha ao instalar dependências a partir de requirements.lock. "
                "Execute novamente o instalador do MulletaFlix Server."
            )

        still_missing = [(module, package) for module, package in required if not _probe(module)[0]]
        try:
            still_drift = _locked_version_drift(lock_path)
        except (OSError, RuntimeError, ValueError) as exc:
            raise RuntimeError(f"Não foi possível revalidar requirements.lock após instalação: {exc}") from exc
        if still_missing or still_drift:
            missing_text = ", ".join(
                f"{package} (módulo {module})" for module, package in still_missing
            )
            drift_text = ", ".join(
                f"{package} esperado={expected} instalado={installed or 'ausente'}"
                for package, expected, installed in still_drift
            )
            details = "; ".join(part for part in (missing_text, drift_text) if part)
            raise RuntimeError(f"Dependências continuam divergentes após instalar o lock: {details}.")
        sys.stderr.write("[deps] dependências instaladas e versões confirmadas pelo lock.\n")
        return

    # Instala em batch (uma chamada pip) quando todos os specs existem
    # nos pins. Caso contrário, instala individualmente.
    batchspecs: list[str] = []
    for _module_name, pip_name, _reason in missing:
        spec = pins.get(pip_name.lower())
        candidate = f"{pip_name}{spec}" if spec else pip_name
        batchspecs.append(candidate)

    rc = _pip_install(" ".join(batchspecs))
    if rc != 0:
        # fallback: instala um-a-um para isolar qual pacote falhou
        for candidate in batchspecs:
            _pip_install(candidate)

    # Re-probe. Quem continuou a falhar após install, é fatal.
    still_missing = [(m, p) for (m, p, _r) in missing if not _probe(m)[0]]
    if still_missing:
        joined = ", ".join(f"{p} (módulo {m})" for m, p in still_missing)
        raise RuntimeError(
            f"Falha ao instalar dependências necessárias: {joined}. "
            "Execute `pip install -r requirements.txt` manualmente."
        )
    sys.stderr.write("[deps] todas as dependências verificadas.\n")


def main() -> int:  # pragma: no cover - ponto de entrada CLI
    """Permite correr manualmente: ``python -m tools.check_deps`` ou
    ``python tools/check_deps.py``."""
    try:
        ensure_runtime_dependencies()
    except RuntimeError as exc:
        sys.stderr.write(f"[deps] {exc}\n")
        return 1
    print(json.dumps({"ok": True, "modules": [m for m, _ in RUNTIME_REQUIRED]}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
