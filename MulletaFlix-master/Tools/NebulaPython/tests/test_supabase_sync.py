from __future__ import annotations

import importlib
import motor.motor_asyncio
from datetime import datetime, timezone
from types import SimpleNamespace
from unittest.mock import AsyncMock, Mock

import pytest
from bson import ObjectId

supabase_sync = importlib.import_module("tools.supabase_sync")


class Cursor:
    def __init__(self, documents):
        self.documents = list(documents)

    def limit(self, count):
        self.documents = self.documents[:count]
        return self

    def __aiter__(self):
        self._iterator = iter(self.documents)
        return self

    async def __anext__(self):
        try:
            return next(self._iterator)
        except StopIteration:
            raise StopAsyncIteration


class MongoCollection:
    def __init__(self, documents=()):
        self.documents = list(documents)
        self.find_calls = []
        self.deleted_queries = []
        self.inserted = []
        self.updated = []
        self.indexes = []

    def find(self, query):
        self.find_calls.append(query)
        return Cursor(self.documents)

    async def count_documents(self, query):
        return len(self.documents)

    async def delete_many(self, query):
        self.deleted_queries.append(query)

    async def insert_many(self, documents):
        self.inserted.extend(documents)

    async def update_one(self, query, update, upsert=False):
        self.updated.append((query, update, upsert))

    async def create_index(self, keys, unique=False):
        self.indexes.append((keys, unique))


class MongoDatabase:
    def __init__(self, collections=None):
        self.collections = collections or {}

    def __getitem__(self, name):
        return self.collections.setdefault(name, MongoCollection())

    def __getattr__(self, name):
        if name.startswith("_"):
            raise AttributeError(name)
        return self[name]

    async def list_collection_names(self):
        return list(self.collections)


class MongoClient:
    def __init__(self, database=None):
        self.database = database or MongoDatabase()
        self.closed = False
        async def command(_name):
            return {"ok": 1}

        self.admin = SimpleNamespace(command=command)

    def __getitem__(self, _name):
        return self.database

    def close(self):
        self.closed = True


class SupabaseQuery:
    def __init__(self, result=None):
        self.result = result or SimpleNamespace(data=[])
        self.filters = []
        self.ordering = []
        self.inserted = None
        self.upserted = None
        self.limit_value = None

    def select(self, _columns):
        return self

    def insert(self, record):
        self.inserted = record
        return self

    def upsert(self, record, **options):
        self.upserted = (record, options)
        return self

    def eq(self, key, value):
        self.filters.append((key, value))
        return self

    def order(self, key, **options):
        self.ordering.append((key, options))
        return self

    def limit(self, value):
        self.limit_value = value
        return self

    def execute(self):
        return self.result


class Supabase:
    def __init__(self, results=None):
        self.results = results or {}
        self.queries = []

    def table(self, name):
        query = SupabaseQuery(self.results.get(name))
        self.queries.append((name, query))
        return query


def initialized_sync(mongo, supabase):
    sync = supabase_sync.SupabaseSync(mongo_uri="mongodb://test", mongo_db="ftp")
    sync._mongo_client = mongo
    sync._supabase = supabase
    sync._sync_enabled = True
    return sync


@pytest.mark.asyncio
async def test_backup_collection_serializes_bson_ids_and_datetimes_and_respects_query_limit():
    file_id = ObjectId()
    parent_id = ObjectId()
    created_at = datetime(2026, 10, 7, 12, 30, tzinfo=timezone.utc)
    mongo = MongoClient(MongoDatabase({"files": MongoCollection([
        {"_id": file_id, "parent": parent_id, "created_at": created_at, "name": "episode.mkv"},
    ])}))
    cloud = Supabase(results={"mongo_backups": SimpleNamespace(data=[{"id": "backup-1"}])})
    sync = initialized_sync(mongo, cloud)

    result = await sync.backup_collection("files", {"status": "completed"}, limit=1)

    assert result == {"success": True, "count": 1, "backup_id": "backup-1"}
    assert mongo.database["files"].find_calls == [{"status": "completed"}]
    record = cloud.queries[0][1].inserted
    assert record["collection"] == "files"
    assert record["document_count"] == 1
    assert record["data"][0] == {
        "_id": str(file_id),
        "parent": str(parent_id),
        "created_at": created_at.isoformat(),
        "name": "episode.mkv",
    }


