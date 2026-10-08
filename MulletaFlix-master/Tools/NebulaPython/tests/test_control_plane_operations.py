from __future__ import annotations

import asyncio
import importlib
import sys
from types import SimpleNamespace

import pytest
from bson import ObjectId
from aiohttp.test_utils import TestClient, TestServer

from control_plane import ConflictError, ControlPlane, FeederSupervisor
from ftp.pathio import MongoDBPathIO

control_module = importlib.import_module("control_plane")
TOKEN = "c" * 40
AUTH = {"Authorization": f"Bearer {TOKEN}"}


def make_control(*, source_roots=(), output_roots=(), feeder=None, drain=None):
    async def default_drain():
        return None

    return ControlPlane(
        token=TOKEN,
        mongo=object(),
        upload_queue=asyncio.Queue(),
        drain_callback=drain or default_drain,
        status_provider=lambda: {},
        source_roots=source_roots,
        output_roots=output_roots,
        feeder=feeder,
    )


@pytest.mark.asyncio
async def test_cache_status_and_clear_report_count_and_empty_state():
    async with MongoDBPathIO._cache_lock:
        MongoDBPathIO._memory_cache.clear()
        MongoDBPathIO._memory_cache["one"] = {"name": "one"}
        MongoDBPathIO._memory_cache["two"] = {"name": "two"}
    control = make_control()
    try:
        async with TestClient(TestServer(control._app)) as client:
            before = await client.get("/v1/cache", headers=AUTH)
            clear = await client.post("/v1/cache/clear", headers=AUTH)
            after = await client.get("/v1/cache", headers=AUTH)
            assert (await before.json())["entries"] == 2
            assert await clear.json() == {"api": "v1", "removed": 2}
            assert (await after.json())["entries"] == 0
    finally:
        async with MongoDBPathIO._cache_lock:
            MongoDBPathIO._memory_cache.clear()


@pytest.mark.asyncio
async def test_prune_preview_requires_allowlisted_paths_and_apply_is_single_use(tmp_path, monkeypatch):
    source, output, outside = tmp_path / "source", tmp_path / "output", tmp_path / "outside"
    source.mkdir()
    output.mkdir()
    outside.mkdir()
    calls = []

    control = make_control(source_roots=(source,), output_roots=(output,))
    async def fake_run_prune(config, apply):
        calls.append((config, apply))
        return {"scanned": 3, "removed": int(apply)}

    monkeypatch.setattr(control, "_run_prune", fake_run_prune)
    async with TestClient(TestServer(control._app)) as client:
        denied = await client.post(
            "/v1/prune/preview", headers=AUTH,
            json={"sources": [str(source)], "destination": str(outside)},
        )
        preview = await client.post(
            "/v1/prune/preview", headers=AUTH,
            json={"sources": [str(source)], "destination": str(output), "excludeDirectories": ["Temp"]},
        )
        body = await preview.json()
        applied = await client.post("/v1/prune/apply", headers=AUTH, json={"previewId": body["previewId"]})
        replay = await client.post("/v1/prune/apply", headers=AUTH, json={"previewId": body["previewId"]})
        applied_body = await applied.json()
        replay_body = await replay.json()

    assert denied.status == 400
    assert preview.status == 200 and body["scanned"] == 3 and body["removed"] == 0
    assert applied.status == 200 and applied_body["removed"] == 1
    assert replay.status == 400
    assert replay_body["error"] == "previewId is invalid or expired"
    assert len(calls) == 2
    assert calls[0][1] is False and calls[1][1] is True
    assert calls[0][0]["exclude"] == {"temp"}


@pytest.mark.asyncio
async def test_prune_apply_rejects_expired_preview(tmp_path, monkeypatch):
    source = tmp_path / "source"
    source.mkdir()

    control = make_control(source_roots=(source,), output_roots=(source,))
    async def fake_run_prune(_config, _apply):
        return {"scanned": 0, "removed": 0}

    monkeypatch.setattr(control, "_run_prune", fake_run_prune)
    async with TestClient(TestServer(control._app)) as client:
        response = await client.post("/v1/prune/preview", headers=AUTH, json={"sources": [str(source)]})
        preview_id = (await response.json())["previewId"]
        control._previews[preview_id]["expires"] = 0
        expired = await client.post("/v1/prune/apply", headers=AUTH, json={"previewId": preview_id})
        expired_body = await expired.json()
    assert expired.status == 400
    assert expired_body == {"error": "previewId is invalid or expired"}


