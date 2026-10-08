from __future__ import annotations

import importlib
from types import SimpleNamespace

import pytest

check_deps = importlib.import_module("tools.check_deps")


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


def test_ensure_runtime_dependencies_does_not_install_when_all_importable(monkeypatch):
    monkeypatch.setattr(check_deps, "_probe", lambda _module: (True, None))
    monkeypatch.setattr(
        check_deps,
        "_pip_install",
        lambda _spec: pytest.fail("pip must not run when all dependencies are importable"),
    )

    check_deps.ensure_runtime_dependencies([("module_a", "package-a")])


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
