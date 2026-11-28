import pytest
from bson import ObjectId

import generate_strm


def test_path_and_filename_helpers_handle_windows_paths_and_invalid_characters():
    assert generate_strm._path_parts(None) == []
    assert generate_strm._path_parts(r"/raphael\Series\Show") == ["raphael", "Series", "Show"]
    assert generate_strm.safe_windows_name("  A:B?  ") == "A - B -"
    assert generate_strm.safe_windows_name("...   ") == "_"
    assert generate_strm.stream_endpoint("episode.M4V") == "stream"
    assert generate_strm.stream_endpoint("episode.mkv") == "transcode"


@pytest.mark.parametrize(
    "reserved",
    [
        "CON",
        "con",
        "PRN",
        "AUX",
        "NUL",
        "CON.txt",
        "con .txt",
        "CONIN$",
        "CONOUT$",
        "COM1",
        "COM9",
        "COM¹",
        "LPT1",
        "LPT9",
        "LPT³",
    ],
)
def test_safe_windows_name_escapes_reserved_device_names(reserved):
    safe_name = generate_strm.safe_windows_name(reserved)

    assert safe_name != reserved
    assert safe_name.casefold() not in {"con", "prn", "aux", "nul", "com1", "com9", "lpt1", "lpt9"}


def test_generator_writes_reserved_device_name_as_safe_strm(monkeypatch, tmp_path):
    class Files:
        def find(self, query, projection):
            if query == {"type": "dir"}:
                return []
            return [{"_id": ObjectId(), "name": "CON.mkv", "parent": "/raphael/Series/Show"}]

    class Client:
        def __getitem__(self, _name):
            return type("Database", (), {"files": Files()})()

        def close(self):
            pass

    monkeypatch.setattr(generate_strm, "MongoClient", lambda *_args, **_kwargs: Client())

    result = generate_strm.generate_strm_files(output_root=tmp_path, stream_base_url="http://localhost:2122")

    generated_files = list(tmp_path.rglob("*.strm"))
    assert result["generated"] == 1
    assert len(generated_files) == 1
    assert generated_files[0].name == "CON_.strm"
    assert generated_files[0].read_text(encoding="utf-8").startswith(
        "http://localhost:2122/transcode?id="
    )


def test_directory_map_breaks_cycles_and_resolves_legacy_parent_paths():
    first, second, legacy = ObjectId(), ObjectId(), ObjectId()

    class Files:
        def find(self, query, projection):
            assert query == {"type": "dir"}
            return [
                {"_id": first, "name": "One", "parent": second},
                {"_id": second, "name": "Two", "parent": first},
                {"_id": legacy, "name": "Show", "parent": r"/raphael\Series"},
            ]

    result = generate_strm._build_directory_path_map(type("Database", (), {"files": Files()})())

    assert result[str(first)] == ["Two", "One"]
    assert result[str(second)] == ["Two"]
    assert result[str(legacy)] == ["raphael", "Series", "Show"]


def test_prune_is_rejected_before_opening_mongo(monkeypatch, tmp_path):
    monkeypatch.setattr(generate_strm, "MongoClient", lambda *_args, **_kwargs: pytest.fail("Mongo must not be opened"))

    with pytest.raises(ValueError, match="pruning is not supported"):
        generate_strm.generate_strm_files(output_root=tmp_path, prune=True)


def test_client_closes_when_output_write_fails(monkeypatch, tmp_path):
    class Files:
        def find(self, query, projection):
            if query == {"type": "dir"}:
                return []
            return [{"_id": ObjectId(), "name": "episode.mkv", "parent": "/raphael/Series/Show"}]

    class Client:
        closed = False

        def __getitem__(self, _name):
            return type("Database", (), {"files": Files()})()

        def close(self):
            self.closed = True

    client = Client()

    monkeypatch.setattr(generate_strm, "MongoClient", lambda *_args, **_kwargs: client)
    target = tmp_path / "Series"
    target.mkdir()
    (target / "Show").write_text("not a directory", encoding="utf-8")

    with pytest.raises((FileExistsError, NotADirectoryError, OSError)):
        generate_strm.generate_strm_files(output_root=tmp_path)

    assert client.closed
