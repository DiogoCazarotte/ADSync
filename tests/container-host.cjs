const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const origin = process.env.CONTAINER_TEST_ORIGIN || 'http://127.0.0.1:5130';
assert(['localhost', '127.0.0.1'].includes(new URL(origin).hostname), 'O teste exige endereço loopback.');

(async () => {
  // Somente consultas públicas e uma mutação bloqueada antes do banco; sem login real.
  const runtime = await (await fetch(origin + '/api/runtime')).json();
  assert.equal(runtime.readOnly, true, 'Use somente leitura para este teste isolado');
  assert.equal(runtime.seedEnabled, false);
  assert.equal(runtime.preventiveEnabled, false);
  const root = await fetch(origin + '/', { redirect: 'manual' });
  assert.equal(root.status, 200, 'A página local não pode redirecionar para HTTPS');
  assert.match(root.headers.get('content-type'), /text\/html/);
  assert.match(root.headers.get('cache-control'), /no-cache/);
  assert.match(await root.text(), /_framework\/blazor\.webassembly\.[^" ]+\.js/);
  for (const path of ['/api/inexistente', '/api/ausente.json', '/missing.js', '/teste.html']) {
    assert.equal((await fetch(origin + path)).status, 404, `Fallback incorreto em ${path}`);
  }
  const clientRoute = await fetch(origin + '/counter');
  assert.equal(clientRoute.status, 200);
  assert.match(await clientRoute.text(), /id="app"/);
  const unauthorized = await fetch(origin + '/api/indicadores');
  assert.equal(unauthorized.status, 401);
  assert.equal((await fetch(origin + '/api/inexistente', { method: 'POST' })).status, 403);
  const config = await (await fetch(origin + '/appsettings.json')).json();
  assert.equal(config.ApiBaseUrl, undefined, 'O cliente publicado deve usar a origem do container');
  const worker = await fetch(origin + '/service-worker.js');
  assert.match(await worker.text(), /self\.assetsManifest/);
  assert.match(worker.headers.get('cache-control'), /no-cache/);
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const context = await browser.newContext();
    const page = await context.newPage();
    const errors = [], failed = [], apiHosts = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('requestfailed', request => failed.push(request.url()));
    page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) apiHosts.push(new URL(request.url()).origin); });
    await page.goto(origin);
    await page.locator('#login-email').waitFor();
    await page.evaluate(() => Promise.race([
      navigator.serviceWorker.ready,
      new Promise((_, reject) => setTimeout(() => reject(new Error('PWA não ativou')), 30000))
    ]));
    const cacheReady = await page.evaluate(async () => (await caches.keys()).some(key => key.startsWith('offline-cache-')));
    assert.equal(cacheReady, true, 'PWA publicada não criou cache');
    await page.reload();
    await page.locator('#login-email').waitFor();
    await context.setOffline(true);
    await page.reload();
    await page.locator('#login-email').waitFor();
    assert.deepEqual(errors, []);
    assert.deepEqual(failed, []);
    assert(apiHosts.every(host => host === origin));
    console.log('PASS: API e cliente na mesma origem, arquivos WASM, fallback 404 da API, autenticação, configuração pública, cache e recarga offline da PWA. Sem banco.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
