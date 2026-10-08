import logging
import hashlib
import asyncio
import contextlib
import json
import os
import re
import shutil
import unicodedata
from asyncio import CancelledError, Lock, gather, get_event_loop
from contextlib import asynccontextmanager
from asyncio import sleep as asleep
from collections import OrderedDict, namedtuple
from functools import wraps
from io import BytesIO
from itertools import count
from os import environ
from pathlib import PurePosixPath
from sys import exc_info
from time import time
from uuid import uuid4

import aiofiles

try:
    from pymongo.errors import ConnectionFailure, DuplicateKeyError, PyMongoError, ServerSelectionTimeoutError
except ImportError:
    # Fallbacks if pymongo not installed (e.g., test-only env): widen defensively
    class _PyMongoError(Exception):
        pass
    class _ConnectionFailure(_PyMongoError):
        pass
    class _ServerSelectionTimeoutError(_PyMongoError):
        pass
    class _DuplicateKeyError(_PyMongoError):
        pass
    ConnectionFailure = _ConnectionFailure
    DuplicateKeyError = _DuplicateKeyError
    ServerSelectionTimeoutError = _ServerSelectionTimeoutError
    PyMongoError = _PyMongoError

from .common import UPLOAD_QUEUE
from .errors import PathIOError

try:
    from .tg import File  # requires pyrogram + ideally tgcrypto
except (ImportError, RuntimeError, OSError):
    # RuntimeError catches "There is no current event loop" on Py 3.14+
    # when pyrogram's sync wrapper init runs at import time.
    File = None  # type: ignore[assignment,misc]

logger = logging.getLogger("NebulaFTP")
STREAM_BOT_CURSOR = count()


@asynccontextmanager
async def _playback_cache_lease(media_id):
    lease_port = int(environ.get("NEBULA_CACHE_LEASE_PORT", "0"))
    lease_token = environ.get("NEBULA_CACHE_LEASE_TOKEN", "")
    if not lease_port or not lease_token or media_id is None:
        yield False
        return

    reader = writer = None
    lease_id = None
    try:
        reader, writer = await asyncio.wait_for(asyncio.open_connection("127.0.0.1", lease_port), timeout=1)
        writer.write((json.dumps({"action": "acquire", "mediaId": str(media_id), "token": lease_token}) + "\n").encode())
        await writer.drain()
        response = json.loads(await asyncio.wait_for(reader.readline(), timeout=1))
        lease_id = response.get("leaseId") if response.get("ok") else None
    except (OSError, asyncio.TimeoutError, ValueError, json.JSONDecodeError):
        if writer:
            writer.close()
            with contextlib.suppress(Exception):
                await writer.wait_closed()
        yield False
        return

    try:
        yield lease_id is not None
    finally:
        if lease_id and writer and reader:
            try:
                writer.write((json.dumps({"action": "release", "leaseId": lease_id, "token": lease_token}) + "\n").encode())
                await writer.drain()
                await asyncio.wait_for(reader.readline(), timeout=1)
            except (OSError, asyncio.TimeoutError):
                pass
        if writer:
            writer.close()
            with contextlib.suppress(Exception):
                await writer.wait_closed()

__all__ = (
    "AbstractPathIO", "PathIONursery", "MongoDBPathIO", "BoundedLRUCache",
    "is_uploadable_name", "movie_folder_score", "resolve_part_bot", "resolve_part_bots",
)

def get_free_bytes(path: str) -> int:
    """Retorna os bytes livres no disco correspondente ao caminho."""
    try:
        p = os.path.abspath(path)
        while not os.path.exists(p):
            parent = os.path.dirname(p)
            if parent == p or not parent:
                p = os.path.abspath(path)
                break
            p = parent
        return shutil.disk_usage(p).free
    except Exception:
        return 0


