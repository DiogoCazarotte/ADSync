const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../.local-demo/cadastros/client/wwwroot');
const mime = {'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.wasm':'application/wasm','.css':'text/css','.png':'image/png','.webmanifest':'application/manifest+json','.dat':'application/octet-stream'};
http.createServer((req, res) => {
    let file;
    try { file = path.resolve(root, '.' + decodeURIComponent(req.url.split('?')[0])); }
    catch { res.writeHead(400); res.end(); return; }
    if (file !== root && !file.startsWith(root + path.sep)) { res.writeHead(403); res.end(); return; }
    if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) {
        if (path.extname(file)) { res.writeHead(404); res.end(); return; }
        file = path.join(root, 'index.html');
    }
    res.writeHead(200, {'Content-Type':mime[path.extname(file)] || 'application/octet-stream','Cache-Control':'no-store','Blazor-Environment':'Production'});
    fs.createReadStream(file).pipe(res);
}).listen(5126, '127.0.0.1', () => console.log('Cadastros de testes: http://127.0.0.1:5126'));
