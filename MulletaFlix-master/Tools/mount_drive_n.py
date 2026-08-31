"""Mount the MulletaFlix Nebula FTP remote as drive N:.

This helper owns only the mount lifecycle. Credentials and the rclone
configuration are prepared by the MulletaFlix server, so this script does not
depend on the original Nebula checkout or read its environment.
"""

from __future__ import annotations

import argparse
import asyncio
import contextlib
from pathlib import Path
import shutil
from typing import Sequence


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


async def mount(args: argparse.Namespace) -> int:
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

    command: Sequence[str] = (
        rclone,
        "mount",
        "nebula:/",
        args.drive,
        "--config",
        str(config),
        "--vfs-cache-mode",
        "full",
        "--vfs-cache-max-size",
        "20G",
        "--dir-cache-time",
        "30s",
        "--poll-interval",
        "0",
        "--links",
        "--no-checksum",
        "--network-mode",
        # O filesystem FTP virtual aceita o conteúdo, mas não oferece
        # alteração de timestamps. Mantemos MDTM desativado para que cada
        # metadata .nfo não termine com 550 durante o SetModTime.
        "--ftp-writing-mdtm=false",
        "--volname",
        "NebulaFTP",
        "--log-file",
        args.log_file,
        "--log-level",
        "INFO",
    )
    print(f"[NEBULA-MOUNT-PY] Executando rclone mount em {args.drive}.", flush=True)
    process = await asyncio.create_subprocess_exec(
        *command,
        stdin=asyncio.subprocess.DEVNULL,
        stdout=asyncio.subprocess.DEVNULL,
        stderr=asyncio.subprocess.DEVNULL,
    )
    try:
        if not await wait_for_mount(process, drive, args.mount_timeout):
            returncode = await process.wait() if process.returncode is not None else None
            detail = f" código {returncode}" if returncode is not None else " sem confirmação de N:"
            print(f"[NEBULA-MOUNT-PY-ERRO] rclone encerrou{detail}.", flush=True)
            return 1
        print(f"[NEBULA-MOUNT-PY] Unidade {args.drive} montada com sucesso.", flush=True)
        await process.wait()
        return process.returncode or 0
    except asyncio.CancelledError:
        raise
    finally:
        if process.returncode is None:
            process.terminate()
            with contextlib.suppress(ProcessLookupError, asyncio.TimeoutError):
                await asyncio.wait_for(process.wait(), timeout=5.0)


def main() -> int:
    args = build_parser().parse_args()
    try:
        return asyncio.run(mount(args))
    except KeyboardInterrupt:
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