def get_cache_dir(required_bytes: int = 0) -> str:
    """Retorna o diretorio de staging por ordem de prioridade/velocidade com espaco livre suficiente."""
    if not CACHE_DIRS:
        return os.path.abspath("staging")

    # 1. Tenta o disco mais rapido na ordem configurada (ex: E: SSD -> F: USB3 -> I:) que tenha espaco livre seguro
    for d in CACHE_DIRS:
        try:
            p = os.path.abspath(d)
            while not os.path.exists(p):
                parent = os.path.dirname(p)
                if parent == p or not parent:
                    p = os.path.abspath(d)
                    break
                p = parent
            usage = shutil.disk_usage(p)
            reserve = usage.total * 10 // 100
            min_free = max(5 * 1024**3, reserve)
            if usage.free - (required_bytes or 0) >= min_free:
                return d
        except Exception:
            continue

    # 2. Fallback: Se os discos mais rapidos estiverem cheios, usa o de maior espaco livre absoluto
    return max(CACHE_DIRS, key=get_free_bytes)


CACHE_DIRS = [
    os.path.abspath(path.strip())
    for path in environ.get("STAGING_DIRS", environ.get("STAGING_DIR", "staging")).split(";")
    if path.strip()
]
CACHE_DIR = get_cache_dir()
UPLOADABLE_EXTENSIONS = {
    ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v",
    ".ts", ".webm", ".flv", ".mpeg", ".mpg", ".m2ts", ".3gp",
    ".sub", ".ass", ".ssa", ".vtt", ".srt",
    # Other media commonly stored alongside the video library.  These must
    # use the same predicate as the staging scanner; otherwise they are
    # silently treated as completed local files and never reach Telegram.
    ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tif", ".tiff",
    ".mp3", ".m4a", ".flac", ".wav", ".aac", ".ogg", ".opus", ".oga",
}
MOVIE_TOKEN_NOISE = {
    "aac", "ac3", "amzn", "bluray", "brrip", "com", "dual", "fgt", "galaxyrg",
    "h264", "h265", "hdr", "imax", "lapumia", "rip", "web", "webdl", "webrip",
    "www", "x264", "x265", "yify", "yts",
    "de", "da", "do", "das", "dos", "em", "um", "uma", "os", "as", "na", "no", "nas", "nos",
    "por", "para", "com", "sem", "the", "of", "and", "in", "on", "at", "to", "for", "with",
}
for cache_dir in CACHE_DIRS:
    try:
        os.makedirs(cache_dir, exist_ok=True)
    except (OSError, ValueError):
        pass


def is_uploadable_name(name):
    if name.endswith(".partial"):
        name = name[:-8]
    return os.path.splitext(name)[1].lower() in UPLOADABLE_EXTENSIONS


def movie_tokens(name):
    if name.endswith(".partial"):
        name = name[:-8]
    name = os.path.splitext(name)[0]
    text = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode("ascii")
    tokens = set(re.findall(r"[a-z0-9]+", text.lower()))
    return {t for t in tokens if len(t) > 1 and t not in MOVIE_TOKEN_NOISE and not re.fullmatch(r"\d{3,4}p", t)}


def movie_folder_score(filename, foldername):
    file_tokens = movie_tokens(filename)
    folder_tokens = movie_tokens(foldername)
    if not file_tokens or not folder_tokens:
        return 0.0
    file_years = {t for t in file_tokens if re.fullmatch(r"(19|20)\d{2}", t)}
    folder_years = {t for t in folder_tokens if re.fullmatch(r"(19|20)\d{2}", t)}
    file_title = file_tokens - file_years
    folder_title = folder_tokens - folder_years
    if not file_title or not folder_title:
        return 0.0
    common_title = file_title & folder_title
    if not common_title:
        return 0.0
    score = len(common_title) / min(len(file_title), len(folder_title))
    if file_years and folder_years:
        score += 0.25 if file_years & folder_years else -0.50
    return score


def resolve_part_bot(part, tg):
    if isinstance(tg, (list, tuple)):
        if not tg:
            return None
        bot_name = part.get("bot_name")
        if bot_name:
            for bot in tg:
                if getattr(bot, "name", None) == bot_name:
                    return bot
        return tg[part.get("bot_index", 0) % len(tg)]
    return tg