@pytest.mark.asyncio
async def test_drain_stop_is_idempotent_and_marks_readiness_false():
    called = []

    async def drain():
        called.append("drained")

    control = make_control(drain=drain)
    control.set_ready(True)
    async with TestClient(TestServer(control._app)) as client:
        first = await client.post("/v1/drain-stop", headers=AUTH)
        second = await client.post("/v1/drain-stop", headers=AUTH)
        first_body = await first.json()
        second_body = await second.json()
        await control._drain_task
        readiness = await client.get("/v1/readiness", headers=AUTH)
        readiness_body = await readiness.json()
    assert first.status == second.status == 202
    assert first_body == second_body == {"api": "v1", "accepted": True, "draining": True}
    assert control.draining
    assert readiness_body == {
        "api": "v1", "ready": False, "draining": True, "database_ready": False
    }
    assert called == ["drained"]


@pytest.mark.asyncio
async def test_feeder_http_routes_surface_status_and_start_stop_results():
    class Feeder:
        async def start(self, config):
            return {"running": True, "config": config}

        async def stop(self):
            return {"running": False, "exitCode": 0}

        def status(self):
            return {"running": True, "pid": 123}

    control = make_control(feeder=Feeder())
    async with TestClient(TestServer(control._app)) as client:
        status = await client.get("/v1/feeder/status", headers=AUTH)
        started = await client.post("/v1/feeder/start", headers=AUTH, json={"workers": 2})
        stopped = await client.post("/v1/feeder/stop", headers=AUTH)
        status_body = await status.json()
        start_body = await started.json()
        stop_body = await stopped.json()
    assert status_body == {"api": "v1", "running": True, "pid": 123}
    assert started.status == 202
    assert start_body["config"] == {"workers": 2}
    assert stop_body == {"api": "v1", "running": False, "exitCode": 0}


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("payload", "error"),
    [
        ({"outputRoot": "", "streamBaseUrl": "https://stream.example", "libraryUser": "raphael"}, "outside configured roots"),
        ({"outputRoot": "{output}", "streamBaseUrl": "ftp://stream.example", "libraryUser": "raphael"}, "HTTP(S) URL"),
        ({"outputRoot": "{output}", "streamBaseUrl": "https://user:pw@stream.example", "libraryUser": "raphael"}, "without credentials"),
        ({"outputRoot": "{output}", "streamBaseUrl": "https://stream.example", "libraryUser": "bad user"}, "invalid libraryUser"),
    ],
)
async def test_strm_generation_rejects_unsafe_or_invalid_configuration_before_import(tmp_path, payload, error):
    output = tmp_path / "output"
    output.mkdir()
    resolved = {
        **payload,
        "outputRoot": payload["outputRoot"].replace("{output}", str(output)),
    }
    control = make_control(output_roots=(output,))
    async with TestClient(TestServer(control._app)) as client:
        initial = await client.get("/v1/strm/status", headers=AUTH)
        initial_body = await initial.json()
        response = await client.post("/v1/strm/generate", headers=AUTH, json=resolved)
        body = await response.json()
        status = await client.get("/v1/strm/status", headers=AUTH)
        status_body = await status.json()
    assert initial_body == {"api": "v1", "running": False, "lastResult": None}
    assert response.status == 400
    assert error in body["error"]
    assert status_body == {"api": "v1", "running": False, "lastResult": None}


@pytest.mark.asyncio
async def test_strm_generation_api_delegates_and_persists_result_when_generator_is_available(tmp_path, monkeypatch):
    output = tmp_path / "output"
    output.mkdir()
    generator_calls = []

    def generate_strm_files(**kwargs):
        generator_calls.append(kwargs)
        return {"status": "completed", "created": 4}

    monkeypatch.setitem(sys.modules, "generate_strm", SimpleNamespace(generate_strm_files=generate_strm_files))
    control = make_control(output_roots=(output,))
    async with TestClient(TestServer(control._app)) as client:
        response = await client.post(
            "/v1/strm/generate", headers=AUTH,
            json={
                "outputRoot": str(output),
                "streamBaseUrl": "https://stream.example/base/",
                "libraryUser": "raphael_1",
            },
        )
        body = await response.json()
        status = await client.get("/v1/strm/status", headers=AUTH)
        status_body = await status.json()

    assert response.status == 200
    assert body == {"api": "v1", "status": "completed", "created": 4}
    assert status_body == {
        "api": "v1", "running": False, "lastResult": {"status": "completed", "created": 4}
    }
    assert generator_calls == [{
        "mongo_url": "", "database": "ftp", "output_root": output.resolve(),
        "stream_base_url": "https://stream.example/base", "library_user": "raphael_1", "prune": False,
    }]


