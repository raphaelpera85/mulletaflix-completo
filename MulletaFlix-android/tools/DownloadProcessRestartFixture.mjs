import http from 'node:http';

const port = Number(process.argv[2]);
const size = 16 * 1024 * 1024;
const requests = [];
const completedRequests = [];

function payloadByte(offset) {
  // Stable pseudo-random bytes prevent Media3 from compressing the test fixture.
  let value = (offset ^ 0x5a17c3) >>> 0;
  value = Math.imul(value ^ (value >>> 16), 0x45d9f3b) >>> 0;
  value = Math.imul(value ^ (value >>> 16), 0x45d9f3b) >>> 0;
  return (value ^ (value >>> 16)) & 0xff;
}

const server = http.createServer((request, response) => {
  if (request.url === '/health') {
    response.writeHead(200, { 'content-type': 'text/plain' }).end('ready');
    return;
  }
  if (request.url === '/status') {
    response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({
      requests,
      completedRequests,
      rangeStarts: requests.filter((entry) => entry.rangeStart > 0).map((entry) => entry.rangeStart),
    }));
    return;
  }
  if (request.url !== '/media') {
    response.writeHead(404).end();
    return;
  }

  const range = /^bytes=(\d+)-/i.exec(request.headers.range ?? '');
  const start = range ? Number(range[1]) : 0;
  requests.push({ rangeStart: start, at: Date.now() });
  if (start >= size) {
    response.writeHead(416, { 'content-range': `bytes */${size}` }).end();
    return;
  }
  const end = size - 1;
  response.once('finish', () => completedRequests.push({ rangeStart: start, bytes: end - start + 1 }));
  const headers = {
    'accept-ranges': 'bytes',
    'content-length': String(end - start + 1),
    'content-type': 'video/mp4',
    'cache-control': 'no-store',
    connection: 'close',
  };
  if (range) headers['content-range'] = `bytes ${start}-${end}/${size}`;
  response.writeHead(range ? 206 : 200, headers);

  let offset = start;
  const writeChunk = () => {
    if (response.destroyed || offset >= size) {
      response.end();
      return;
    }
    const chunkSize = Math.min(64 * 1024, size - offset);
    const chunk = Buffer.allocUnsafe(chunkSize);
    for (let i = 0; i < chunkSize; i++) chunk[i] = payloadByte(offset + i);
    offset += chunkSize;
    if (response.write(chunk)) setTimeout(writeChunk, 35);
    else response.once('drain', () => setTimeout(writeChunk, 35));
  };
  response.on('error', () => {});
  writeChunk();
});

server.listen(port, '0.0.0.0');
server.on('listening', () => process.stdout.write(`fixture-ready:${port}\n`));
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
