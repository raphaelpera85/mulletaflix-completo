import http from 'node:http';

const port = Number(process.argv[2]);
const itemId = process.argv[3];
const serverId = process.argv[4];
const requests = [];
const bookText = 'Livro reaberto após cold start do processo principal.';

if (!Number.isInteger(port) || port < 1 || port > 65535 || !itemId || !serverId) {
  throw new Error('Expected port, itemId and serverId arguments.');
}

const server = http.createServer((request, response) => {
  const url = new URL(request.url ?? '/', 'http://localhost');
  if (url.pathname === '/health') {
    response.writeHead(200, { 'content-type': 'text/plain' }).end('ready');
    return;
  }
  if (url.pathname === '/status') {
    response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({ requests }));
    return;
  }

  requests.push({ method: request.method, path: url.pathname });
  if (request.method === 'GET' && url.pathname === `/Users/book-cold-start-user/Items/${itemId}`) {
    response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({
      Id: itemId,
      Name: 'Livro do cold start',
      ServerId: serverId,
      Type: 'Book',
    }));
    return;
  }
  if (request.method === 'GET' && url.pathname === `/BookReader/Items/${itemId}/BookReader/Status`) {
    response.writeHead(200, { 'content-type': 'application/json' }).end('{"Status":"Ready"}');
    return;
  }
  if (request.method === 'GET' && url.pathname === `/BookReader/Items/${itemId}/BookReader/Epub`) {
    response.writeHead(200, { 'content-type': 'text/plain; charset=utf-8' }).end(bookText);
    return;
  }

  response.writeHead(404, { 'content-type': 'application/json' }).end('{}');
});

server.listen(port, '0.0.0.0', () => process.stdout.write(`fixture-ready:${port}\n`));
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
