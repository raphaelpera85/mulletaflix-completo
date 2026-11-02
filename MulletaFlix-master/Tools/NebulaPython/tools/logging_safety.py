import logging
import traceback
from urllib.parse import urlsplit


def escape_log_text(value):
    return str(value).replace("\r", r"\r").replace("\n", r"\n")


def safe_print(message, *, flush=False):
    print(escape_log_text(message), flush=flush)


def safe_exception_summary(error):
    error_type = type(error).__name__
    status_code = getattr(error, "code", None)
    if isinstance(status_code, int) and not isinstance(status_code, bool):
        return f"{error_type} (code {status_code})"
    error_number = getattr(error, "errno", None)
    if isinstance(error_number, int) and not isinstance(error_number, bool):
        return f"{error_type} (errno {error_number})"
    return error_type


def safe_endpoint_for_log(value):
    try:
        parsed = urlsplit(str(value))
        port = parsed.port
        hostname = parsed.hostname
        if not parsed.scheme or not hostname:
            return "[endpoint redacted]"
        host = f"[{hostname}]" if ":" in hostname else hostname
        return f"{parsed.scheme}://{host}{f':{port}' if port is not None else ''}"
    except (TypeError, ValueError):
        return "[endpoint redacted]"


class SingleLineFilter(logging.Filter):
    def filter(self, record):
        message = record.getMessage()
        exception = record.exc_text
        if record.exc_info:
            exception = "".join(traceback.format_exception(*record.exc_info))
        if exception:
            message += " | " + exception
        if record.stack_info:
            message += " | " + record.stack_info
        record.msg = escape_log_text(message)
        record.args = ()
        record.exc_info = None
        record.exc_text = None
        record.stack_info = None
        return True


class SingleLineFormatter(logging.Formatter):
    """Escape line breaks in formatted events to prevent forged log entries."""

    def format(self, record):
        return escape_log_text(super().format(record))
