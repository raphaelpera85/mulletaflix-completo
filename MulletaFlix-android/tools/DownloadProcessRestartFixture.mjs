import http from 'node:http';
import { readFileSync } from 'node:fs';

const port = Number(process.argv[2]);
const fixturePath = process.argv[3];
if (!fixturePath) throw new Error('Expected a playable Matroska fixture path');
const media = readFileSync(fixturePath);
const size = media.length;
if (size <= 8 * 1024) throw new Error(`Fixture is too small for a partial restart test: ${size}`);
const requests = [];
const completedRequests = [];
let mediaDisabled = false;
let requestsAtDisable = null;

const server = http.createServer((request, response) => {
  if (request.url === '/health') {
    response.writeHead(200, { 'content-type': 'text/plain' }).end('ready');
    return;
  }
  if (request.url === '/status') {
    response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({
      size,
      requests,
      completedRequests,
      rangeStarts: requests.filter((entry) => entry.rangeStart > 0).map((entry) => entry.rangeStart),
    }));
    return;
  }
  if (request.url === '/disable-media') {
    mediaDisabled = true;
    requestsAtDisable = requests.length;
    response.writeHead(200, { 'content-type': 'text/plain' }).end('disabled');
    return;
  }
  if (request.url === '/assert-offline') {
    const noOriginRequests = mediaDisabled && requests.length === requestsAtDisable;
    response.writeHead(noOriginRequests ? 204 : 409).end();
    return;
  }
  if (request.url !== '/media') {
    response.writeHead(404).end();
    return;
  }

  const range = /^bytes=(\d+)-/i.exec(request.headers.range ?? '');
  const start = range ? Number(range[1]) : 0;
  requests.push({ rangeStart: start, at: Date.now() });
  if (mediaDisabled) {
    response.writeHead(503, { 'content-type': 'text/plain', connection: 'close' }).end('media source disabled');
    return;
  }
  if (start >= size) {
    response.writeHead(416, { 'content-range': `bytes */${size}` }).end();
    return;
  }
  const end = size - 1;
  response.once('finish', () => completedRequests.push({ rangeStart: start, bytes: end - start + 1 }));
  const headers = {
    'accept-ranges': 'bytes',
    'content-length': String(end - start + 1),
    'content-type': 'video/x-matroska',
    'cache-control': 'no-store',
    connection: 'close',
  };
  if (range) headers['content-range'] = `bytes ${start}-${end}/${size}`;
  response.writeHead(range ? 206 : 200, headers);

  let offset = start;
  let initialPartialHeld = false;
  const writeChunk = () => {
    if (response.destroyed || offset >= size) {
      response.end();
      return;
    }
    if (start === 0 && offset >= 1024 * 1024 && !initialPartialHeld) {
      initialPartialHeld = true;
      setTimeout(writeChunk, 120_000);
      return;
    }
    const chunkSize = Math.min(64 * 1024, size - offset);
    const chunk = media.subarray(offset, offset + chunkSize);
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