@pytest.mark.asyncio
async def test_backup_collection_returns_failure_when_sync_is_disabled():
    sync = supabase_sync.SupabaseSync()

    assert await sync.backup_collection("files") == {
        "success": False,
        "error": "SupabaseSync not initialized",
    }


@pytest.mark.asyncio
async def test_backup_collection_redacts_no_error_and_returns_failure_when_cloud_insert_raises():
    sync = initialized_sync(MongoClient(), Supabase())
    sync._supabase.table = Mock(side_effect=RuntimeError("cloud unavailable"))

    result = await sync.backup_collection("files")

    assert result["success"] is False
    assert "cloud unavailable" in result["error"]


@pytest.mark.asyncio
async def test_backup_all_collections_discovers_names_and_applies_limit(monkeypatch):
    sync = initialized_sync(MongoClient(MongoDatabase({"files": MongoCollection(), "users": MongoCollection()})), Supabase())
    calls = []

    async def backup(collection, query=None, limit=10000):
        calls.append((collection, query, limit))
        return {"success": True, "count": 0}

    monkeypatch.setattr(sync, "backup_collection", backup)

    result = await sync.backup_all_collections(limit_per_collection=7)

    assert set(result) == {"files", "users"}
    assert calls == [("files", None, 7), ("users", None, 7)]


@pytest.mark.asyncio
@pytest.mark.parametrize("backup_id", [None, "selected-backup"])
async def test_restore_collection_restores_documents_and_only_clears_for_full_restore(backup_id):
    file_id = ObjectId()
    created_at = "2026-10-07T12:30:00+00:00"
    backup = {
        "data": [{"_id": str(file_id), "created_at": created_at, "name": "episode.mkv", "note": "not a date"}]
    }
    cloud = Supabase(results={"mongo_backups": SimpleNamespace(data=[backup])})
    mongo = MongoClient()
    sync = initialized_sync(mongo, cloud)

    result = await sync.restore_collection("files", backup_id=backup_id)

    collection = mongo.database["files"]
    assert result == {"success": True, "count": 1}
    assert collection.inserted[0]["_id"] == file_id
    assert collection.inserted[0]["created_at"] == datetime.fromisoformat(created_at)
    assert collection.inserted[0]["note"] == "not a date"
    assert collection.deleted_queries == ([] if backup_id else [{}])
    query = cloud.queries[0][1]
    assert ("id", backup_id) in query.filters if backup_id else query.limit_value == 1


@pytest.mark.asyncio
async def test_restore_collection_reports_missing_backup_without_touching_mongo():
    mongo = MongoClient()
    sync = initialized_sync(mongo, Supabase())

    result = await sync.restore_collection("users")

    assert result == {"success": False, "error": "No backup found"}
    assert mongo.database["users"].deleted_queries == []
    assert mongo.database["users"].inserted == []


@pytest.mark.asyncio
async def test_config_sync_uses_upsert_and_load_returns_config():
    config = {"VERSION": "12.1.0", "MONGODB": "private-uri"}
    cloud = Supabase(results={"app_config": SimpleNamespace(data=[{"id": "config-1", "config": config}])})
    sync = initialized_sync(MongoClient(), cloud)

    saved = await sync.sync_config_to_supabase(config)
    loaded = await sync.load_config_from_supabase()

    assert saved == {"success": True, "config_id": "config-1"}
    assert cloud.queries[0][1].upserted[0]["version"] == "12.1.0"
    assert cloud.queries[0][1].upserted[1] == {"on_conflict": "app_name"}
    assert loaded == config
    assert ("app_name", "mulletaflix") in cloud.queries[1][1].filters


