from __future__ import annotations

import builtins
import importlib
from types import SimpleNamespace

import pytest

check_deps = importlib.import_module("tools.check_deps")


@pytest.fixture(autouse=True)
def isolated_runtime_for_dependency_repair_tests(monkeypatch):
    monkeypatch.setattr(check_deps, "_is_isolated_environment", lambda: True)


def test_read_pinned_specs_ignores_comments_and_normalizes_package_names(tmp_path):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text(
        "# comment\n\n--index-url https://example.invalid/simple\n!! malformed\n"
        "PySocks >=1.7,<2,!=1.8\nplain-package\n",
        encoding="utf-8",
    )

    assert check_deps._read_pinned_specs(str(requirements)) == {
        "pysocks": ">=1.7,<2,!=1.8"
    }


def test_read_pinned_specs_missing_file_returns_empty_mapping(tmp_path):
    assert check_deps._read_pinned_specs(str(tmp_path / "missing.txt")) == {}


def test_read_pinned_specs_follows_relative_requirement_includes_and_cycles(tmp_path):
    root = tmp_path / "requirements.txt"
    base = tmp_path / "base.in"
    extra = tmp_path / "extra.in"
    root.write_text("-r base.in\n", encoding="utf-8")
    base.write_text("package-a>=1,<2\n--requirement extra.in\n", encoding="utf-8")
    extra.write_text("package-b==3.4\n-r requirements.txt\n", encoding="utf-8")

    assert check_deps._read_pinned_specs(str(root)) == {
        "package-a": ">=1,<2",
        "package-b": "==3.4",
    }


def test_read_locked_versions_evaluates_markers_and_normalizes_names(tmp_path):
    lock = tmp_path / "requirements.lock"
    lock.write_text(
        "PySocks==2.0.0 ; python_full_version >= '0' \\\n"
        "    --hash=sha256:active\n"
        "PySocks==1.0.0 ; python_full_version < '0' \\\n"
        "    --hash=sha256:inactive\n"
        "package-a==1.2.3 \\\n"
        "    --hash=sha256:package\n",
        encoding="utf-8",
    )

    assert check_deps._read_locked_versions(str(lock)) == {
        "pysocks": "2.0.0",
        "package-a": "1.2.3",
    }


def test_locked_version_drift_reports_outdated_and_missing_packages(monkeypatch, tmp_path):
    lock = tmp_path / "requirements.lock"
    lock.write_text(
        "package-a==2.0.0 \\\n    --hash=sha256:a\n"
        "package-b==3.0.0 \\\n    --hash=sha256:b\n",
        encoding="utf-8",
    )

    def version(name):
        if name == "package-b":
            raise check_deps.metadata.PackageNotFoundError(name)
        return "1.0.0"

    monkeypatch.setattr(check_deps.metadata, "version", version)

    assert check_deps._locked_version_drift(str(lock)) == [
        ("package-a", "2.0.0", "1.0.0"),
        ("package-b", "3.0.0", None),
    ]


def test_read_locked_versions_fails_closed_on_non_exact_pin(tmp_path):
    lock = tmp_path / "requirements.lock"
    lock.write_text("package-a>=1 --hash=sha256:abcd\n", encoding="utf-8")

    with pytest.raises(ValueError, match="sem pin exato"):
        check_deps._read_locked_versions(str(lock))


def test_tgcrypto_runtime_import_uses_pyrofork_binary_wheel_distribution():
    requirements = check_deps._read_pinned_specs(
        str(check_deps.os.path.join(check_deps.os.path.dirname(__file__), "..", "requirements.txt"))
    )

    assert ("tgcrypto", "tgcrypto-pyrofork") in check_deps.RUNTIME_REQUIRED
    assert requirements["tgcrypto-pyrofork"] == ">=1.2.8,<1.3"


def test_headless_nebula_does_not_require_unused_system_tray_runtime():
    assert ("pystray", "pystray") not in check_deps.RUNTIME_REQUIRED


def test_cryptography_runtime_requirement_excludes_known_vulnerable_versions():
    requirements = check_deps._read_pinned_specs(
        str(check_deps.os.path.join(check_deps.os.path.dirname(__file__), "..", "requirements.txt"))
    )

    assert requirements["cryptography"] == ">=50.0.2,<51"


