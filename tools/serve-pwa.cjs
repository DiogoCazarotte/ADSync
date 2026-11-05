const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const args = process.argv.slice(2);
const option = (name, fallback) => args.includes(name) ? args[args.indexOf(name) + 1] : fallback;
const port = Number(option('--port', '5180'));
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Porta inválida.');
const statePath = option('--state', null);
const fixedRoot = path.resolve(option('--root', path.join(__dirname, '../ADSync.Client/bin/Release/net10.0/publish/wwwroot')));
const root = () => statePath ? path.resolve(JSON.parse(fs.readFileSync(statePath, 'utf8').replace(/^\uFEFF/, '')).root) : fixedRoot;
const mime = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css', '.png': 'image/png', '.webmanifest': 'application/manifest+json', '.dat': 'application/octet-stream', '.woff': 'font/woff', '.woff2': 'font/woff2', '.svg': 'image/svg+xml', '.ico': 'image/x-icon' };
http.createServer((req, res) => {
    let pathname;
    try { pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname); }
    catch { res.writeHead(400); return res.end(); }
    if (pathname === '/_pwa/status') {
        res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
        return res.end(JSON.stringify({ app: 'NexServicePwa', pid: process.pid }));
    }
    if (pathname.startsWith('/api/')) {
        const proxy = http.request({ hostname: '127.0.0.1', port: 5099, path: req.url, method: req.method, headers: { ...req.headers, host: 'localhost:5099' }, timeout: 15000 }, incoming => {
            res.writeHead(incoming.statusCode, { ...incoming.headers, 'Cache-Control': 'no-store' }); incoming.pipe(res);
        });
        proxy.on('timeout', () => proxy.destroy());
        proxy.on('error', () => { if (!res.headersSent) res.writeHead(503, { 'Content-Type': 'application/json' }); res.end('{"mensagem":"API indisponível. Pendências preservadas."}'); });
        req.on('aborted', () => proxy.destroy());
        req.pipe(proxy); return;
    }
    if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405); return res.end(); }
    let base, file;
    try { base = root(); file = path.resolve(base, '.' + pathname); }
    catch { res.writeHead(503); return res.end('PWA ainda não publicada.'); }
    if (file !== base && !file.startsWith(base + path.sep)) { res.writeHead(403); return res.end(); }
    if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) {
        if (path.extname(file)) { res.writeHead(404); return res.end(); }
        file = path.join(base, 'index.html');
    }
    if (!fs.existsSync(file)) { res.writeHead(503); return res.end('PWA ainda não publicada.'); }
    res.writeHead(200, { 'Content-Type': mime[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache', 'Blazor-Environment': 'Production' });
    if (req.method === 'HEAD') return res.end();
    fs.createReadStream(file).on('error', () => res.destroy()).pipe(res);
}).listen(port, '127.0.0.1', () => console.log(`PWA NexService: http://127.0.0.1:${port}`));
