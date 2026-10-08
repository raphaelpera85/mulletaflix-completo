from __future__ import annotations

import threading
from pathlib import Path

import pytest

from tools import feed_ftp, strm_downloader


@pytest.fixture
def categorized_library(tmp_path: Path) -> Path:
    library = tmp_path / "library"
    titles = {
        "Animações": ["A Anime.mkv", "Z Anime (2026).mkv", "Alpha Anime (2020).mkv"],
        "Filmes": ["Zebra (2020).mkv", "Alpha (1990).mkv"],
        "Series": ["Show B/S01E01.mkv", "Show A/S01E02.mkv", "Show A/S01E01.mkv"],
        "Doramas": ["Drama B/S01E01.mkv", "Drama A/S01E02.mkv", "Drama A/S01E01.mkv"],
        "Novelas": ["Novela B/S01E01.mkv", "Novela A/S01E02.mkv", "Novela A/S01E01.mkv"],
        "Porno": ["Zulu.mkv", "Bravo.mkv"],
    }
    for category, filenames in titles.items():
        for filename in filenames:
            path = library / category / filename
            path.parent.mkdir(parents=True, exist_ok=True)
            path.touch()
    (library / "Filmes" / "poster.jpg").touch()
    (library / "Filmes" / "unfinished.mkv.part").touch()
    return library


def _episode_order(paths: list[Path], library: Path, category: str) -> list[tuple[str, str]]:
    return [
        (relative.parts[1], path.name)
        for path in paths
        if (relative := path.relative_to(library)).parts[0] == category
    ]


def test_feeder_uses_category_priority_and_alphabetical_titles(categorized_library: Path):
    items = list(feed_ftp.iter_files_by_priority([categorized_library], False, set()))
    paths = [path for _root, path in items]

    assert [path.relative_to(categorized_library).parts[0] for path in paths] == [
        "Animações",
        "Animações",
        "Animações",
        "Filmes",
        "Filmes",
        "Series",
        "Series",
        "Series",
        "Doramas",
        "Doramas",
        "Doramas",
        "Novelas",
        "Novelas",
        "Novelas",
        "Porno",
        "Porno",
    ]
    assert [path.name for path in paths if path.parent.name == "Filmes"] == [
        "Alpha (1990).mkv",
        "Zebra (2020).mkv",
    ]
    assert [path.name for path in paths if path.parent.name == "Porno"] == ["Bravo.mkv", "Zulu.mkv"]
    assert [path.name for path in paths if path.parent.name == "Animações"] == [
        "A Anime.mkv",
        "Alpha Anime (2020).mkv",
        "Z Anime (2026).mkv",
    ]
    assert _episode_order(paths, categorized_library, "Series") == [
        ("Show A", "S01E01.mkv"),
        ("Show A", "S01E02.mkv"),
        ("Show B", "S01E01.mkv"),
    ]
    assert _episode_order(paths, categorized_library, "Doramas") == [
        ("Drama A", "S01E01.mkv"),
        ("Drama A", "S01E02.mkv"),
        ("Drama B", "S01E01.mkv"),
    ]
    assert _episode_order(paths, categorized_library, "Novelas") == [
        ("Novela A", "S01E01.mkv"),
        ("Novela A", "S01E02.mkv"),
        ("Novela B", "S01E01.mkv"),
    ]
    assert all(path.suffix.lower() != ".jpg" for path in paths)
    assert all("unfinished" not in path.name for path in paths)


def test_overwrite_copy_failure_preserves_existing_destination(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    destination = tmp_path / "library" / "title.mkv"
    destination.parent.mkdir()
    source.write_bytes(b"new bytes")
    destination.write_bytes(b"known good old bytes")
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "ensure_directory", lambda *_args, **_kwargs: None)

    def fail_copy(*_args, **_kwargs):
        raise OSError("simulated copy failure")

    monkeypatch.setattr(feed_ftp.shutil, "copyfile", fail_copy)

    with pytest.raises(OSError, match="simulated copy failure"):
        feed_ftp.copy_one(
            source,
            destination,
            destination.parent,
            "mongodb://unused",
            True,
            set(),
            threading.Lock(),
        )

    assert destination.read_bytes() == b"known good old bytes"
    assert list(destination.parent.glob("*.copying")) == []