@pytest.mark.parametrize(
    ("find_spec_result", "find_spec_error", "import_error", "expected"),
    [
        (None, None, None, (False, "find_spec returned None")),
        (None, ImportError("bad parent"), None, (False, "find_spec failed: bad parent")),
        (None, ValueError("bad spec"), None, (False, "find_spec failed: bad spec")),
        (object(), None, RuntimeError("broken import"), (False, "RuntimeError: broken import")),
        (object(), None, None, (True, None)),
    ],
)
def test_probe_reports_missing_specs_and_import_failures(
    monkeypatch, find_spec_result, find_spec_error, import_error, expected
):
    def fake_find_spec(_module_name):
        if find_spec_error:
            raise find_spec_error
        return find_spec_result

    def fake_import_module(_module_name):
        if import_error:
            raise import_error
        return object()

    monkeypatch.setattr(check_deps.importlib.util, "find_spec", fake_find_spec)
    monkeypatch.setattr(check_deps.importlib, "import_module", fake_import_module)

    assert check_deps._probe("example.module") == expected


@pytest.mark.parametrize(("returncode", "expected_stderr"), [(0, ""), (1, "pip install falhou")])
def test_pip_install_uses_current_interpreter_and_reports_failure(
    monkeypatch, capsys, returncode, expected_stderr
):
    calls = []

    def fake_run(command, **kwargs):
        calls.append((command, kwargs))
        return SimpleNamespace(returncode=returncode, stdout="out detail", stderr="err detail")

    monkeypatch.setattr(check_deps.subprocess, "run", fake_run)

    assert check_deps._pip_install("pkg>=1,<2 other==3") == returncode

    command, kwargs = calls[0]
    assert command == [
        check_deps.sys.executable,
        "-m",
        "pip",
        "install",
        "--quiet",
        "--disable-pip-version-check",
        "pkg>=1,<2",
        "other==3",
    ]
    assert kwargs == {"capture_output": True, "text": True, "check": False}
    assert expected_stderr in capsys.readouterr().err


@pytest.mark.parametrize(("returncode", "expected_stderr"), [(0, ""), (1, "instalação pelo lock falhou")])
def test_pip_install_locked_requires_hashes_and_reports_failure(
    monkeypatch, capsys, returncode, expected_stderr, tmp_path
):
    calls = []
    lock = tmp_path / "requirements.lock"

    def fake_run(command, **kwargs):
        calls.append((command, kwargs))
        return SimpleNamespace(returncode=returncode, stdout="out detail", stderr="err detail")

    monkeypatch.setattr(check_deps.subprocess, "run", fake_run)

    assert check_deps._pip_install_locked(str(lock)) == returncode

    command, kwargs = calls[0]
    assert command == [
        check_deps.sys.executable,
        "-m",
        "pip",
        "install",
        "--quiet",
        "--disable-pip-version-check",
        "--require-hashes",
        "-r",
        str(lock),
    ]
    assert kwargs == {"capture_output": True, "text": True, "check": False}
    assert expected_stderr in capsys.readouterr().err


def test_ensure_runtime_dependencies_does_not_install_when_all_importable_without_lock(
    monkeypatch, tmp_path
):
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (True, None))
    monkeypatch.setattr(
        check_deps,
        "_pip_install",
        lambda _spec: pytest.fail("pip must not run when all dependencies are importable"),
    )

    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")

    check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))


@pytest.mark.parametrize("has_lock", [True, False])
def test_global_python_fails_closed_without_running_pip(monkeypatch, tmp_path, has_lock):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")
    if has_lock:
        (tmp_path / "requirements.lock").write_text(
            "package-a==1.2.3 --hash=sha256:abcd\n", encoding="utf-8"
        )

    monkeypatch.setattr(check_deps, "_is_isolated_environment", lambda: False)
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (False, "missing"))
    monkeypatch.setattr(
        check_deps,
        "_pip_install_locked",
        lambda _path: pytest.fail("global Python must never run pip"),
    )
    monkeypatch.setattr(
        check_deps,
        "_pip_install",
        lambda _spec: pytest.fail("global Python must never run pip"),
    )

    with pytest.raises(RuntimeError, match="Python global.*execute novamente o instalador"):
        check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))


