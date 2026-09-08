import json

with open('graphify-out/graph.json') as f:
    data = json.load(f)

# Find API-related nodes
api_nodes = [n for n in data['nodes'] if 'api' in n.get('name', '').lower() or 'controller' in n.get('name', '').lower() or 'endpoint' in n.get('name', '').lower()]
print(f'API/Controller/Endpoint nodes: {len(api_nodes)}')
for n in api_nodes[:20]:
    print(f'  {n["id"]}: {n["name"]} ({n.get("type", "?")})')

# Find DTO nodes
dto_nodes = [n for n in data['nodes'] if 'dto' in n.get('name', '').lower()]
print(f'\nDTO nodes: {len(dto_nodes)}')
for n in dto_nodes[:20]:
    print(f'  {n["id"]}: {n["name"]} ({n.get("type", "?")})')

# Find Plugin-related nodes
plugin_nodes = [n for n in data['nodes'] if 'plugin' in n.get('name', '').lower()]
print(f'\nPlugin nodes: {len(plugin_nodes)}')
for n in plugin_nodes[:20]:
    print(f'  {n["id"]}: {n["name"]} ({n.get("type", "?")})')

# Find Migration-related nodes
migration_nodes = [n for n in data['nodes'] if 'migration' in n.get('name', '').lower()]
print(f'\nMigration nodes: {len(migration_nodes)}')
for n in migration_nodes[:20]:
    print(f'  {n["id"]}: {n["name"]} ({n.get("type", "?")})')

# Find Client-related nodes (web, mobile, tv)
client_nodes = [n for n in data['nodes'] if any(x in n.get('name', '').lower() for x in ['web', 'mobile', 'tv', 'client', 'react', 'vue', 'angular'])]
print(f'\nClient/UI nodes: {len(client_nodes)}')
for n in client_nodes[:20]:
    print(f'  {n["id"]}: {n["name"]} ({n.get("type", "?")})')