def resolve_part_bots(part, tg):
    if not isinstance(tg, (list, tuple)):
        return [tg] if tg is not None else []
    if not tg:
        return []
    bot_name = part.get("bot_name")
    if bot_name:
        for idx, bot in enumerate(tg):
            if getattr(bot, "name", None) == bot_name:
                start = next(STREAM_BOT_CURSOR) % len(tg)
                rotated = [*tg[start:], *tg[:start]]
                candidates = [tg[idx], tg[(idx + 1) % len(tg)], *rotated]
                return list(dict.fromkeys(candidates))
    index = part.get("bot_index", 0) % len(tg)
    start = next(STREAM_BOT_CURSOR) % len(tg)
    rotated = [*tg[start:], *tg[:start]]
    candidates = [tg[index], tg[(index + 1) % len(tg)], *rotated]
    return list(dict.fromkeys(candidates))


async def reserve_free_stream_bot(candidates):
    """Reserve the first idle bot without interrupting active work."""
    while candidates:
        for bot in candidates:
            uploads = getattr(bot, "_nebula_uploads", 0)
            streams = getattr(bot, "_nebula_streams", 0)
            if not isinstance(uploads, int):
                uploads = 0
            if not isinstance(streams, int):
                streams = 0
            if uploads == 0 and streams == 0:
                # No await between the check and increment: this reservation is
                # atomic with respect to other tasks on the asyncio event loop.
                bot._nebula_streams = 1
                return bot
        await asleep(0.1)
    return None


class BoundedLRUCache(OrderedDict):
    """Tiny LRUCache so we don't pull in cachetools."""

    def __init__(self, maxsize: int = 10000):
        super().__init__()
        self.maxsize = max(1, int(maxsize))

    def __setitem__(self, key, value):
        if key in self:
            self.move_to_end(key)
        super().__setitem__(key, value)
        while len(self) > self.maxsize:
            self.popitem(last=False)

    def __getitem__(self, key):
        value = super().__getitem__(key)
        self.move_to_end(key)
        return value

    def get(self, key, default=None):
        if key in self:
            self.move_to_end(key)
            return super().__getitem__(key)
        return default

    def pop(self, key, default=...):
        """Remove a cache entry without triggering the LRU read side effect."""
        if key not in self:
            if default is ...:
                raise KeyError(key)
            return default
        value = OrderedDict.__getitem__(self, key)
        OrderedDict.__delitem__(self, key)
        return value

    def popitem(self, last=True):
        """Remove the oldest/newest entry without invoking ``__getitem__``."""
        if not self:
            raise KeyError("dictionary is empty")
        key = next(reversed(self)) if last else next(iter(self))
        value = OrderedDict.__getitem__(self, key)
        OrderedDict.__delitem__(self, key)
        return key, value

def universal_exception(coro):
    @wraps(coro)
    async def wrapper(*args, **kwargs):
        try:
            return await coro(*args, **kwargs)
        except (CancelledError, NotImplementedError, StopAsyncIteration):
            raise
        except Exception as exc:
            raise PathIOError(reason=exc_info()) from exc
    return wrapper

class PathIONursery:
    def __init__(self, factory):
        self.factory = factory
        self.state = None

    def __call__(self, *args, **kwargs):
        instance = self.factory(*args, state=self.state, **kwargs)
        if self.state is None:
            self.state = instance.state
        return instance

class AbstractPathIO:
    def __init__(self, connection=None):
        self.connection = connection

class Node:
    def __init__(self, type, name, ctime=None, mtime=None, size=0, parent="/", parts=None, local_path=None, **k):
        if parts is None: parts = []
        self.type = type
        self.name = name
        self.ctime = ctime or int(time())
        self.mtime = mtime or int(time())
        self.size = size
        self.parent = parent
        self.path = str(PurePosixPath(parent) / name)
        self.parts = parts
        self.local_path = local_path
        self.id = k.get("_id")