def test_ensure_runtime_dependencies_repairs_importable_version_drift_from_lock(
    monkeypatch, tmp_path
):
    requirements = tmp_path / "requirements.txt"
    lock = tmp_path / "requirements.lock"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")
    lock.write_text("package-a==2.0.0 \\\n    --hash=sha256:abcd\n", encoding="utf-8")
    installed_version = {"value": "1.0.0"}
    installed = {"value": False}
    installs = []

    def probe(_module):
        assert installed["value"], "stale modules must not load before lock repair"
        return True, None

    monkeypatch.setattr(check_deps, "_probe", probe)

    def install(path):
        installs.append(path)
        installed_version["value"] = "2.0.0"
        installed["value"] = True
        return 0

    monkeypatch.setattr(check_deps, "_pip_install_locked", install)
    monkeypatch.setattr(
        check_deps.metadata, "version", lambda _name: installed_version["value"]
    )

    check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))

    assert installs == [str(lock)]
    assert installed_version["value"] == "2.0.0"


def test_ensure_runtime_dependencies_repairs_when_lock_parser_is_unavailable(
    monkeypatch, tmp_path
):
    requirements = tmp_path / "requirements.txt"
    lock = tmp_path / "requirements.lock"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")
    lock.write_text("package-a==2.0.0 --hash=sha256:abcd\n", encoding="utf-8")
    parser_available = {"value": False}
    installs = []
    original_import = builtins.__import__

    def import_without_lock_parser(name, *args, **kwargs):
        if not parser_available["value"] and name in {
            "packaging.requirements",
            "packaging.utils",
            "pip._vendor.packaging.requirements",
            "pip._vendor.packaging.utils",
        }:
            raise ImportError("parser missing")
        return original_import(name, *args, **kwargs)

    def probe(_module):
        assert parser_available["value"], "runtime imports must wait until lock repair"
        return True, None

    def install(path):
        installs.append(path)
        parser_available["value"] = True
        return 0

    monkeypatch.setattr(builtins, "__import__", import_without_lock_parser)
    monkeypatch.setattr(check_deps.metadata, "version", lambda _name: "2.0.0")
    monkeypatch.setattr(check_deps, "_probe", probe)
    monkeypatch.setattr(check_deps, "_pip_install_locked", install)

    check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))

    assert installs == [str(lock)]


def test_ensure_runtime_dependencies_fails_closed_when_version_drift_persists(
    monkeypatch, tmp_path
):
    requirements = tmp_path / "requirements.txt"
    lock = tmp_path / "requirements.lock"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")
    lock.write_text("package-a==2.0.0 --hash=sha256:abcd\n", encoding="utf-8")
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (True, None))
    monkeypatch.setattr(check_deps.metadata, "version", lambda _name: "1.0.0")
    installs = []
    monkeypatch.setattr(
        check_deps, "_pip_install_locked", lambda path: installs.append(path) or 0
    )

    with pytest.raises(RuntimeError, match="esperado=2.0.0 instalado=1.0.0"):
        check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))

    assert installs == [str(lock)]


def test_ensure_runtime_dependencies_uses_locked_install_when_package_is_missing(
    tmp_path, monkeypatch
):
    requirements = tmp_path / "requirements.txt"
    lock = tmp_path / "requirements.lock"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")
    lock.write_text("package-a==1.2.3 --hash=sha256:abcd\n", encoding="utf-8")
    available = set()
    installed_versions = {}
    installs = []

    def probe(module):
        return (module in available, None if module in available else "missing")

    def install(path):
        installs.append(path)
        available.add("module_a")
        installed_versions["package-a"] = "1.2.3"
        return 0

    def version(name):
        try:
            return installed_versions[name]
        except KeyError as exc:
            raise check_deps.metadata.PackageNotFoundError(name) from exc

    monkeypatch.setattr(check_deps, "_probe", probe)
    monkeypatch.setattr(check_deps, "_pip_install_locked", install)
    monkeypatch.setattr(check_deps.metadata, "version", version)
    monkeypatch.setattr(
        check_deps,
        "_pip_install",
        lambda _spec: pytest.fail("the range installer must not run when a lock exists"),
    )

    check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))

    assert installs == [str(lock)]


