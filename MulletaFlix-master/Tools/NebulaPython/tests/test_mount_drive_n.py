import argparse
import asyncio
import importlib.util
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[2] / "mount_drive_n.py"


def load_mount_module():
    spec = importlib.util.spec_from_file_location("mount_drive_n", SCRIPT_PATH)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def make_args(drive="N:"):
    return argparse.Namespace(drive=drive)


def test_mount_rejects_preexisting_drive_without_claiming_mount_ownership(monkeypatch, capsys):
    module = load_mount_module()
    expected_path = "N:\\"
    monkeypatch.setattr(Path, "exists", lambda self: str(self) == expected_path)
    monkeypatch.setattr(module, "acquire_instance_lock", lambda: (_ for _ in ()).throw(AssertionError("lock must not be acquired")))

    result = asyncio.run(module.mount(make_args()))

    assert result == 4
    assert "não é possível confirmar que a montagem pertence" in capsys.readouterr().out


def test_mount_reports_distinct_exit_code_when_another_helper_owns_mount(monkeypatch):
    module = load_mount_module()
    monkeypatch.setattr(Path, "exists", lambda self: False)
    monkeypatch.setattr(module, "acquire_instance_lock", lambda: False)

    result = asyncio.run(module.mount(make_args()))

    assert result == 3


def test_mount_reports_lock_acquisition_errors_as_failures(monkeypatch, capsys):
    module = load_mount_module()
    monkeypatch.setattr(Path, "exists", lambda self: False)

    def fail_lock_acquisition():
        raise PermissionError("lock file is not writable")

    monkeypatch.setattr(module, "acquire_instance_lock", fail_lock_acquisition)

    result = asyncio.run(module.mount(make_args()))

    assert result == 2
    assert "Não foi possível adquirir o lock de montagem" in capsys.readouterr().out