class MongoDBMemoryIO:
    def __init__(self, node, mode, tg, db):
        self._node = node; self._mode = mode; self._tg = tg; self._db = db
        self.offset = 0
        self.safe_name = f"{uuid4().hex}_{node.name}"
        staged_path = getattr(node, "local_path", None)
        if mode == "r+b" and staged_path and os.path.isfile(staged_path):
            self.local_path = staged_path
        else:
            cache_dir = get_cache_dir()
            self.local_path = os.path.join(cache_dir, self.safe_name)

    async def __aenter__(self): return self
    async def __aexit__(self, *args, **kwargs): pass
    async def seek(self, offset=0): self.offset = offset

    async def write_stream(self, stream):
        try:
            # A resumed STOR must retain the already staged prefix.  Use wb only
            # for a new transfer (or when there is no staging file to resume).
            mode = "r+b" if self.offset > 0 and os.path.isfile(self.local_path) else "wb"
            async with aiofiles.open(self.local_path, mode) as f:
                if self.offset > 0: await f.seek(self.offset)
                async for data in stream.iter_by_block(1024*1024):
                    await f.write(data)
                await f.flush()
        except Exception as e:
            logger.error(f"❌ [WRITE] Erro disco: {e}"); raise

        final_size = os.path.getsize(self.local_path)
        parent = self._node.parent
        name = self._node.name
        cache_key = f"{parent}::{name}"
        now = int(time())
        should_upload = is_uploadable_name(name)
        if should_upload and final_size == 0:
            try:
                os.remove(self.local_path)
            except OSError as exc:
                logger.debug("empty upload cleanup skipped (%s): %s", name, exc)
            await self._db.files.delete_one({"name": name, "parent": parent})
            async with MongoDBPathIO._cache_lock:
                MongoDBPathIO._memory_cache.pop(cache_key, None)
            logger.warning("Upload vazio ignorado: %s", name)
            return

        doc_cache = {
            "type": "file", "name": name, "parent": parent, "size": final_size,
            "status": "staging" if should_upload else "completed",
            "mtime": now, "ctime": now, "parts": []
        }
        if should_upload:
            doc_cache["local_path"] = self.local_path
        else:
            try:
                os.remove(self.local_path)
            except OSError as exc:
                logger.debug("ignored file cleanup skipped (%s): %s", name, exc)

        # Atualiza Cache (Prioridade para Rclone)
        async with MongoDBPathIO._cache_lock:
            MongoDBPathIO._memory_cache[cache_key] = doc_cache

        # Atualiza DB em background (best effort) — falhas no DB não devem bloquear uploads
        try:
            await self._db.files.replace_one({"name": name, "parent": parent}, doc_cache, upsert=True)
        except (ConnectionFailure, ServerSelectionTimeoutError) as exc:
            logger.warning("DB upsert skipped for %s: %s", name, exc)
        except PyMongoError as exc:
            logger.debug("DB upsert transient error for %s: %s", name, exc)

        if name.endswith(".partial"):
             logger.debug("[WRITE] Aguardando rename para: %s", name)
        else:
             logger.debug("[WRITE] Arquivo salvo em staging: %s", name)

    async def iter_by_block(self, block_size):
        if self._node.local_path and os.path.exists(self._node.local_path):
            async with aiofiles.open(self._node.local_path, 'rb') as f:
                await f.seek(self.offset)
                while True:
                    chunk = await f.read(block_size)
                    if not chunk: break
                    yield chunk
            return

        if File is None:
            logger.error("Cannot stream from Telegram: pyrogram/tgcrypto not installed")
            return
        parts = self._node.parts
        if not parts: return
        parts.sort(key=lambda x: x["part_id"])
        current_file_pos = 0; start_read_at = self.offset

        for part in parts:
            part_size = part.get("file_size", 2 * 1024 * 1024 * 1024)
            part_end = current_file_pos + part_size
            if part_end <= start_read_at: current_file_pos += part_size; continue
            local_offset = max(0, start_read_at - current_file_pos)
            cache_root = environ.get("NEBULA_PLAYBACK_CACHE_ROOT")
            if cache_root and self._node.id:
                media_key = hashlib.sha256(f"mongo:{self._node.id}".encode("utf-8")).hexdigest().upper()
                part_index = int(part.get("part_number", part.get("part_id", 0)))
                chunk_size = 1024 * 1024
                chunk_index = local_offset // chunk_size
                cache_hit = False
                async with _playback_cache_lease(self._node.id) as has_cache_lease:
                    cache_hit = has_cache_lease
                    while cache_hit and local_offset < part_size:
                        chunk_offset = chunk_index * chunk_size
                        expected_size = min(chunk_size, part_size - chunk_offset)
                        cache_file = os.path.join(
                            cache_root, media_key, f"{part_index:06d}-{chunk_index:08d}.bin"
                        )
                        try:
                            if os.path.getsize(cache_file) != expected_size:
                                cache_hit = False
                                break
                            async with aiofiles.open(cache_file, "rb") as cached:
                                offset_in_chunk = local_offset - chunk_offset
                                await cached.seek(offset_in_chunk)
                                remaining = expected_size - offset_in_chunk
                                while remaining > 0:
                                    data = await cached.read(min(block_size, remaining))
                                    if not data:
                                        cache_hit = False
                                        break
                                    yield data
                                    remaining -= len(data)
                            if not cache_hit:
                                break
                        except OSError:
                            cache_hit = False
                            break
                        local_offset = chunk_offset + expected_size
                        chunk_index += 1
                if cache_hit:
                    current_file_pos += part_size
                    start_read_at = current_file_pos
                    continue
                # Any absent or incomplete cache chunk falls through to Telegram from
                # the requested byte position. Already yielded complete cached chunks
                # stay valid, so skip those bytes when calculating the remote offset.
                start_read_at = current_file_pos + local_offset
                local_offset = max(0, start_read_at - current_file_pos)
            candidates = resolve_part_bots(part, self._tg)
            if not candidates:
                logger.error("Cannot stream from Telegram: no bot clients configured")
                return
            streamed = False
            remaining_candidates = list(candidates)
            while remaining_candidates:
                tg = await reserve_free_stream_bot(remaining_candidates)
                if tg is None:
                    break
                try:
                    remaining_candidates.remove(tg)
                    bot_number = self._tg.index(tg) + 1 if isinstance(self._tg, (list, tuple)) else 1
                    logger.info(
                        "[STREAM] Midia=%s parte=%s tentando Bot #%s",
                        self._node.name,
                        part.get("part_id"),
                        bot_number,
                    )
                    file = File(
                        part["tg_file"],
                        tg,
                        chat_id=os.environ.get("CHAT_ID"),
                        message_id=part.get("tg_message"),
                    )
                    async for chunk in file.stream(offset=local_offset):
                        if not streamed:
                            bot_name = getattr(tg, "name", None)
                            bot_changed = (
                                part.get("bot_index") != bot_number - 1
                                or (bot_name and part.get("bot_name") != bot_name)
                            )
                            if self._node.id and (bot_changed or file.reference_refreshed):
                                try:
                                    updates = {
                                        "parts.$.bot_index": bot_number - 1,
                                        "parts.$.bot_name": bot_name,
                                        "parts.$.stream_verified_at": int(time()),
                                        "stream_bot_name": bot_name,
                                    }
                                    if file.reference_refreshed:
                                        updates["parts.$.tg_file"] = file.file_id
                                        updates["parts.$.reference_updated_at"] = int(time())
                                    await self._db.files.update_one(
                                        {"_id": self._node.id, "parts.part_id": part.get("part_id")},
                                        {"$set": updates},
                                    )
                                    part["bot_index"] = bot_number - 1
                                    part["bot_name"] = bot_name
                                    if file.reference_refreshed:
                                        part["tg_file"] = file.file_id
                                    logger.info(
                                        "[STREAM] Rota salva: midia=%s parte=%s Bot #%s",
                                        self._node.name,
                                        part.get("part_id"),
                                        bot_number,
                                    )
                                except Exception as exc:
                                    logger.warning("[STREAM] Nao foi possivel salvar referencia: %s", exc)
                            logger.info(
                                "[STREAM] Midia=%s parte=%s atendida pelo Bot #%s",
                                self._node.name,
                                part.get("part_id"),
                                bot_number,
                            )
                        streamed = True
                        yield chunk
                finally:
                    tg._nebula_streams = max(0, getattr(tg, "_nebula_streams", 1) - 1)
                if streamed:
                    break
                logger.warning(
                    "[STREAM] Midia=%s parte=%s sem dados no Bot #%s",
                    self._node.name,
                    part.get("part_id"),
                    bot_number,
                )
            if not streamed:
                logger.error("Cannot stream Telegram part %s with configured bot indexes", part.get("part_id"))
                return
            current_file_pos += part_size; start_read_at = current_file_pos

