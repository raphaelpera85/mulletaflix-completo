import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "checklist.py"
SPEC = importlib.util.spec_from_file_location("checklist", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Unable to load {SCRIPT}")
checklist = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(checklist)


class ChecklistResultTests(unittest.TestCase):
    def test_security_findings_fail_even_when_scanner_exit_code_is_zero(self):
        payload = {
            "summary": {"total_findings": 1},
        }
        with tempfile.TemporaryDirectory() as temporary_directory:
            script = Path(temporary_directory) / "security_scan.py"
            script.write_text(
                "import json\n"
                f"print(json.dumps({json.dumps(payload)}))\n",
                encoding="utf-8",
            )

            result = checklist.run_script("Security Scan", script, temporary_directory)

            self.assertFalse(result["passed"])


if __name__ == "__main__":
    unittest.main()
