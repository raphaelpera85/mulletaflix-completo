from __future__ import annotations

import asyncio
from copy import deepcopy

import pytest
from aiohttp.test_utils import TestClient, TestServer

from control_plane import ControlPlane


class Cursor:
    def __init__(self, rows):
        self.rows = list(rows)

    def __aiter__(self):
        self._iterator = iter(self.rows)
        return self

    async def __anext__(self):
        try:
            return deepcopy(next(self._iterator))
        except StopIteration:
            raise StopAsyncIteration


class Users:
    def __init__(self, rows):
        self.rows = {row["login"]: deepcopy(row) for row in rows}
        self.updates = []
        self.deleted = []

    def find(self, query, projection=None):
        if "$nin" in query.get("login", {}):
            excluded = set(query["login"]["$nin"])
            rows = [row for login, row in self.rows.items() if login not in excluded]
        else:
            rows = list(self.rows.values())
        if projection:
            rows = [
                {key: value for key, value in row.items() if key in projection and projection[key]}
                for row in rows
            ]
        return Cursor(rows)

    async def find_one(self, query):
        row = self.rows.get(query["login"])
        return deepcopy(row) if row else None

    async def update_one(self, query, update, upsert=False):
        login = query["login"]
        current = self.rows.setdefault(login, {"login": login})
        current.update(update.get("$set", {}))
        for key in update.get("$unset", {}):
            current.pop(key, None)
        self.updates.append((query, deepcopy(update), upsert))

    async def delete_many(self, query):
        logins = query["login"]["$in"]
        self.deleted.extend(logins)
        for login in logins:
            self.rows.pop(login, None)


class Mongo:
    def __init__(self, users):
        self.users = users

    async def command(self, _name):
        return {"ok": 1}


def make_client(users, disconnect):
    async def drain():
        return None

    control = ControlPlane(
        token="t" * 40,
        mongo=Mongo(users),
        upload_queue=asyncio.Queue(),
        drain_callback=drain,
        status_provider=lambda: {},
        disconnect_user=disconnect,
    )
    return TestClient(TestServer(control._app))


@pytest.mark.asyncio
async def test_users_list_sorts_and_only_returns_login_and_permissions():
    users = Users([
        {"login": "zeta", "permissions": [{"path": "/Films"}], "password": "secret"},
        {"login": "Alpha", "password_hash": "private"},
    ])
    client = make_client(users, None)
    async with client:
        response = await client.get("/v1/users", headers={"Authorization": f"Bearer {'t' * 40}"})
        result = await response.json()

    assert response.status == 200
    assert result == {
        "api": "v1",
        "users": [
            {"login": "Alpha", "permissions": []},
            {"login": "zeta", "permissions": [{"path": "/Films"}]},
        ],
    }


@pytest.mark.asyncio
async def test_users_sync_creates_updates_deletes_and_disconnects_changed_users():
    users = Users([
        {"login": "existing", "password": "legacy", "permissions": [{"path": "/Old"}]},
        {"login": "remove-me", "permissions": []},
    ])
    disconnected = []

    async def disconnect(login):
        disconnected.append(login)

    client = make_client(users, disconnect)
    payload = {
        "users": [
            {"login": "new_user", "password": "new-secret", "permissions": [{"path": "/Series", "writable": True}]},
            {"login": "existing", "permissions": [{"path": "/Films", "readable": True}]},
        ],
        "replaceAll": True,
    }
    async with client:
        response = await client.post(
            "/v1/users/sync",
            headers={"Authorization": f"Bearer {'t' * 40}"},
            json=payload,
        )
        result = await response.json()

    assert response.status == 200
    assert result == {"api": "v1", "created": 1, "updated": 1, "deleted": 1}
    assert set(users.rows) == {"new_user", "existing"}
    assert users.rows["new_user"]["permissions"] == [
        {"path": "/Series", "readable": True, "writable": True}
    ]
    assert users.rows["new_user"]["password_hash"] != "new-secret"
    assert "password" not in users.rows["new_user"]
    assert users.rows["existing"]["password"] == "legacy"
    assert disconnected == ["new_user", "existing", "remove-me"]


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "payload",
    [
        {"users": "not-an-array"},
        {"users": [{"login": "bad login", "password": "pw"}]},
        {"users": [{"login": "duplicate", "password": "pw"}, {"login": "duplicate", "password": "pw"}]},
        {"users": [{"login": "missing-password"}]},
        {"users": [{"login": "bad-permissions", "password": "pw", "permissions": "all"}]},
    ],
)
async def test_users_sync_rejects_invalid_payload_without_creating_user(payload):
    users = Users([])
    client = make_client(users, None)
    async with client:
        response = await client.post(
            "/v1/users/sync",
            headers={"Authorization": f"Bearer {'t' * 40}"},
            json=payload,
        )
        result = await response.json()

    assert response.status == 400
    assert users.rows == {}
    assert result["error"]