@pytest.mark.asyncio
async def test_sync_config_and_load_config_handle_disabled_and_cloud_errors():
    disabled = supabase_sync.SupabaseSync()
    assert await disabled.sync_config_to_supabase({}) == {
        "success": False,
        "error": "SupabaseSync not initialized",
    }
    assert await disabled.load_config_from_supabase() is None

    sync = initialized_sync(MongoClient(), Supabase())
    sync._supabase.table = Mock(side_effect=RuntimeError("network error"))
    assert (await sync.sync_config_to_supabase({"VERSION": "test"}))["success"] is False
    assert await sync.load_config_from_supabase() is None


def test_http_helpers_build_authenticated_headers_and_return_empty_batch_without_network():
    headers = supabase_sync.get_supabase_headers("service-key")

    assert headers["apikey"] == "service-key"
    assert headers["Authorization"] == "Bearer service-key"
    assert headers["Prefer"] == "resolution=merge-duplicates,return=representation"
    assert supabase_sync.post_batch_to_supabase("https://example.test", "key", "files", []) == 0


def test_post_batch_serializes_bson_and_returns_count(monkeypatch):
    responses = []

    class Response:
        status = 201

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

    def open_request(request, timeout):
        responses.append((request, timeout))
        return Response()

    monkeypatch.setattr(supabase_sync.urllib.request, "urlopen", open_request)
    record = {"_id": ObjectId(), "created_at": datetime(2026, 10, 7, tzinfo=timezone.utc)}

    assert supabase_sync.post_batch_to_supabase("https://example.test/", "key", "nebula_files", [record]) == 1
    request, timeout = responses[0]
    assert request.full_url == "https://example.test/rest/v1/nebula_files"
    assert timeout == 30
    assert str(record["_id"]) in request.data.decode("utf-8")


def test_fetch_from_supabase_parses_response_and_constructs_paging_url(monkeypatch):
    seen = []

    class Response:
        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def read(self):
            return b'[{"login":"raphael"}]'

    def open_request(request, timeout):
        seen.append((request, timeout))
        return Response()

    monkeypatch.setattr(supabase_sync.urllib.request, "urlopen", open_request)

    result = supabase_sync.fetch_from_supabase("https://example.test/", "key", "nebula_users", limit=50, offset=100)

    assert result == [{"login": "raphael"}]
    assert seen[0][0].full_url == "https://example.test/rest/v1/nebula_users?select=*&limit=50&offset=100"
    assert seen[0][1] == 30


def test_post_batch_raises_with_cloud_http_error_details(monkeypatch):
    import urllib.error

    def fail_request(_request, timeout):
        raise urllib.error.HTTPError("https://example.test", 503, "offline", {}, None)

    monkeypatch.setattr(supabase_sync.urllib.request, "urlopen", fail_request)

    with pytest.raises(RuntimeError, match="PostgREST Error 503"):
        supabase_sync.post_batch_to_supabase("https://example.test", "key", "files", [{"id": "1"}])


@pytest.mark.asyncio
async def test_initialize_rejects_missing_credentials_without_opening_mongo(monkeypatch):
    sync = supabase_sync.SupabaseSync(supabase_url="", supabase_key="")
    mongo_factory = Mock(side_effect=AssertionError("Mongo must stay closed"))
    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", mongo_factory)

    assert await sync.initialize() is False
    assert sync._sync_enabled is False
    mongo_factory.assert_not_called()


