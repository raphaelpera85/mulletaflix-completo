from __future__ import annotations

import builtins
import logging
import sys

import main as main_module


class AsciiStrictStream:
    encoding = "ascii"

    def __init__(self):
        self.writes = []

    def write(self, value):
        value.encode(self.encoding)
        self.writes.append(value)

    def flush(self):
        pass


def test_safe_stream_handler_replaces_text_unsupported_by_terminal_encoding(monkeypatch):
    monkeypatch.setattr(logging, "raiseExceptions", False)
    stream = AsciiStrictStream()
    handler = main_module.SafeStreamHandler(stream)
    handler.setFormatter(logging.Formatter("%(message)s"))
    record = logging.LogRecord("nebula-test", logging.INFO, __file__, 1, "Invasão concluída", (), None)

    handler.emit(record)

    assert stream.writes == ["Invas?o conclu?da\n"]


def test_single_line_formatter_escapes_untrusted_line_breaks_and_tracebacks():
    formatter = main_module.SingleLineFormatter("%(levelname)s %(message)s")
    assert all(isinstance(handler.formatter, main_module.SingleLineFormatter) for handler in main_module.logger.handlers)
    try:
        raise ValueError("invalid media\r\nINFO forged entry")
    except ValueError:
        exc_info = sys.exc_info()
    record = logging.LogRecord("nebula-test", logging.ERROR, __file__, 1, "Failed: %s", ("movie.mkv\r\nWARNING forged entry",), exc_info)

    formatted = formatter.format(record)

    assert "\n" not in formatted
    assert "\r" not in formatted
    assert r"movie.mkv\r\nWARNING forged entry" in formatted
    assert r"invalid media\r\nINFO forged entry" in formatted
    assert "Traceback" in formatted


def test_strm_downloader_uses_single_line_formatter(monkeypatch):
    import io
    from tools import strm_downloader

    stream = io.StringIO()
    monkeypatch.setattr(strm_downloader.sys, "stdout", stream)
    logger = strm_downloader.logger
    previous_handlers = logger.handlers[:]
    previous_level = logger.level
    try:
        strm_downloader.setup_logger()
        logger.warning("bad title: %s", "movie.mkv\r\nERROR forged entry")
        assert stream.getvalue().count("\n") == 1
        assert r"movie.mkv\r\nERROR forged entry" in stream.getvalue()
    finally:
        for handler in logger.handlers:
            handler.close()
        logger.handlers[:] = previous_handlers
        logger.setLevel(previous_level)


def test_compacting_file_handler_summarizes_and_clears_rotating_log(tmp_path):
    log_path = tmp_path / "nebula.log"
    context_path = tmp_path / "nebula-context.md"
    handler = main_module.CompactingFileHandler(
        log_path,
        compact_lines=2,
        context_file=context_path,
        maxBytes=1024,
        backupCount=1,
        encoding="utf-8",
    )
    handler.setFormatter(logging.Formatter("%(message)s"))
    handler.emit(logging.LogRecord("nebula-test", logging.INFO, __file__, 1, "Concluido: item Fila: 3", (), None))
    handler.emit(logging.LogRecord("nebula-test", logging.ERROR, __file__, 2, "falhou Progresso: 50%", (), None))

    summary = context_path.read_text(encoding="utf-8")
    assert "- linhas compactadas: 2" in summary
    assert "- concluidos no trecho: 1" in summary
    assert "- linhas com erro/falha no trecho: 1" in summary
    assert "- ultimo estado de fila: Concluido: item Fila: 3" in summary
    assert "- ultimo progresso: falhou Progresso: 50%" in summary
    assert log_path.read_text(encoding="utf-8") == ""
    assert handler.line_count == 0
    assert not handler.recent_lines
    handler.close()


def test_compacting_file_handler_preserves_logs_if_context_write_fails(tmp_path):
    log_path = tmp_path / "nebula.log"
    handler = main_module.CompactingFileHandler(
        log_path,
        compact_lines=1,
        context_file=tmp_path,
        maxBytes=1024,
        backupCount=1,
        encoding="utf-8",
    )
    handler.setFormatter(logging.Formatter("%(message)s"))

    handler.emit(logging.LogRecord("nebula-test", logging.INFO, __file__, 1, "fila ativa", (), None))
    handler.emit(logging.LogRecord("nebula-test", logging.INFO, __file__, 2, "mais dados", (), None))

    assert log_path.read_text(encoding="utf-8") == "fila ativa\nmais dados\n"
    handler.close()


def test_writable_log_path_falls_back_to_program_data(monkeypatch, tmp_path):
    real_open = builtins.open
    program_data = tmp_path / "program-data"

    def fail_original_path(path, *args, **kwargs):
        if str(path) == "nebula.log":
            raise PermissionError("directory is read-only")
        return real_open(path, *args, **kwargs)

    monkeypatch.setattr(main_module, "open", fail_original_path, raising=False)
    monkeypatch.setenv("PROGRAMDATA", str(program_data))
    monkeypatch.delenv("LOCALAPPDATA", raising=False)
    monkeypatch.delenv("APPDATA", raising=False)
    monkeypatch.delenv("TEMP", raising=False)

    resolved = main_module._get_writable_file_path("nebula.log")

    assert resolved == str(program_data / "Mulletaflix" / "nebula" / "nebula.log")
    assert (program_data / "Mulletaflix" / "nebula" / "nebula.log").is_file()


def test_writable_log_path_returns_original_name_when_all_fallbacks_fail(monkeypatch):
    def fail_all_paths(*_args, **_kwargs):
        raise PermissionError("no writable directory")

    monkeypatch.setattr(main_module, "open", fail_all_paths, raising=False)
    for variable in ("PROGRAMDATA", "LOCALAPPDATA", "APPDATA", "TEMP"):
        monkeypatch.delenv(variable, raising=False)

    assert main_module._get_writable_file_path("nebula.log") == "nebula.log"