class MongoDBPathIO(AbstractPathIO):
    db = None; tg = None
    _cache_maxsize = int(environ.get("PATHIO_CACHE_MAXSIZE", "10000"))
    _memory_cache = BoundedLRUCache(maxsize=_cache_maxsize)
    _cache_lock = Lock()
    Stats = namedtuple("Stats", ("st_size", "st_ctime", "st_mtime", "st_nlink", "st_mode"))

    @property
    def _files(self):
        if self.db is None:
            raise PathIOError("Banco de dados MongoDB nao foi inicializado")
        return self.db.files

    def __init__(self, *args, state=None, cwd=None, **kwargs):
        super().__init__(*args, **kwargs); self.cwd = PurePosixPath("/")

    @property
    def state(self): return []

    def _absolute(self, path):
        if not path.is_absolute(): path = self.cwd / path
        return path

    def _sanitize(self, text):
        if not text: return ""
        return unicodedata.normalize('NFC', str(text))

    def _split_path(self, path_obj):
        p_str = self._sanitize(path_obj.as_posix())
        if not p_str.startswith("/"): p_str = "/" + p_str
        if p_str != "/" and p_str.endswith("/"): p_str = p_str[:-1]
        return os.path.dirname(p_str), os.path.basename(p_str)

    async def _parent_values_for_path(self, parent_path):
        """Return legacy path and BSON-id parent values for a virtual directory."""
        normalized = self._sanitize(PurePosixPath(parent_path).as_posix())
        if not normalized.startswith("/"):
            normalized = "/" + normalized
        if normalized == "/":
            return ["/", "", None]

        parent_values = ["/", "", None]
        current = "/"
        for segment in (part for part in normalized.split("/") if part):
            candidates = [f"{current.rstrip('/')}/{segment}" if current != "/" else f"/{segment}"]
            if candidates[0].startswith("/"):
                candidates.append(candidates[0][1:])
            node = None
            for parent_value in parent_values:
                node = await self._files.find_one({"name": segment, "parent": parent_value, "type": "dir"})
                if node:
                    break
            if not node:
                return candidates
            node_id = node.get("_id")
            parent_values = [node_id] if node_id is not None else candidates
            parent_values.extend(candidates)
            current = candidates[0]
        return list(dict.fromkeys(parent_values))

    async def _find_child(self, name, parent_path, *, node_type=None):
        parent_values = await self._parent_values_for_path(parent_path)
        for parent_value in parent_values:
            query = {"name": name, "parent": parent_value}
            if node_type:
                query["type"] = node_type
            node = await self._files.find_one(query)
            if node:
                return node
        return None

    async def get_node(self, path):
        if str(path) in ("/", "."): return Node("dir", "", 0, 0, size=0, parent="/")
        parent, name = self._split_path(path)
        cache_key = f"{parent}::{name}"

        async with self._cache_lock:
            if cache_key in self._memory_cache:
                return Node(**self._memory_cache[cache_key])

        if self.db is None:
            return None

        node = await self._find_child(name, parent)
        if node:
            virtual_node = {**node, "parent": parent}
            async with self._cache_lock: self._memory_cache[cache_key] = virtual_node
            return Node(**virtual_node)

        return None

    @universal_exception
    async def exists(self, path): return (await self.get_node(self._absolute(path))) is not None

    @universal_exception
    async def is_dir(self, path):
        node = await self.get_node(self._absolute(path))
        return not (node is None or node.type != "dir")

    @universal_exception
    async def is_file(self, path):
        node = await self.get_node(self._absolute(path))
        return not (node is None or node.type != "file")

    @universal_exception
    async def mkdir(self, path, *, exist_ok=False):
        path = self._absolute(path)
        if await self.get_node(path):
            if not exist_ok: raise FileExistsError
            return
        parent, name = self._split_path(path)
        doc = {"type": "dir", "ctime": int(time()), "mtime": int(time()), "name": name, "parent": parent, "size": 0}
        key = f"{parent}::{name}"
        try:
            await self._files.insert_one(doc)
        except DuplicateKeyError:
            # Lost the race against a concurrent insert. Treat as success
            # only when the caller opted into exist_ok=True; otherwise we
            # must surface the conflict (the previous code raised the
            # wrong exception type unconditionally).
            existing = await self.get_node(path)
            if not existing:
                raise FileExistsError from None
            if not exist_ok:
                raise FileExistsError
            async with self._cache_lock:
                self._memory_cache[key] = existing
            return
        except PyMongoError as exc:
            logger.debug("mkdir transient error (%s): %s", name, exc)
            raise
        async with self._cache_lock:
            self._memory_cache[key] = doc

    @universal_exception
    async def rmdir(self, path):
        path = self._absolute(path)
        parent, name = self._split_path(path)
        key = f"{parent}::{name}"
        full = f"{parent}/{name}" if parent != "/" else f"/{name}"
        node = await self._find_child(name, parent, node_type="dir")
        if node is None:
            raise FileNotFoundError(full)

        # FTP RMD is defined for empty directories. Never recursively delete
        # a media tree here: an accidental Explorer/rclone RMD must not erase
        # all files below a category or series folder.
        parent_values = await self._parent_values_for_path(full)
        child_count = await self._files.count_documents({"parent": {"$in": parent_values}})
        if child_count:
            raise OSError(f"directory not empty: {full}")

        async with self._cache_lock:
            self._memory_cache.pop(key, None)
        await self._files.delete_one({"_id": node["_id"]})

    @universal_exception
    async def unlink(self, path):
        path = self._absolute(path)
        parent, name = self._split_path(path)
        raw = await self._find_child(name, parent)
        if raw:
            async with self._cache_lock: self._memory_cache.pop(f"{parent}::{name}", None)
            if raw.get("local_path") and os.path.exists(raw["local_path"]):
                try:
                    os.remove(raw["local_path"])
                except OSError as exc:
                    logger.debug("unlink local file skipped (%s): %s", name, exc)
            await self._files.delete_one({"_id": raw["_id"]})

    def list(self, path):
        path = self._absolute(path)
        search = path.as_posix()
        if not search.startswith("/"): search = "/" + search
        if search != "/" and search.endswith("/"): search = search[:-1]

        class Lister:
            iter = None
            def __aiter__(self): return self
            @universal_exception
            async def __anext__(cls):
                if cls.iter is None:
                    parent_values = await self._parent_values_for_path(search)
                    cls.iter = self._files.find({"parent": {"$in": parent_values}, "name": {"$not": {"$regex": r"\.partial$"}}})
                try:
                    doc = await cls.iter.__anext__()
                    return path / doc["name"]
                except StopAsyncIteration: raise
        return Lister()

    @universal_exception
    async def stat(self, path):
        node = await self.get_node(self._absolute(path))
        if node is None: raise FileNotFoundError
        mode = (0x8000 | 0o666) if node.type == "file" else (0x4000 | 0o777)
        return MongoDBPathIO.Stats(node.size, node.ctime, node.mtime, 1, mode)

    @universal_exception
    async def open(self, path, mode="rb", *args, **kwargs):
        path = self._absolute(path)
        parent, name = self._split_path(path)
        if mode == "wb":
            doc = {"type": "file", "ctime": int(time()), "mtime": int(time()), "name": name, "parent": parent, "size": 0, "parts": []}
            async with self._cache_lock: self._memory_cache[f"{parent}::{name}"] = doc
            existing = await self._find_child(name, parent)
            if existing and existing.get("_id") is not None:
                doc["_id"] = existing["_id"]
                await self._files.replace_one({"_id": existing["_id"]}, doc, upsert=True)
            else:
                await self._files.replace_one({"name": name, "parent": parent}, doc, upsert=True)

        node = await self.get_node(path)
        if not node and mode == "rb": raise FileNotFoundError
        return MongoDBMemoryIO(node, mode, self.tg, self.db)

    @universal_exception
    async def rename(self, source, destination):
        source = self._absolute(source); destination = self._absolute(destination)
        # logger.info(f"🔄 [RENAME] {source} → {destination}")

        src_p, src_n = self._split_path(source)
        dst_p, dst_n = self._split_path(destination)

        # 1. BUSCA ORIGEM NO CACHE PRIMEIRO
        old_key = f"{src_p}::{src_n}"
        new_key = f"{dst_p}::{dst_n}"
        src_doc = None

        async with self._cache_lock:
            src_doc = self._memory_cache.get(old_key)

        if not src_doc:
            src_doc = await self._find_child(src_n, src_p)

        if not src_doc:
            logger.warning(f"⚠️ [RENAME] Origem não encontrada: {source}")
            return

        existing_dst = await self._find_child(dst_n, dst_p)
        if existing_dst and existing_dst.get("_id") != src_doc.get("_id"):
            if existing_dst.get("type") == "dir" and src_doc.get("type") == "dir":
                async with self._cache_lock:
                    self._memory_cache.pop(old_key, None)
                await self._files.delete_one({"_id": src_doc["_id"]})
                return
            if existing_dst.get("local_path") and os.path.exists(existing_dst["local_path"]):
                try:
                    os.remove(existing_dst["local_path"])
                except OSError as exc:
                    logger.debug("overwrite local cleanup skipped (%s): %s", dst_n, exc)
            await self._files.delete_one({"_id": existing_dst["_id"]})

        # 2. Atualiza Cache Atomicamente
        async with self._cache_lock:
            self._memory_cache.pop(old_key, None)
            self._memory_cache.pop(new_key, None)

            src_doc["name"] = dst_n
            src_doc["parent"] = dst_p
            src_doc["mtime"] = int(time())

            self._memory_cache[new_key] = src_doc

        # 3. Atualiza DB
        await self._files.update_one(
            {"_id": src_doc["_id"]},
            {"$set": {"name": dst_n, "parent": dst_p, "mtime": int(time())}}
        )

        # 4. Dispara Upload (Partial -> Final)
        if src_n.endswith(".partial") and not dst_n.endswith(".partial"):
            local_p = src_doc.get("local_path")
            if not is_uploadable_name(dst_n):
                await self._files.update_one(
                    {"_id": src_doc["_id"]},
                    {"$set": {"status": "completed"}, "$unset": {"local_path": 1}}
                )
                if local_p and os.path.exists(local_p):
                    try:
                        os.remove(local_p)
                    except OSError as exc:
                        logger.debug("ignored renamed file cleanup skipped (%s): %s", dst_n, exc)
                logger.debug("[RENAME] Ignorado sem upload: %s", dst_n)
                return

            if local_p and os.path.exists(local_p):
                await UPLOAD_QUEUE.put({
                    "path": local_p,
                    "filename": dst_n,
                    "parent": dst_p,
                    "size": src_doc.get("size", 0)
                })
                logger.info(f"📤 [RENAME] Enfileirado: {dst_n}")
            else:
                logger.warning(f"⚠️ [RENAME] Arquivo físico não encontrado: {dst_n}")
