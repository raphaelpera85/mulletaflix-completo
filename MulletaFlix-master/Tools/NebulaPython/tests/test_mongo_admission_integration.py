from __future__ import annotations

import asyncio
import contextlib
import os
import time
from urllib.parse import urlsplit
from uuid import uuid4

import pytest
from pymongo import AsyncMongoClient

from tools import mongo_admission


MONGODB_URI = os.getenv("MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING")


def _validate_loopback_uri(uri: str) -> None:
    parsed_uri = urlsplit(uri)
    test_hosts = parsed_uri.netloc.rsplit("@", 1)[-1].split(",")
    if parsed_uri.scheme != "mongodb" or not test_hosts or any(
        host.rsplit(":", 1)[0].strip("[]").lower() not in {"localhost", "127.0.0.1", "::1"}
        for host in test_hosts
    ):
        raise ValueError("real Mongo integration test only accepts loopback hosts")


def test_integration_uri_accepts_loopback_and_rejects_remote_mongo():
    _validate_loopback_uri("mongodb://127.0.0.1:27099/?directConnection=true")
    for uri in (
        "mongodb://production.example:27017",
        "mongodb://127.0.0.1:27017,mongo.example:27017",
        "mongodb+srv://cluster.example",
    ):
        with pytest.raises(ValueError, match="only accepts loopback"):
            _validate_loopback_uri(uri)


@pytest.mark.asyncio
@pytest.mark.skipif(not MONGODB_URI, reason="requires a disposable MongoDB service")
async def test_upload_admission_waits_for_backup_and_releases_real_mongo_lease(monkeypatch):
    monkeypatch.setattr(mongo_admission, "ADMISSION_POLL_SECONDS", 0.01)
    _validate_loopback_uri(MONGODB_URI)
    client = AsyncMongoClient(MONGODB_URI, serverSelectionTimeoutMS=5000, connectTimeoutMS=5000)
    database = client[f"nebula_admission_{uuid4().hex}"]
    collection = database["gates"]
    waiting = None
    lease = None
    connected = False

    try:
        await client.admin.command("ping")
        connected = True
        await collection.insert_one({
            "_id": mongo_admission.ADMISSION_GATE_ID,
            "backup_owner": "ci",
            "backup_expires_at": int(time.time()) + 30,
            "active_upload_leases": [],
        })

        waiting = asyncio.create_task(mongo_admission.acquire_upload_admission(collection, "ci-worker"))
        await asyncio.sleep(0.05)
        assert not waiting.done(), "upload must wait while a live backup reservation exists"

        await collection.update_one(
            {"_id": mongo_admission.ADMISSION_GATE_ID},
            {"$unset": {"backup_owner": "", "backup_expires_at": ""}},
        )
        lease = await asyncio.wait_for(waiting, timeout=5)
        document = await collection.find_one({"_id": mongo_admission.ADMISSION_GATE_ID})
        assert [entry["lease_id"] for entry in document["active_upload_leases"]] == [lease.lease_id]

        await lease.close()
        lease = None
        document = await collection.find_one({"_id": mongo_admission.ADMISSION_GATE_ID})
        assert document["active_upload_leases"] == []
    finally:
        if waiting is not None and not waiting.done():
            waiting.cancel()
            await asyncio.gather(waiting, return_exceptions=True)
        if lease is not None:
            with contextlib.suppress(Exception):
                await lease.close()
        if connected:
            with contextlib.suppress(Exception):
                await client.drop_database(database.name)
        await client.close()