@pytest.mark.asyncio
async def test_initialize_connects_to_mongo_and_supabase_and_close_releases_mongo(monkeypatch):
    mongo = MongoClient()
    cloud = Supabase()
    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: mongo)
    monkeypatch.setattr(supabase_sync, "create_client", lambda *_args: cloud)
    monkeypatch.setattr(supabase_sync, "SUPABASE_AVAILABLE", True)
    sync = supabase_sync.SupabaseSync(
        mongo_uri="mongodb://test", supabase_url="https://example.test", supabase_key="key"
    )

    assert await sync.initialize() is True
    assert sync._sync_enabled is True
    assert cloud.queries[0][0] == "mongo_backups"
    await sync.close()
    assert mongo.closed is True


@pytest.mark.asyncio
async def test_initialize_reports_mongo_failure_and_disables_sync(monkeypatch):
    mongo = MongoClient()

    async def fail_ping(_name):
        raise ConnectionError("mongo unavailable")

    mongo.admin.command = fail_ping
    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: mongo)
    monkeypatch.setattr(supabase_sync, "SUPABASE_AVAILABLE", True)
    sync = supabase_sync.SupabaseSync(supabase_url="https://example.test", supabase_key="key")

    assert await sync.initialize() is False
    assert sync._sync_enabled is False
    assert mongo.closed is True


@pytest.mark.asyncio
async def test_initialize_reports_supabase_failure_and_closes_mongo(monkeypatch):
    mongo = MongoClient()

    def fail_supabase(*_args):
        raise ConnectionError("supabase unavailable")

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: mongo)
    monkeypatch.setattr(supabase_sync, "create_client", fail_supabase)
    monkeypatch.setattr(supabase_sync, "SUPABASE_AVAILABLE", True)
    sync = supabase_sync.SupabaseSync(supabase_url="https://example.test", supabase_key="key")

    assert await sync.initialize() is False
    assert sync._sync_enabled is False
    assert sync._mongo_client is None
    assert mongo.closed is True


@pytest.mark.asyncio
async def test_scheduled_backup_forwards_collection_selection_and_results():
    sync = supabase_sync.SupabaseSync()
    expected = {"users": {"success": True, "count": 2}}
    backup_all = AsyncMock(return_value=expected)
    sync.backup_all_collections = backup_all

    result = await sync.run_scheduled_backup(["users"], interval_hours=6)

    assert result == expected
    backup_all.assert_awaited_once_with(["users"])


@pytest.mark.asyncio
async def test_one_shot_backup_closes_sync_after_backup_exception(monkeypatch):
    mongo = MongoClient()
    sync = initialized_sync(mongo, Supabase())
    initialize = AsyncMock(return_value=True)
    backup = AsyncMock(side_effect=RuntimeError("backup failed"))
    monkeypatch.setattr(supabase_sync, "SupabaseSync", lambda *_args: sync)
    sync.initialize = initialize
    sync.backup_all_collections = backup

    with pytest.raises(RuntimeError, match="backup failed"):
        await supabase_sync.backup_mongodb_to_supabase(collections=["users"])

    assert mongo.closed is True


