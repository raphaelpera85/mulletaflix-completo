"""Mount the MulletaFlix Nebula FTP remote as drive N:.

This helper owns only the mount lifecycle. Credentials and the rclone
configuration are prepared by the MulletaFlix server, so this script does not
depend on the original Nebula checkout or read its environment.
"""

from __future__ import annotations

import argparse
import asyncio
import contextlib
import signal
from pathlib import Path
import shutil
import sys
import tempfile
from typing import Sequence

_lock_file = None


def acquire_instance_lock() -> bool:
    """Ensure only one mount_drive_n process runs at a time."""
    global _lock_file
    if sys.platform == "win32":
        try:
            import msvcrt
            lock_path = Path(tempfile.gettempdir()) / "mulletaflix_mount_n.lock"
            _lock_file = open(lock_path, "w")
            msvcrt.locking(_lock_file.fileno(), msvcrt.LK_NBLCK, 1)
            return True
        except (OSError, ImportError):
            return False
    return True


async def wait_for_ftp(host: str, port: int, timeout: float) -> bool:
    """Wait without blocking the event loop for the local FTP server."""
    deadline = asyncio.get_running_loop().time() + timeout
    while asyncio.get_running_loop().time() < deadline:
        remaining = max(0.1, deadline - asyncio.get_running_loop().time())
        try:
            reader, writer = await asyncio.wait_for(
                asyncio.open_connection(host, port), timeout=min(1.0, remaining)
            )
        except (OSError, asyncio.TimeoutError):
            await asyncio.sleep(min(1.0, remaining))
            continue
        writer.close()
        with contextlib.suppress(OSError):
            await writer.wait_closed()
        del reader
        return True
    return False


async def wait_for_mount(process: asyncio.subprocess.Process, drive: Path, timeout: float) -> bool:
    """Poll the mounted drive while also detecting an early rclone exit."""
    deadline = asyncio.get_running_loop().time() + timeout
    while asyncio.get_running_loop().time() < deadline:
        if process.returncode is not None:
            return False
        if drive.exists():
            return True
        await asyncio.sleep(1.0)
    return drive.exists()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Mount MulletaFlix Nebula FTP as N:")
    parser.add_argument("--rclone", required=True)
    parser.add_argument("--config", required=True)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=2121)
    parser.add_argument("--drive", default="N:")
    parser.add_argument("--log-file", required=True)
    parser.add_argument("--ftp-timeout", type=float, default=60.0)
    parser.add_argument("--mount-timeout", type=float, default=30.0)
    return parser


async def run_rclone_mount(rclone: str, config: Path, drive: str, log_file: str, stop_event: asyncio.Event) -> int:
    """Run rclone mount with automatic restart on failure. Flags match original Nebula."""
    while not stop_event.is_set():
        command: Sequence[str] = (
            rclone,
            "mount",
            "nebula:/",
            drive,
            "--config",
            str(config),
            "--network-mode",
            "--links",
            "--volname",
            "MulletaFlix",
            "--vfs-cache-mode",
            "writes",
            "--vfs-read-chunk-size",
            "1M",
            "--vfs-read-chunk-size-limit",
            "16M",
            "--vfs-cache-max-size",
            "20G",
            "--vfs-cache-max-age",
            "6h",
            "--dir-cache-time",
            "24h",
            "--attr-timeout",
            "10m",
            "--poll-interval",
            "0",
            "--buffer-size",
            "8M",
            "--no-checksum",
            "--vfs-fast-fingerprint",
            "--timeout",
            "60s",
            "--contimeout",
            "15s",
            "--retries",
            "5",
            "--low-level-retries",
            "10",
            "--ftp-idle-timeout",
            "15s",
            "--log-file",
            log_file,
            "--log-level",
            "INFO",
        )
        print(f"[NEBULA-MOUNT-PY] Iniciando rclone mount em {drive}.", flush=True)
        process = await asyncio.create_subprocess_exec(
            *command,
            stdin=asyncio.subprocess.DEVNULL,
            stdout=asyncio.subprocess.DEVNULL,
            stderr=asyncio.subprocess.DEVNULL,
        )
        
        # Wait for process to complete or stop_event
        wait_task = asyncio.create_task(process.wait())
        stop_wait_task = asyncio.create_task(stop_event.wait())
        
        done, pending = await asyncio.wait(
            [wait_task, stop_wait_task],
            return_when=asyncio.FIRST_COMPLETED
        )
        
        for task in pending:
            task.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await task
        
        if stop_event.is_set():
            # Graceful shutdown requested
            if process.returncode is None:
                process.terminate()
                with contextlib.suppress(ProcessLookupError, asyncio.TimeoutError):
                    await asyncio.wait_for(process.wait(), timeout=5.0)
            print(f"[NEBULA-MOUNT-PY] rclone mount encerrado (solicitado).", flush=True)
            return 0
        
        # Let the server own the recovery cycle. Restarting here can leave
        # multiple helper processes racing for the same WinFsp drive letter.
        returncode = process.returncode
        print(f"[NEBULA-MOUNT-PY-ERRO] rclone encerrou com código {returncode}.", flush=True)
        return returncode or 1


async def mount(args: argparse.Namespace) -> int:
    if not acquire_instance_lock():
        print("[NEBULA-MOUNT-PY] Outro processo de montagem de N: já está em execução.", flush=True)
        return 0

    rclone = args.rclone if Path(args.rclone).is_file() else shutil.which(args.rclone)
    if not rclone:
        print("[NEBULA-MOUNT-PY-ERRO] rclone não foi encontrado.", flush=True)
        return 2

    config = Path(args.config)
    if not config.is_file():
        print(f"[NEBULA-MOUNT-PY-ERRO] Configuração não encontrada: {config}", flush=True)
        return 2

    drive = Path(args.drive + "\\")
    if drive.exists():
        print(f"[NEBULA-MOUNT-PY] Unidade {args.drive} já está acessível.", flush=True)
        return 0

    print(
        f"[NEBULA-MOUNT-PY] Aguardando FTP {args.host}:{args.port} por {args.ftp_timeout:.0f}s...",
        flush=True,
    )
    if not await wait_for_ftp(args.host, args.port, args.ftp_timeout):
        print("[NEBULA-MOUNT-PY-AVISO] FTP não respondeu no prazo; tentando montar mesmo assim.", flush=True)

    # Run rclone with supervision loop
    stop_event = asyncio.Event()
    
    # Handle Ctrl+C
    loop = asyncio.get_running_loop()
    def signal_handler():
        print("[NEBULA-MOUNT-PY] Sinal de interrupção recebido. Parando...", flush=True)
        stop_event.set()
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, signal_handler)
        except NotImplementedError:
            # Windows doesn't support add_signal_handler for SIGTERM
            pass
    
    return await run_rclone_mount(rclone, config, args.drive, args.log_file, stop_event)


def main() -> int:
    args = build_parser().parse_args()
    try:
        return asyncio.run(mount(args))
    except KeyboardInterrupt:
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