def test_overwrite_copy_success_replaces_destination_and_cleans_staging(tmp_path, monkeypatch):
    source = tmp_path / "source.mkv"
    destination = tmp_path / "library" / "title.mkv"
    destination.parent.mkdir()
    source.write_bytes(b"new bytes")
    destination.write_bytes(b"old bytes")
    monkeypatch.setattr(feed_ftp, "ensure_nebula_metadata", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(feed_ftp, "ensure_directory", lambda *_args, **_kwargs: None)

    copied_size = feed_ftp.copy_one(
        source,
        destination,
        destination.parent,
        "mongodb://unused",
        True,
        set(),
        threading.Lock(),
    )

    assert copied_size == len(b"new bytes")
    assert destination.read_bytes() == b"new bytes"
    assert list(destination.parent.glob("*.copying")) == []


def test_strm_downloader_uses_same_category_priority_and_alphabetical_titles(categorized_library: Path):
    items = strm_downloader.iter_strm_files_prioritized([categorized_library])
    paths = [path for _root, path, _category, _year in items]

    assert [path.relative_to(categorized_library).parts[0] for path in paths] == [
        "Animações",
        "Animações",
        "Animações",
        "Filmes",
        "Filmes",
        "Series",
        "Series",
        "Series",
        "Doramas",
        "Doramas",
        "Doramas",
        "Novelas",
        "Novelas",
        "Novelas",
        "Porno",
        "Porno",
    ]
    assert [path.name for path in paths if path.parent.name == "Filmes"] == [
        "Alpha (1990).mkv",
        "Zebra (2020).mkv",
    ]
    assert [path.name for path in paths if path.parent.name == "Porno"] == ["Bravo.mkv", "Zulu.mkv"]
    assert [path.name for path in paths if path.parent.name == "Animações"] == [
        "A Anime.mkv",
        "Alpha Anime (2020).mkv",
        "Z Anime (2026).mkv",
    ]
    assert _episode_order(paths, categorized_library, "Series") == [
        ("Show A", "S01E01.mkv"),
        ("Show A", "S01E02.mkv"),
        ("Show B", "S01E01.mkv"),
    ]
    assert _episode_order(paths, categorized_library, "Doramas") == [
        ("Drama A", "S01E01.mkv"),
        ("Drama A", "S01E02.mkv"),
        ("Drama B", "S01E01.mkv"),
    ]
    assert _episode_order(paths, categorized_library, "Novelas") == [
        ("Novela A", "S01E01.mkv"),
        ("Novela A", "S01E02.mkv"),
        ("Novela B", "S01E01.mkv"),
    ]


@pytest.mark.parametrize(
    ("source_name", "expected"),
    [
        ("Ação & Reação (2024)", ("acao reacao", "2024")),
        ("Livro Sem Ano", None),
        ("(2020)", None),
    ],
)
def test_downloader_movie_identity_uses_normalized_title_and_year(source_name, expected):
    assert strm_downloader.movie_identity(source_name) == expected


@pytest.mark.parametrize(
    ("series", "filename", "expected"),
    [
        ("Minha Série", "Minha.Serie.S02E07.mkv", ("minha serie", 2, 7)),
        ("Season 3", "Drama - 1x02.mp4", ("drama", 1, 2)),
        ("Sem Episódio", "feature-film.mkv", None),
    ],
)
def test_downloader_episode_identity_extracts_series_and_episode(series, filename, expected):
    assert strm_downloader.episode_identity(series, filename) == expected


def test_downloader_destination_preserves_categories_and_season_structure(tmp_path: Path):
    source = tmp_path / "source"
    destination = tmp_path / "destination"
    episode = source / "Series" / "Demon School" / "Demon School S02E03.mkv"
    animation = source / "Animações" / "Aventura" / "episode.mkv"

    assert strm_downloader.destination_for(source, destination, episode) == (
        destination / "Series" / "Demon School" / "Demon School S02E03.mkv"
    )
    assert strm_downloader.destination_for(source, destination, animation) == (
        destination / "Animações" / "Aventura" / "episode.mkv"
    )


@pytest.mark.parametrize("name", ["movie.mkv.download", "movie.mkv.part1", "movie.partial", "movie.crdownload"])
def test_downloader_ignores_incomplete_download_artifacts(name):
    assert strm_downloader.is_incomplete_filename(name)


def test_feeder_episode_identity_supports_alternate_season_episode_notation():
    assert feed_ftp.episode_identity("Drama", "Drama - 1x02.mp4") == ("drama", 1, 2)


def test_downloader_creates_season_folder_for_alternate_episode_notation(tmp_path: Path):
    source = tmp_path / "source"
    destination = tmp_path / "destination"
    episode = source / "Drama - 1x02.mp4"

    assert strm_downloader.destination_for(source, destination, episode) == (
        destination / "Series" / "Drama" / "Season 01" / "Drama - 1x02.mp4"
    )


def test_downloader_reads_first_non_comment_strm_url_and_rejects_empty_file(tmp_path: Path):
    strm = tmp_path / "movie.strm"
    strm.write_text("# generated file\n\n https://media.example/movie.mkv \n", encoding="utf-8")
    assert strm_downloader.read_strm_url(strm) == "https://media.example/movie.mkv"

    strm.write_text("# no stream url\n\n", encoding="utf-8")
    with pytest.raises(ValueError, match="vazio ou inválido"):
        strm_downloader.read_strm_url(strm)