@pytest.mark.asyncio
async def test_legacy_mongo_backup_serializes_files_and_users_and_closes_client(monkeypatch):
    file_id = ObjectId()
    user_id = ObjectId()
    files = MongoCollection([{"_id": file_id, "name": "episode.mkv", "size": 12, "parent": ObjectId()}])
    users = MongoCollection([{"_id": user_id, "login": "viewer", "password": "hash", "permissions": ["read"]}])
    database = MongoDatabase({"files": files, "users": users})
    client = MongoClient(database)
    batches = []

    def save_batch(_url, _key, table, records):
        batches.append((table, records))
        return len(records)

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "post_batch_to_supabase", save_batch)

    result = await supabase_sync.backup_mongo_to_supabase(
        "mongodb://test", "ftp", "https://example.test", "service-key"
    )

    assert result["success"] is True
    assert result["files"] == 1 and result["users"] == 1
    assert batches[0][0] == "nebula_files"
    assert batches[0][1][0]["id"] == str(file_id)
    assert batches[0][1][0]["doc_data"]["_id"] == str(file_id)
    assert batches[1][0] == "nebula_users"
    assert batches[1][1][0]["password_hash"] == "hash"
    assert batches[2][0] == "nebula_backups"
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_backup_records_failed_status_and_raises_when_a_file_batch_fails(monkeypatch):
    files = MongoCollection([{"_id": index, "name": f"file-{index}.mkv"} for index in range(101)])
    database = MongoDatabase({"files": files, "users": MongoCollection()})
    client = MongoClient(database)
    history = []
    file_calls = 0

    def save_batch(_url, _key, table, records):
        nonlocal file_calls
        if table == "nebula_files":
            file_calls += 1
            if file_calls == 1:
                raise OSError("private endpoint/token must not be logged")
        if table == "nebula_backups":
            history.extend(records)

    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "post_batch_to_supabase", save_batch)

    with pytest.raises(RuntimeError, match="Backup incompleto: files batch"):
        await supabase_sync.backup_mongo_to_supabase(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    assert history[0]["status"] == "failed"
    assert history[0]["total_files"] == 1
    assert "1/101 arquivos" in history[0]["details"]
    assert "private endpoint" not in history[0]["details"]
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_backup_records_failed_status_and_raises_when_user_batch_fails(monkeypatch):
    users = MongoCollection([{"_id": ObjectId(), "login": "viewer", "password": "hash"}])
    database = MongoDatabase({"files": MongoCollection(), "users": users})
    client = MongoClient(database)
    history = []

    def save_batch(_url, _key, table, records):
        if table == "nebula_users":
            raise OSError("upload unavailable")
        if table == "nebula_backups":
            history.extend(records)

    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "post_batch_to_supabase", save_batch)

    with pytest.raises(RuntimeError, match="Backup incompleto: users batch"):
        await supabase_sync.backup_mongo_to_supabase(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    assert history[0]["status"] == "failed"
    assert history[0]["total_users"] == 0
    assert "0/1 usuários" in history[0]["details"]
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_backup_fails_closed_when_history_cannot_be_recorded(monkeypatch):
    database = MongoDatabase({"files": MongoCollection(), "users": MongoCollection()})
    client = MongoClient(database)

    def save_batch(_url, _key, table, _records):
        if table == "nebula_backups":
            raise OSError("history store unavailable")

    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "post_batch_to_supabase", save_batch)

    with pytest.raises(RuntimeError, match="Backup não confirmado: falha ao registrar histórico"):
        await supabase_sync.backup_mongo_to_supabase(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_restore_pages_files_and_recreates_indexes(monkeypatch):
    file_id = ObjectId()
    user_id = ObjectId()
    database = MongoDatabase()
    client = MongoClient(database)
    pages = {
        "nebula_users": [{"login": "viewer", "doc_data": {"_id": str(user_id), "login": "viewer"}}],
        "nebula_files": [{"id": str(file_id), "doc_data": {"_id": str(file_id), "name": "episode.mkv"}}],
    }
    requests = []

    def fetch(_url, _key, table, limit, offset=0):
        requests.append((table, limit, offset))
        return pages[table] if offset == 0 else []

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "fetch_from_supabase", fetch)

    result = await supabase_sync.restore_supabase_to_mongo(
        "mongodb://test", "ftp", "https://example.test", "service-key"
    )

    assert result["users"] == 1 and result["files"] == 1
    assert database.users.updated[0][0] == {"login": "viewer"}
    assert database.files.updated[0][0] == {"_id": file_id}
    assert requests == [("nebula_users", 500, 0), ("nebula_files", 500, 0)]
    assert len(database.files.indexes) == 3
    assert len(database.users.indexes) == 1
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_restore_reports_partial_file_restore_when_later_page_fails(monkeypatch):
    database = MongoDatabase()
    client = MongoClient(database)
    first_page = [
        {"id": str(ObjectId()), "doc_data": {"name": f"episode-{index}.mkv"}}
        for index in range(500)
    ]
    requests = []

    def fetch(_url, _key, table, limit, offset=0):
        requests.append((table, limit, offset))
        if table == "nebula_users":
            return []
        if offset == 0:
            return first_page
        raise OSError("simulated Supabase page failure")

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "fetch_from_supabase", fetch)

    with pytest.raises(RuntimeError, match=r"offset 500.*500 arquivos"):
        await supabase_sync.restore_supabase_to_mongo(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    assert requests == [
        ("nebula_users", 500, 0),
        ("nebula_files", 500, 0),
        ("nebula_files", 500, 500),
    ]
    assert len(database.files.updated) == 500
    assert len(database.files.indexes) == 3
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_restore_reports_user_fetch_failure(monkeypatch, capsys):
    database = MongoDatabase()
    client = MongoClient(database)

    def fetch(_url, _key, table, limit, offset=0):
        if table == "nebula_users":
            raise OSError("simulated user table failure")
        assert table == "nebula_files" and offset == 0
        return []

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "fetch_from_supabase", fetch)

    with pytest.raises(RuntimeError, match="usuários"):
        await supabase_sync.restore_supabase_to_mongo(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    output = capsys.readouterr().out
    assert "RESTAURAÇÃO INCOMPLETA" in output
    assert "RESTAURAÇÃO CONCLUÍDA" not in output
    assert len(database.files.indexes) == 3
    assert len(database.users.indexes) == 1
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_restore_preserves_partial_user_count_when_update_fails(monkeypatch, capsys):
    database = MongoDatabase()
    client = MongoClient(database)
    users = [
        {"login": "viewer-1", "doc_data": {"login": "viewer-1"}},
        {"login": "viewer-2", "doc_data": {"login": "viewer-2"}},
    ]
    update_count = 0
    original_update = database.users.update_one

    async def update_one(*args, **kwargs):
        nonlocal update_count
        update_count += 1
        if update_count == 2:
            raise OSError("simulated Mongo user write failure")
        await original_update(*args, **kwargs)

    database.users.update_one = update_one

    def fetch(_url, _key, table, limit, offset=0):
        if table == "nebula_users":
            return users
        assert table == "nebula_files" and offset == 0
        return []

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "fetch_from_supabase", fetch)

    with pytest.raises(RuntimeError, match="usuários"):
        await supabase_sync.restore_supabase_to_mongo(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    output = capsys.readouterr().out
    assert "Usuários restaurados: 1" in output
    assert "Usuários restaurados: 0" not in output
    assert len(database.users.updated) == 1
    assert client.closed is True


@pytest.mark.asyncio
async def test_legacy_restore_reports_index_creation_failure(monkeypatch):
    database = MongoDatabase()
    database.files.create_index = AsyncMock(side_effect=RuntimeError("index creation failed"))
    client = MongoClient(database)

    def fetch(_url, _key, _table, limit, offset=0):
        return []

    monkeypatch.setattr(supabase_sync, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(motor.motor_asyncio, "AsyncIOMotorClient", lambda *_args: client)
    monkeypatch.setattr(supabase_sync, "fetch_from_supabase", fetch)

    with pytest.raises(RuntimeError, match="índices"):
        await supabase_sync.restore_supabase_to_mongo(
            "mongodb://test", "ftp", "https://example.test", "service-key"
        )

    assert client.closed is True


def test_connection_check_treats_authenticated_404_as_reachable(monkeypatch):
    import urllib.error

    def not_found(_request, timeout):
        raise urllib.error.HTTPError("https://example.test/rest/v1/", 404, "Not Found", {}, None)

    monkeypatch.setattr(supabase_sync.urllib.request, "urlopen", not_found)

    assert supabase_sync.test_supabase_connection("https://example.test/", "key") is True


def test_connection_check_rejects_missing_credentials_without_network():
    assert supabase_sync.test_supabase_connection("", "key") is False
    assert supabase_sync.test_supabase_connection("https://example.test", "") is False
