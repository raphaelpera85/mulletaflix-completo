import json

with open(r'C:\Users\rapha\.gemini\antigravity-ide\brain\3c2e3911-7491-41ed-a88a-bb199c7d2758\.system_generated\logs\transcript.jsonl', encoding='utf-8') as f:
    for line in f:
        d = json.loads(line)
        if d.get('type') == 'USER_INPUT':
            print(f"STEP {d.get('step_index')}: {d.get('content')}")
