"""Shared Mongo-backed admission leases for Nebula uploads and Supabase sync."""

from __future__ import annotations

import asyncio
import logging
import time
import uuid
from typing import Any

from pymongo import ReturnDocument
from pymongo.errors import DuplicateKeyError

ADMISSION_GATE_ID = "supabase_sync_admission"
ADMISSION_LEASE_SECONDS = 5 * 60
ADMISSION_HEARTBEAT_SECONDS = 30
ADMISSION_POLL_SECONDS = 1


class MongoAdmissionLease:
    """Renewable upload lease; expired leases are recoverable after worker death."""

    def __init__(self, collection: Any, lease_id: str, logger: logging.Logger | None = None):
        self._collection = collection
        self.lease_id = lease_id
        self._logger = logger or logging.getLogger(__name__)
        self._lost = asyncio.Event()
        self._closed = False
        self._heartbeat_task = asyncio.create_task(self._heartbeat())

    @property
    def lost(self) -> bool:
        return self._lost.is_set()

    def ensure_active(self) -> None:
        if self.lost:
            raise RuntimeError("Nebula upload admission lease was lost; aborting Telegram upload.")

    async def close(self) -> None:
        if self._closed:
            return
        self._closed = True
        self._heartbeat_task.cancel()
        try:
            await self._heartbeat_task
        except asyncio.CancelledError:
            pass
        await self._collection.update_one(
            {"_id": ADMISSION_GATE_ID},
            {"$pull": {"active_upload_leases": {"lease_id": self.lease_id}}},
        )

    async def __aenter__(self) -> MongoAdmissionLease:
        return self

    async def __aexit__(self, _exc_type, _exc, _traceback) -> None:
        await self.close()

    async def _heartbeat(self) -> None:
        try:
            while True:
                await asyncio.sleep(ADMISSION_HEARTBEAT_SECONDS)
                now = int(time.time())
                result = await self._collection.update_one(
                    {
                        "_id": ADMISSION_GATE_ID,
                        "active_upload_leases.lease_id": self.lease_id,
                    },
                    {
                        "$set": {
                            "active_upload_leases.$.expires_at": now + ADMISSION_LEASE_SECONDS,
                        }
                    },
                )
                if result.matched_count != 1:
                    self._lost.set()
                    self._logger.error("[NEBULA-ADMISSION] Lease de upload perdido; worker vai abortar o envio.")
                    return
        except asyncio.CancelledError:
            raise
        except Exception:
            self._lost.set()
            self._logger.exception("[NEBULA-ADMISSION] Falha ao renovar lease de upload.")


async def acquire_upload_admission(collection: Any, owner: str, logger: logging.Logger | None = None) -> MongoAdmissionLease:
    """Wait for backup admission to open, then atomically register an active upload."""
    log = logger or logging.getLogger(__name__)
    try:
        await collection.update_one(
            {"_id": ADMISSION_GATE_ID},
            {"$setOnInsert": {"_id": ADMISSION_GATE_ID}},
            upsert=True,
        )
    except DuplicateKeyError:
        # C# and multiple Python workers may initialize the singleton concurrently.
        pass

    lease_id = uuid.uuid4().hex
    last_wait_log = 0.0
    while True:
        now = int(time.time())
        await collection.update_one(
            {"_id": ADMISSION_GATE_ID},
            {"$pull": {"active_upload_leases": {"expires_at": {"$lte": now}}}},
        )
        document = await collection.find_one_and_update(
            {
                "_id": ADMISSION_GATE_ID,
                "$or": [
                    {"backup_expires_at": {"$exists": False}},
                    {"backup_expires_at": {"$lte": now}},
                ],
            },
            {
                "$push": {
                    "active_upload_leases": {
                        "lease_id": lease_id,
                        "owner": owner,
                        "expires_at": now + ADMISSION_LEASE_SECONDS,
                    }
                }
            },
            return_document=ReturnDocument.AFTER,
        )
        if document is not None:
            return MongoAdmissionLease(collection, lease_id, log)

        if time.monotonic() - last_wait_log >= 30:
            log.info("[NEBULA-ADMISSION] Backup Supabase reservou a fila; upload aguardando liberação.")
            last_wait_log = time.monotonic()
        await asyncio.sleep(ADMISSION_POLL_SECONDS)
