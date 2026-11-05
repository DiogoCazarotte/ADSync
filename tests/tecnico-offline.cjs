const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { chromium } = require('playwright');

// PWA Release real e API simulada em porta aleatória; não acessa nenhum banco.
(async () => {
    const root = path.resolve('.local-demo/cadastros/client/wwwroot');
    const id = '00000000-0000-0000-0000-000000000001';
    const piece = '00000000-0000-0000-0000-000000000002';
    const cause = '00000000-0000-0000-0000-000000000003';
    const technician = '00000000-0000-0000-0000-000000000004';
    const key = 'nexservice:offline-test:perfil@example.test:technical';
    const config = JSON.stringify({ ReadOnlyMode: false, TechnicalOfflineEnabled: true, StorageScope: 'offline-test' });
    const manifest = JSON.parse(fs.readFileSync(path.join(root, 'service-worker-assets.js'), 'utf8').replace(/^self.assetsManifest\s*=\s*/, '').replace(/;\s*$/, ''));
    const entry = manifest.assets.find(a => a.url === 'appsettings.json');
    entry.hash = 'sha256-' + createHash('sha256').update(config).digest('base64');
    manifest.version += '-offline-test';
    const state = { id, numeroOS: 'OS-OFFLINE', status: 'Aberta', prioridade: 'Alta', tipoManutencao: 'Corretiva', equipamentoNome: 'Teste', tecnicoId: technician, tecnicoNome: 'Técnico', dataAbertura: '2026-01-01T00:00:00Z', registros: [], pecas: [] };
    const receipts = new Map();
    let releaseDetails, notifyDetails;
    const detailsRequested = new Promise(resolve => { notifyDetails = resolve; });
    const detailsHold = new Promise(resolve => { releaseDetails = resolve; });
    let stock = 10, loseResponse = true, rejectEvents = false, databaseOffline = false, delayConfirmation = false;
    const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css' };
    const server = http.createServer(async (req, res) => {
        const pathname = new URL(req.url, 'http://localhost').pathname;
        const json = (value, code = 200) => { res.writeHead(code, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(value)); };
        if (pathname.startsWith('/api/')) {
            if (databaseOffline) return json({ title: 'Serviço temporariamente indisponível', detail: 'Sem conexão com o banco.' }, 503);
            if (req.method === 'POST') {
                const chunks = []; for await (const chunk of req) chunks.push(chunk);
                const e = JSON.parse(Buffer.concat(chunks).toString());
                if (rejectEvents) return json({ message: 'Ordem reatribuída; procure o gestor.' }, 409);
                if (receipts.has(e.id)) return json(receipts.get(e.id));
                if (e.acao === 'pecas') { stock -= e.quantidade; state.pecas.push({ pecaId: piece, nome: 'Peça', quantidade: e.quantidade }); }
                else {
                    state.status = { iniciar: 'EmAndamento', pausar: 'Pausada', retomar: 'EmAndamento', concluir: 'Concluida' }[e.acao];
                    state.registros.push({ tipo: { iniciar: 'Inicio', pausar: 'Pausa', retomar: 'Retomada', concluir: 'Conclusao' }[e.acao], timestamp: e.ocorridoEm, motivoPausa: e.motivo });
                    if (e.acao === 'concluir') state.dataConclusao = e.ocorridoEm;
                }
                receipts.set(e.id, structuredClone(state));
                if (loseResponse) { loseResponse = false; res.destroy(); return; }
                if (delayConfirmation) { delayConfirmation = false; await new Promise(resolve => setTimeout(resolve, 16000)); }
                return json(state);
            }
            if (pathname === '/api/ordensservico') return json([state]);
            if (pathname.includes('/api/ordensservico/')) { notifyDetails(); await detailsHold; return json(state); }
            if (pathname === '/api/pecas') return json([{ id: piece, nome: 'Peça', quantidadeEstoque: stock }]);
            if (pathname === '/api/causas-falha') return json([{ id: cause, descricao: 'Causa teste', ativo: true }]);
            return json([]);
        }
        if (pathname === '/appsettings.json') { res.writeHead(200, { 'Content-Type': 'application/json' }); return res.end(config); }
        if (pathname === '/service-worker-assets.js') { res.writeHead(200, { 'Content-Type': 'text/javascript' }); return res.end('self.assetsManifest = ' + JSON.stringify(manifest) + ';'); }
        const file = path.resolve(root, '.' + (pathname === '/' ? '/index.html' : decodeURIComponent(pathname)));
        if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404); return res.end(); }
        res.writeHead(200, { 'Content-Type': mime[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache', 'Blazor-Environment': 'Production' });
        fs.createReadStream(file).pipe(res);
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const origin = 'http://127.0.0.1:' + server.address().port;
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH || 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe' });
    try {
        const context = await browser.newContext();
        await context.addInitScript(() => {
            if (!sessionStorage.getItem('nexservice:offline-test:session')) sessionStorage.setItem('nexservice:offline-test:session', JSON.stringify({ token: 'mock', email: 'perfil@example.test', nome: 'Teste', papeis: ['Tecnico'], expiraEm: '2099-01-01T00:00:00Z' }));
        });
        const page = await context.newPage();
        await page.goto(origin);
        await detailsRequested;
        await page.getByLabel('Buscar equipamento ou número', { exact: true }).fill('OS-OFFLINE');
        assert(await page.getByRole('button', { name: 'Sair', exact: true }).isEnabled(), 'Preparação automática bloqueou a tela');
        releaseDetails();
        await page.getByRole('status').filter({ hasText: 'Seus atendimentos estão salvos para uso offline' }).waitFor();
        await page.evaluate(() => {
            const event = new Event('beforeinstallprompt', { cancelable: true });
            event.prompt = async () => { window.installPromptCalled = true; };
            event.userChoice = Promise.resolve({ outcome: 'dismissed' });
            dispatchEvent(event);
        });
        await page.getByRole('button', { name: 'Instalar NexService', exact: true }).click();
        assert.equal(await page.evaluate(() => window.installPromptCalled), true);
        assert.equal(await page.getByRole('button', { name: 'Preparar atendimentos offline', exact: true }).count(), 0);
        await page.evaluate(() => navigator.serviceWorker.ready.then(() => true));
        await page.waitForFunction(() => !!navigator.serviceWorker.controller);
        await context.setOffline(true);
        // Detalhes nunca foram abertos com rede: a preparação foi automática.
        await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        // Sem espaço: a ação não deve avançar a O.S. nem entrar na fila.
        await page.evaluate(() => {
            window.originalSet = Storage.prototype.setItem;
            Storage.prototype.setItem = function(key, value) { if (key.endsWith(':technical')) throw new DOMException('Sem espaço', 'QuotaExceededError'); return window.originalSet.call(this, key, value); };
        });
        await page.getByRole('button', { name: 'Iniciar', exact: true }).click();
        await page.getByRole('alert').filter({ hasText: 'Sem espaço' }).waitFor();
        assert.equal(await page.evaluate(key => JSON.parse(localStorage.getItem(key)).Events?.length ?? JSON.parse(localStorage.getItem(key)).events.length, key), 0);
        await page.evaluate(() => Storage.prototype.setItem = window.originalSet);
        await page.getByRole('button', { name: 'Iniciar', exact: true }).click();
        await page.getByLabel('Peça', { exact: true }).selectOption(piece);
        await page.getByLabel('Quantidade', { exact: true }).fill('2');
        await page.getByRole('button', { name: 'Registrar peça', exact: true }).click();
        await page.getByLabel('Motivo da pausa').fill('Aguardando liberação');
        await page.getByRole('button', { name: 'Pausar', exact: true }).click();
        await page.getByRole('button', { name: 'Retomar', exact: true }).click();
        await page.getByLabel('Causa da falha', { exact: true }).selectOption(cause);
        await page.getByRole('button', { name: 'Concluir', exact: true }).click();
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).events.length === 5, key);
        const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key);
        assert.equal(saved.events.length, 5); assert.equal(receipts.size, 0);
        await page.reload();
        await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        await page.getByText(/Atendimento salvo no dispositivo — pendente/).waitFor();
        assert.equal(await page.getByRole('button', { name: 'Iniciar', exact: true }).count(), 0);
        await context.setOffline(false);
        // Sem clicar em sincronizar: aguarda o temporizador, incluindo a resposta perdida.
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).events.length === 0, key, { timeout: 65000 });
        assert.equal(receipts.size, 5); assert.equal(stock, 8); assert.equal(state.status, 'Concluida');
        assert.deepEqual(state.registros.map(r => r.timestamp), saved.events.filter(e => e.acao !== 'pecas').map(e => e.ocorridoEm));
        console.log('PASS: PWA recarrega sem rede; cinco eventos persistidos; sincronização automática; perda da resposta não duplica estoque; horários preservados; armazenamento cheio não confirma ação.');
        state.status = 'Aberta'; state.registros = []; state.pecas = [];
        await page.reload();
        await page.getByRole('status').filter({ hasText: 'Seus atendimentos estão salvos para uso offline' }).waitFor();
        databaseOffline = true;
        assert.equal(await page.evaluate(() => navigator.onLine), true);
        await page.reload();
        await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        await page.getByRole('button', { name: 'Iniciar', exact: true }).click();
        await page.waitForFunction(key => {
            const s = JSON.parse(localStorage.getItem(key)); return s.events.length === 1 && s.error.includes('Sincronização aguardando conexão');
        }, key);
        assert.equal((await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key)).blocked, false);
        // Recupera também filas que a versão antiga bloqueava incorretamente ao receber HTTP 500.
        await page.evaluate(key => { const s = JSON.parse(localStorage.getItem(key)); s.blocked = true; s.error = 'Pendências preservadas. API 500: erro de banco'; localStorage.setItem(key, JSON.stringify(s)); }, key);
        await page.reload();
        await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        await page.getByLabel('Motivo da pausa').fill('Sem acesso ao banco');
        await page.getByRole('button', { name: 'Pausar', exact: true }).click();
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).events.length === 2, key);
        databaseOffline = false;
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).events.length === 0, key, { timeout: 45000 });
        assert.equal(state.status, 'Pausada');
        console.log('PASS: API acessível com banco offline usa cache, permite iniciar/pausar e sincroniza após recuperação; fila antiga com HTTP 500 é desbloqueada.');
        state.status = 'Aberta'; state.registros = []; state.pecas = [];
        await page.reload();
        await page.getByRole('status').filter({ hasText: 'Seus atendimentos estão salvos para uso offline' }).waitFor();
        await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        delayConfirmation = true;
        await page.getByRole('button', { name: 'Iniciar', exact: true }).click();
        await page.waitForFunction(key => {
            const s = JSON.parse(localStorage.getItem(key)); return s.events.length === 1 && s.error.includes('Sincronização aguardando conexão');
        }, key, { timeout: 25000 });
        assert.equal((await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key)).blocked, false);
        await page.getByLabel('Motivo da pausa').fill('Confirmação demorada');
        await page.getByRole('button', { name: 'Pausar', exact: true }).click();
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).events.length === 0, key, { timeout: 45000 });
        assert.equal(state.registros.filter(r => r.tipo === 'Inicio').length, 1);
        assert.equal(state.status, 'Pausada');
        console.log('PASS: confirmação após o prazo preserva evento, permite continuar o atendimento e reenvia sem duplicar o início.');
        state.status = 'Aberta'; state.registros = []; state.pecas = [];
        await page.reload();
        await page.getByRole('status').filter({ hasText: 'Seus atendimentos estão salvos para uso offline' }).waitFor();
        await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        const second = await context.newPage();
        await second.goto(origin);
        await second.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        await context.setOffline(true);
        await Promise.all([page, second].map(p => p.getByRole('button', { name: 'Iniciar', exact: true }).click()));
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).events.length === 1, key);
        // A outra tentativa usa o snapshot compartilhado e é recusada pela transição de status.
        await Promise.race([page, second].map(p => p.getByRole('alert').filter({ hasText: 'Só é possível iniciar' }).waitFor()));
        assert.equal((await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key)).events.length, 1);
        await second.close();
        rejectEvents = true;
        await context.setOffline(false);
        await page.getByRole('button', { name: 'Tentar sincronizar', exact: true }).click();
        await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).blocked, key);
        await page.reload();
        await page.getByText(/Pendências de atendimento preservadas/).waitFor();
        assert.equal((await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key)).events.length, 1);
        console.log('PASS: duas abas não duplicam o início; conflito do servidor preserva evento e mensagem após recarga.');
        // Uma nova conta não herda a fila nem os detalhes da anterior.
        await page.evaluate(() => sessionStorage.setItem('nexservice:offline-test:session', JSON.stringify({ token: 'mock', email: 'outra@example.test', nome: 'Outra', papeis: ['Tecnico'], expiraEm: '2099-01-01T00:00:00Z' })));
        await page.reload();
        await page.getByText('0 pendência(s) no dispositivo', { exact: true }).waitFor();
        assert.equal(await page.evaluate(() => localStorage.getItem('nexservice:offline-test:outra@example.test:technical') !== null), true);
        console.log('PASS: armazenamento isolado por conta.');
    } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