@pytest.mark.asyncio
async def test_strm_generation_uses_packaged_generator_for_legacy_and_object_id_parents(tmp_path, monkeypatch):
    import generate_strm

    output = tmp_path / "output"
    output.mkdir()
    stale = output / "existing.strm"
    stale.write_text("keep this file", encoding="utf-8")
    root_id, category_id, show_id = ObjectId(), ObjectId(), ObjectId()
    movie_id, episode_id, cover_id = ObjectId(), ObjectId(), ObjectId()
    directories = [
        {"_id": root_id, "type": "dir", "name": "raphael", "parent": None},
        {"_id": category_id, "type": "dir", "name": "S\u00e9ries", "parent": root_id},
        {"_id": show_id, "type": "dir", "name": "Example Show", "parent": category_id},
    ]
    completed_files = [
        {
            "_id": episode_id,
            "type": "file",
            "status": "completed",
            "parts": [{"tg_file": "episode-part"}],
            "name": "Example Show S01E01.mkv",
            "parent": show_id,
        },
        {
            "_id": movie_id,
            "type": "file",
            "status": "completed",
            "parts": [{"tg_file": "movie-part"}],
            "name": "Film (2024).mp4",
            "parent": "/raphael/Filmes/Film (2024)",
        },
        {
            "_id": cover_id,
            "type": "file",
            "status": "completed",
            "parts": [{"tg_file": "cover-part"}],
            "name": "poster.jpg",
            "parent": show_id,
        },
        {
            "_id": ObjectId(),
            "type": "file",
            "status": "queued",
            "parts": [{"tg_file": "queued-part"}],
            "name": "Queued.mkv",
            "parent": show_id,
        },
    ]

    class FilesCollection:
        def find(self, query, projection):
            assert projection == {"_id": 1, "name": 1, "parent": 1}
            if query == {"type": "dir"}:
                return directories
            assert query == {"type": "file", "status": "completed", "parts.0": {"$exists": True}}
            return [
                document
                for document in completed_files
                if document["type"] == "file"
                and document["status"] == "completed"
                and document.get("parts")
            ]

    class Database:
        files = FilesCollection()

    class MongoClient:
        instances = []

        def __init__(self, uri):
            self.uri = uri
            self.closed = False
            self.instances.append(self)

        def __getitem__(self, name):
            assert name == "ftp"
            return Database()

        def close(self):
            self.closed = True

    monkeypatch.setattr(generate_strm, "MongoClient", MongoClient)
    control = make_control(output_roots=(output,))
    async with TestClient(TestServer(control._app)) as client:
        response = await client.post(
            "/v1/strm/generate",
            headers=AUTH,
            json={
                "outputRoot": str(output),
                "streamBaseUrl": "https://stream.example/base/",
                "libraryUser": "raphael",
            },
        )
        body = await response.json()
        status = await (await client.get("/v1/strm/status", headers=AUTH)).json()

    episode_strm = output / "S\u00e9ries" / "Example Show" / "Example Show S01E01.strm"
    movie_strm = output / "Filmes" / "Film (2024)" / "Film (2024).strm"
    assert response.status == 200
    assert body == {"api": "v1", "generated": 2, "removed": 0, "output": str(output.resolve())}
    assert episode_strm.read_text(encoding="utf-8") == f"https://stream.example/base/transcode?id={episode_id}"
    assert movie_strm.read_text(encoding="utf-8") == f"https://stream.example/base/stream?id={movie_id}"
    assert not (output / "Séries" / "Example Show" / "poster.strm").exists()
    assert not (output / "Séries" / "Example Show" / "Queued.strm").exists()
    assert stale.read_text(encoding="utf-8") == "keep this file"
    assert status == {
        "api": "v1",
        "running": False,
        "lastResult": {"generated": 2, "removed": 0, "output": str(output.resolve())},
    }
    assert len(MongoClient.instances) == 1
    assert MongoClient.instances[0].closed


class FakeProcess:
    def __init__(self, *, running=True):
        self.pid = 4321
        self.returncode = None if running else 0
        self.terminated = False
        self.killed = False
        self.wait_calls = 0

    def terminate(self):
        self.terminated = True

    def kill(self):
        self.killed = True

    async def wait(self):
        self.wait_calls += 1
        if self.terminated or self.killed:
            self.returncode = -15 if self.terminated else -9
        return self.returncode


