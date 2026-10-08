from __future__ import annotations

import asyncio

import pytest
from aiohttp.test_utils import TestClient, TestServer

from control_plane import ControlPlane


class Mongo:
    def __init__(self):
        self.fail_ping = False
        self.files = self

    async def command(self, name):
        assert name == "ping"
        if self.fail_ping:
            raise ConnectionError("private connection details")
        return {"ok": 1}

    async def count_documents(self, query):
        return {
            "queued": 2,
            "staging": 3,
            "uploading": 4,
            "completed": 5,
            "failed": 6,
        }[query["status"]]


def make_control_plane(*, status_provider=None):
    async def drain():
        return None

    return ControlPlane(
        token="t" * 40,
        mongo=Mongo(),
        upload_queue=asyncio.Queue(),
        drain_callback=drain,
        status_provider=status_provider or (lambda: {"worker": "running"}),
    )


@pytest.mark.asyncio
async def test_control_api_requires_bearer_and_disables_caching():
    control = make_control_plane()
    async with TestClient(TestServer(control._app)) as client:
        unauthorized = await client.get("/v1/health")
        assert unauthorized.status == 401
        assert unauthorized.headers["Cache-Control"] == "no-store"
        assert unauthorized.headers["WWW-Authenticate"] == "Bearer"
        assert await unauthorized.json() == {"error": "unauthorized"}

        authorized = await client.get(
            "/v1/health", headers={"Authorization": f"Bearer {'t' * 40}"}
        )
        assert authorized.status == 200
        assert authorized.headers["Cache-Control"] == "no-store"
        assert (await authorized.json())["status"] == "alive"


@pytest.mark.asyncio
async def test_readiness_tracks_initialization_and_database_failure_without_leaking_error():
    control = make_control_plane()
    control.set_ready(True)
    headers = {"Authorization": f"Bearer {'t' * 40}"}
    async with TestClient(TestServer(control._app)) as client:
        ready = await client.get("/v1/readiness", headers=headers)
        assert ready.status == 200
        assert await ready.json() == {
            "api": "v1",
            "ready": True,
            "draining": False,
            "database_ready": True,
        }

        control._mongo.fail_ping = True
        unavailable = await client.get("/v1/readiness", headers=headers)
        assert unavailable.status == 503
        assert await unavailable.json() == {
            "api": "v1",
            "ready": False,
            "draining": False,
            "database_ready": False,
        }
        assert b"private connection details" not in await unavailable.read()


@pytest.mark.asyncio
async def test_control_status_supports_async_status_provider():
    async def status_provider():
        return {"worker": "running"}

    control = make_control_plane(status_provider=status_provider)
    headers = {"Authorization": f"Bearer {'t' * 40}"}
    async with TestClient(TestServer(control._app)) as client:
        response = await client.get("/v1/status", headers=headers)
        body = await response.json()

    assert response.status == 200
    assert body == {
        "api": "v1",
        "ready": False,
        "draining": False,
        "worker": "running",
    }


@pytest.mark.asyncio
async def test_control_queue_reports_persisted_and_in_memory_counts():
    control = make_control_plane()
    control._upload_queue.put_nowait({"name": "one"})
    headers = {"Authorization": f"Bearer {'t' * 40}"}
    async with TestClient(TestServer(control._app)) as client:
        response = await client.get("/v1/queue", headers=headers)
        body = await response.json()

    assert response.status == 200
    assert body == {
        "api": "v1",
        "queued": 2,
        "staging": 3,
        "uploading": 4,
        "completed": 5,
        "failed": 6,
        "pending": 9,
        "in_memory": 1,
    }


@pytest.mark.asyncio
async def test_control_api_maps_conflicts_bad_payloads_and_internal_errors():
    async def broken_status_provider():
        raise RuntimeError("private traceback")

    control = make_control_plane(status_provider=broken_status_provider)
    headers = {"Authorization": f"Bearer {'t' * 40}"}
    async with TestClient(TestServer(control._app)) as client:
        conflict = await client.get("/v1/feeder/status", headers=headers)
        assert conflict.status == 409
        assert await conflict.json() == {"error": "feeder is not configured"}

        bad_payload = await client.post(
            "/v1/strm/generate", headers=headers, json=["not", "an", "object"]
        )
        assert bad_payload.status == 400
        assert await bad_payload.json() == {"error": "request body must be an object"}

        internal_error = await client.get("/v1/status", headers=headers)
        assert internal_error.status == 500
        assert await internal_error.json() == {"error": "internal_error"}
        assert b"private traceback" not in await internal_error.read()
