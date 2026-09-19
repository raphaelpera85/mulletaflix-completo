"""Restore Nebula Telegram sidecars into the installed MulletaFlix metadata cache.

The script is intentionally delta-first: it writes a manifest before any download
and only downloads completed Telegram sidecars whose canonical internal metadata
cache file is absent or has a different size.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import os
import re
import tempfile
import urllib.parse
import urllib.request
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path, PureWindowsPath

import pymysql
from pymongo import MongoClient


SIDECAR_EXTENSIONS = {
    ".nfo", ".xml", ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif",
    ".bmp", ".tif", ".tiff", ".jpe", ".jif", ".jfif", ".jfi", ".tbn",
}
IMAGE_STEMS = {"poster", "fanart", "logo", "landscape", "clearart", "banner", "disc", "box", "back"}
EPISODE_THUMB = re.compile(r"^.+-S\d{1,2}E\d{1,3}-thumb$", re.IGNORECASE)


def load_nebula_config(path: str) -> dict[str, str]:
    import xml.etree.ElementTree as ET

    root = ET.parse(path).getroot()
    return {child.tag: child.text or "" for child in root}


def has_telegram_payload(doc: dict) -> bool:
    if doc.get("tg_file_id") or doc.get("file_id"):
        return True
    return any(
        isinstance(part, dict) and (part.get("tg_file_id") or part.get("tg_file"))
        for part in (doc.get("parts") or [])
    )


def virtual_directory_map(files) -> dict[str, tuple[str, str]]:
    result: dict[str, tuple[str, str]] = {}
    for doc in files:
        if doc.get("type") != "dir":
            continue
        parent = doc.get("parent")
        result[str(doc["_id"])] = (str(doc.get("name", "")), "" if parent is None else str(parent))
    return result


def virtual_path(doc: dict, dirs: dict[str, tuple[str, str]]) -> str:
    pieces = [str(doc.get("name", ""))]
    parent = doc.get("parent")
    parent = "" if parent is None else str(parent)
    seen: set[str] = set()
    while parent and parent not in seen and parent in dirs:
        seen.add(parent)
        name, next_parent = dirs[parent]
        pieces.append(name)
        parent = next_parent
    # Directory parents in older catalogs are canonical POSIX paths.
    if parent.startswith("/"):
        prefix = parent.strip("/").split("/")
        pieces = list(reversed(prefix)) + list(reversed(pieces))
    else:
        pieces.reverse()
    return "/" + "/".join(piece for piece in pieces if piece)


def canonical_name(name: str) -> str:
    stem, extension = os.path.splitext(name)
    lower = stem.lower()
    if lower == "fanart":
        stem = "backdrop"
    elif EPISODE_THUMB.match(stem):
        stem = "poster"
    return stem + extension.lower()


def normalize_virtual(path: str) -> str:
    value = path.replace("\\", "/")
    value = re.sub(r"^[A-Za-z]:", "", value)
    value = re.sub(r"^/?(?:raphael/)?", "/", value, flags=re.IGNORECASE)
    return "/" + "/".join(part for part in value.split("/") if part)


def item_cache_path(metadata_root: str, item_id: str, filename: str) -> str:
    compact = item_id.replace("-", "").lower()
    return os.path.join(metadata_root, "library", compact[:2], compact, filename)


def build_items(mysql_config: dict[str, str]) -> dict[str, str]:
    conn = pymysql.connect(
        host=mysql_config.get("server", "127.0.0.1"),
        port=int(mysql_config.get("port", "3306")),
        user=mysql_config.get("user", "root"),
        password=mysql_config.get("password", ""),
        database="mulletaflix",
        connect_timeout=10,
        read_timeout=30,
    )
    try:
        with conn.cursor() as cursor:
            cursor.execute("SELECT Id, Path, IsFolder FROM baseitems WHERE Path IS NOT NULL")
            rows = cursor.fetchall()
    finally:
        conn.close()

    result: dict[str, str] = {}
    for item_id, path, is_folder in rows:
        if is_folder or not path:
            continue
        normalized = normalize_virtual(str(PureWindowsPath(path).parent).replace("N:/", "/"))
        result.setdefault(normalized.casefold(), str(item_id))
    return result


def parse_mysql_config(path: str) -> dict[str, str]:
    import xml.etree.ElementTree as ET

    root = ET.parse(path).getroot()
    result: dict[str, str] = {}
    for option in root.findall(".//CustomDatabaseOption"):
        key = option.findtext("Key")
        value = option.findtext("Value")
        if key:
            result[key] = value or ""
    return result


def collect_delta(mongo_uri: str, metadata_root: str, mysql_config: dict[str, str]) -> list[dict]:
    client = MongoClient(mongo_uri, serverSelectionTimeoutMS=10_000)
    collection = client["ftp"]["files"]
    all_files = list(collection.find({}, {"_id": 1, "name": 1, "parent": 1, "type": 1}))
    dirs = virtual_directory_map(all_files)
    item_by_dir = build_items(mysql_config)
    delta: list[dict] = []
    for doc in collection.find({"status": "completed", "type": "file"}):
        name = str(doc.get("name", ""))
        if Path(name).suffix.lower() not in SIDECAR_EXTENSIONS or not has_telegram_payload(doc):
            continue
        vpath = virtual_path(doc, dirs)
        parent = "/".join(vpath.split("/")[:-1]) or "/"
        item_id = item_by_dir.get(normalize_virtual(parent).casefold())
        if not item_id:
            continue
        target_name = canonical_name(name)
        target = item_cache_path(metadata_root, item_id, target_name)
        expected = int(doc.get("size") or 0)
        exists = os.path.isfile(target)
        actual = os.path.getsize(target) if exists else 0
        if not exists or actual != expected:
            delta.append({
                "id": str(doc["_id"]), "name": name, "virtual_path": vpath,
                "item_id": item_id, "target": target, "expected_size": expected,
                "existing_size": actual if exists else None,
            })
    client.close()
    return delta


def write_manifest(path: str, delta: list[dict], mode: str) -> None:
    payload = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "mode": mode,
        "count": len(delta),
        "bytes": sum(item["expected_size"] for item in delta),
        "by_extension": dict(Counter(Path(item["name"]).suffix.lower() for item in delta)),
        "items": delta,
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)


def download_item(item: dict, base_url: str, token: str) -> tuple[bool, str]:
    target = item["target"]
    os.makedirs(os.path.dirname(target), exist_ok=True)
    query = urllib.parse.urlencode({"id": item["id"], "token": token})
    request = urllib.request.Request(f"{base_url.rstrip('/')}/stream?{query}")
    fd, temp_path = tempfile.mkstemp(prefix=".nebula-restore-", dir=os.path.dirname(target))
    os.close(fd)
    try:
        with urllib.request.urlopen(request, timeout=180) as response, open(temp_path, "wb") as output:
            remaining = item["expected_size"]
            while remaining > 0:
                chunk = response.read(min(1024 * 1024, remaining))
                if not chunk:
                    break
                output.write(chunk)
                remaining -= len(chunk)
        size = os.path.getsize(temp_path)
        if size != item["expected_size"]:
            return False, f"size mismatch: {size} != {item['expected_size']}"
        os.replace(temp_path, target)
        return True, "restored"
    except Exception as exc:  # noqa: BLE001 - report item-specific failures and continue
        return False, str(exc)
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", default=r"C:\ProgramData\MulletaFlix\Server\config\nebulaftp.xml")
    parser.add_argument("--database-config", default=r"C:\ProgramData\MulletaFlix\Server\config\database.xml")
    parser.add_argument("--metadata-root", default=r"C:\ProgramData\MulletaFlix\Server\data\metadata")
    parser.add_argument("--manifest", default=r"C:\ProgramData\MulletaFlix\Server\data\backups\nebula-metadata-restore-delta.json")
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--workers", type=int, default=8)
    args = parser.parse_args()
    config = load_nebula_config(args.config)
    delta = collect_delta(config["MongoDbConnectionString"], args.metadata_root, parse_mysql_config(args.database_config))
    write_manifest(args.manifest, delta, "execute" if args.execute else "dry-run")
    print(json.dumps({"delta_count": len(delta), "delta_bytes": sum(x["expected_size"] for x in delta), "manifest": args.manifest}, ensure_ascii=False))
    if not args.execute:
        return 0
    restored = failed = 0
    workers = max(1, min(args.workers, 16))
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
        futures = {
            executor.submit(download_item, item, "http://127.0.0.1:2123", config["HttpStreamToken"]): item
            for item in delta
        }
        for index, future in enumerate(concurrent.futures.as_completed(futures), 1):
            item = futures[future]
            ok, detail = future.result()
            if ok:
                restored += 1
            else:
                failed += 1
                print(json.dumps({"failed": item["name"], "target": item["target"], "detail": detail}, ensure_ascii=False))
            if index % 100 == 0 or index == len(delta):
                print(json.dumps({"processed": index, "restored": restored, "failed": failed}, ensure_ascii=False))
    return 0 if failed == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