@pytest.mark.asyncio
async def test_feeder_supervisor_validates_roots_clamps_options_and_builds_command(tmp_path, monkeypatch):
    source = tmp_path / "source"
    destination = tmp_path / "destination"
    source.mkdir()
    destination.mkdir()
    process = FakeProcess()
    argv = []

    async def spawn(*args, **kwargs):
        argv.extend(args)
        assert kwargs["cwd"] == str((tmp_path / "tools" / "feed.py").resolve().parent.parent)
        return process

    monkeypatch.setattr(control_module.asyncio, "create_subprocess_exec", spawn)
    script = tmp_path / "tools" / "feed.py"
    script.parent.mkdir()
    script.touch()
    feeder = FeederSupervisor(
        script, source_roots=(source,), destination_roots=(destination,),
        state_dir=tmp_path / "state", stop_timeout=15,
    )
    status = await feeder.start({
        "sources": [str(source)], "destination": str(destination), "transport": "ftp-copy",
        "runMode": "watch", "workers": 100, "maxActive": 0, "pollSeconds": 9999,
        "retries": -1, "excludeDirectories": ["cache"], "overwrite": True,
        "allFiles": True, "sourcePolicy": "delete-after-success",
    })
    assert status == {"running": True, "pid": 4321, "exitCode": None, "startedAt": status["startedAt"]}
    assert "--workers" in argv and argv[argv.index("--workers") + 1] == "16"
    assert argv[argv.index("--max-active") + 1] == "1"
    assert argv[argv.index("--poll-seconds") + 1] == "3600"
    assert argv[argv.index("--retries") + 1] == "0"
    assert "--watch" in argv and "--overwrite" in argv and "--all-files" in argv and "--delete-source" in argv
    assert (tmp_path / "state" / "feeder.json").parent.is_dir()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "config",
    [
        {"sources": []},
        {"sources": ["C:/outside"]},
        {"sources": ["{missing}"]},
        {"sources": ["{source}"], "transport": "unknown"},
        {"sources": ["{source}"], "runMode": "continuous"},
        {"sources": ["{source}"], "excludeDirectories": "not-array"},
        {"sources": ["{source}"], "excludeDirectories": ["../escape"]},
        {"sources": ["{source}"], "sourcePolicy": "delete-always"},
    ],
)
async def test_feeder_supervisor_rejects_invalid_configuration_before_spawning(tmp_path, monkeypatch, config):
    source = tmp_path / "source"
    source.mkdir()
    resolved = {key: value.replace("{source}", str(source)).replace("{missing}", str(tmp_path / "missing")) if isinstance(value, str) else [
        x.replace("{source}", str(source)).replace("{missing}", str(tmp_path / "missing")) if isinstance(x, str) else x for x in value
    ] if isinstance(value, list) else value for key, value in config.items()}
    spawned = []

    async def spawn(*_args, **_kwargs):
        spawned.append(True)
        return FakeProcess()

    monkeypatch.setattr(control_module.asyncio, "create_subprocess_exec", spawn)
    feeder = FeederSupervisor(
        tmp_path / "feed.py", source_roots=(source,), destination_roots=(source,), state_dir=tmp_path / "state"
    )
    with pytest.raises((ValueError, OSError)):
        await feeder.start(resolved)
    assert not spawned


@pytest.mark.asyncio
async def test_feeder_supervisor_rejects_concurrent_start_and_stops_running_process(tmp_path, monkeypatch):
    process = FakeProcess()

    async def spawn(*_args, **_kwargs):
        return process

    monkeypatch.setattr(control_module.asyncio, "create_subprocess_exec", spawn)
    source = tmp_path / "source"
    source.mkdir()
    feeder = FeederSupervisor(tmp_path / "feed.py", source_roots=(source,), destination_roots=(source,), state_dir=tmp_path / "state")
    await feeder.start({"sources": [str(source)]})
    with pytest.raises(ConflictError, match="already running"):
        await feeder.start({"sources": [str(source)]})
    stopped = await feeder.stop()
    assert process.terminated and not process.killed
    assert stopped["running"] is False and stopped["exitCode"] == -15


@pytest.mark.asyncio
async def test_feeder_supervisor_kills_process_when_graceful_stop_times_out(tmp_path, monkeypatch):
    class HangingProcess(FakeProcess):
        async def wait(self):
            self.wait_calls += 1
            if self.killed:
                self.returncode = -9
                return self.returncode
            await asyncio.Future()

    process = HangingProcess()

    async def spawn(*_args, **_kwargs):
        return process

    monkeypatch.setattr(control_module.asyncio, "create_subprocess_exec", spawn)
    async def quick_timeout(awaitable, timeout):
        if getattr(awaitable, "cr_code", None):
            awaitable.close()
        raise asyncio.TimeoutError

    monkeypatch.setattr(control_module.asyncio, "wait_for", quick_timeout)
    source = tmp_path / "source"
    source.mkdir()
    feeder = FeederSupervisor(tmp_path / "feed.py", source_roots=(source,), destination_roots=(source,), state_dir=tmp_path / "state")
    await feeder.start({"sources": [str(source)]})
    stopped = await feeder.stop()
    assert process.terminated and process.killed
    assert stopped["exitCode"] == -9
