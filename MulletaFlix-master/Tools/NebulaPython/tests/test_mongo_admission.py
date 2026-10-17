from __future__ import annotations

import asyncio
from types import SimpleNamespace

import pytest

from tools import mongo_admission


class FakeAdmissionCollection:
    def __init__(self):
        self.document = None

    async def update_one(self, query, update, upsert=False):
        if self.document is None and upsert:
            self.document = {"_id": query["_id"], "active_upload_leases": []}
        if self.document is None or self.document["_id"] != query["_id"]:
            return SimpleNamespace(matched_count=0)
        if "$setOnInsert" in update and not self.document:
            self.document.update(update["$setOnInsert"])
        if "$pull" in update:
            key, condition = next(iter(update["$pull"].items()))
            if key == "active_upload_leases":
                leases = self.document.setdefault(key, [])
                if "expires_at" in condition:
                    leases[:] = [lease for lease in leases if lease["expires_at"] > condition["expires_at"]["$lte"]]
                elif "lease_id" in condition:
                    leases[:] = [lease for lease in leases if lease["lease_id"] != condition["lease_id"]]
        if "$set" in update:
            self.document.update(update["$set"])
        return SimpleNamespace(matched_count=1)

    async def find_one_and_update(self, query, update, return_document=None):
        if self.document is None:
            return None
        now = query["$or"][1]["backup_expires_at"]["$lte"]
        backup_expiry = self.document.get("backup_expires_at")
        if backup_expiry is not None and (now is None or backup_expiry > now):
            return None
        lease = update["$push"]["active_upload_leases"]
        self.document.setdefault("active_upload_leases", []).append(lease)
        return self.document


@pytest.mark.asyncio
async def test_upload_admission_waits_for_backup_reservation_then_registers_lease(monkeypatch):
    monkeypatch.setattr(mongo_admission, "ADMISSION_POLL_SECONDS", 0.01)
    monkeypatch.setattr(mongo_admission, "ADMISSION_HEARTBEAT_SECONDS", 0.01)
    collection = FakeAdmissionCollection()
    await collection.update_one(
        {"_id": mongo_admission.ADMISSION_GATE_ID},
        {"$setOnInsert": {"_id": mongo_admission.ADMISSION_GATE_ID}},
        upsert=True,
    )
    collection.document["backup_owner"] = "backup-1"
    collection.document["backup_expires_at"] = 2**31

    waiting = asyncio.create_task(mongo_admission.acquire_upload_admission(collection, "worker-1"))
    await asyncio.sleep(0.03)
    assert not waiting.done()
    assert collection.document["active_upload_leases"] == []

    collection.document.pop("backup_owner")
    collection.document["backup_expires_at"] = 0
    lease = await asyncio.wait_for(waiting, timeout=1)
    assert lease.lease_id == collection.document["active_upload_leases"][0]["lease_id"]

    await lease.close()
    assert collection.document["active_upload_leases"] == []


@pytest.mark.asyncio
async def test_expired_upload_lease_is_removed_before_new_admission(monkeypatch):
    monkeypatch.setattr(mongo_admission, "ADMISSION_POLL_SECONDS", 0.01)
    monkeypatch.setattr(mongo_admission, "ADMISSION_HEARTBEAT_SECONDS", 0.01)
    collection = FakeAdmissionCollection()
    await collection.update_one(
        {"_id": mongo_admission.ADMISSION_GATE_ID},
        {"$setOnInsert": {"_id": mongo_admission.ADMISSION_GATE_ID}},
        upsert=True,
    )
    collection.document["active_upload_leases"] = [{"lease_id": "stale", "expires_at": 0}]

    lease = await mongo_admission.acquire_upload_admission(collection, "worker-2")
    try:
        assert [item["lease_id"] for item in collection.document["active_upload_leases"]] == [lease.lease_id]
    finally:
        await lease.close()
