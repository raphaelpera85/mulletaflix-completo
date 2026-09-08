import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

with open(r'C:\Users\rapha\.gemini\antigravity-ide\brain\3c2e3911-7491-41ed-a88a-bb199c7d2758\.system_generated\logs\transcript.jsonl', encoding='utf-8') as f:
    for line in f:
        d = json.loads(line)
        idx = d.get('step_index', 0)
        if 1740 <= idx <= 1775:
            print(f"=== STEP {idx} ({d.get('type')}) ===")
            if d.get('thinking'):
                print(f"THINKING: {d.get('thinking')[:300]}")
            if d.get('tool_calls'):
                print(f"TOOLS: {d.get('tool_calls')}")
            if d.get('content'):
                print(f"CONTENT: {d.get('content')[:300]}")