def test_ensure_runtime_dependencies_fails_closed_when_locked_install_fails(
    tmp_path, monkeypatch
):
    requirements = tmp_path / "requirements.txt"
    lock = tmp_path / "requirements.lock"
    requirements.write_text("package-a>=1,<2\n", encoding="utf-8")
    lock.write_text("package-a==1.2.3 --hash=sha256:abcd\n", encoding="utf-8")
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (False, "missing"))
    monkeypatch.setattr(check_deps, "_pip_install_locked", lambda _path: 1)
    monkeypatch.setattr(
        check_deps,
        "_pip_install",
        lambda _spec: pytest.fail("the range installer must not run when a lock exists"),
    )

    with pytest.raises(RuntimeError, match="requirements.lock"):
        check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))


def test_ensure_runtime_dependencies_installs_pinned_missing_packages_in_one_batch(
    tmp_path, monkeypatch
):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1,<2\npackage-b==3.4\n", encoding="utf-8")
    available = set()
    installs = []

    def probe(module):
        return (module in available, None if module in available else "missing")

    def install(spec):
        installs.append(spec)
        available.update({"module_a", "module_b"})
        return 0

    monkeypatch.setattr(check_deps, "_probe", probe)
    monkeypatch.setattr(check_deps, "_pip_install", install)

    check_deps.ensure_runtime_dependencies(
        [("module_a", "package-a"), ("module_b", "package-b")], str(requirements)
    )

    assert installs == ["package-a>=1,<2 package-b==3.4"]


def test_ensure_runtime_dependencies_falls_back_to_individual_installs_after_batch_failure(
    tmp_path, monkeypatch
):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1\npackage-b<4\n", encoding="utf-8")
    available = set()
    installs = []

    def probe(module):
        return (module in available, None if module in available else "missing")

    def install(spec):
        installs.append(spec)
        if len(installs) == 1:
            return 1
        if spec == "package-a>=1":
            available.add("module_a")
        if spec == "package-b<4":
            available.add("module_b")
        return 0

    monkeypatch.setattr(check_deps, "_probe", probe)
    monkeypatch.setattr(check_deps, "_pip_install", install)

    check_deps.ensure_runtime_dependencies(
        [("module_a", "package-a"), ("module_b", "package-b")], str(requirements)
    )

    assert installs == ["package-a>=1 package-b<4", "package-a>=1", "package-b<4"]


def test_ensure_runtime_dependencies_reports_every_package_still_missing_after_fallback(
    tmp_path, monkeypatch
):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1\npackage-b<4\n", encoding="utf-8")
    installs = []
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (False, "missing"))
    monkeypatch.setattr(
        check_deps,
        "_pip_install",
        lambda spec: installs.append(spec) or 1,
    )

    with pytest.raises(RuntimeError) as exc_info:
        check_deps.ensure_runtime_dependencies(
            [("module_a", "package-a"), ("module_b", "package-b")], str(requirements)
        )

    assert "package-a (módulo module_a)" in str(exc_info.value)
    assert "package-b (módulo module_b)" in str(exc_info.value)
    assert installs == ["package-a>=1 package-b<4", "package-a>=1", "package-b<4"]


def test_ensure_runtime_dependencies_raises_if_reprobe_still_fails(tmp_path, monkeypatch):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1\n", encoding="utf-8")
    probes = iter([(False, "missing"), (False, "still missing")])
    installs = []
    monkeypatch.setattr(check_deps, "_probe", lambda _module: next(probes))
    monkeypatch.setattr(check_deps, "_pip_install", lambda spec: installs.append(spec) or 0)

    with pytest.raises(RuntimeError, match=r"package-a \(módulo module_a\)"):
        check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))

    assert installs == ["package-a>=1"]


def test_ensure_runtime_dependencies_propagates_keyboard_interrupt(tmp_path, monkeypatch):
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("package-a>=1\n", encoding="utf-8")
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (False, "missing"))
    monkeypatch.setattr(
        check_deps, "_pip_install", lambda _spec: (_ for _ in ()).throw(KeyboardInterrupt())
    )

    with pytest.raises(KeyboardInterrupt):
        check_deps.ensure_runtime_dependencies([("module_a", "package-a")], str(requirements))
