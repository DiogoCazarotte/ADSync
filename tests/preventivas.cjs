// Interface Release com API simulada: não conecta ao Supabase.
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

(async () => {
    const root = path.resolve('.local-demo/preventivas/client/wwwroot');
    const equipmentId = '00000000-0000-0000-0000-000000000001';
    const technicianId = '00000000-0000-0000-0000-000000000002';
    const plans = [];
    let reject = false, writes = 0;
    const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css' };
    const server = http.createServer(async (req, res) => {
        const url = new URL(req.url, 'http://localhost');
        const json = (data, status = 200) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); };
        if (url.pathname === '/appsettings.json') return json({ StorageScope: 'preventive-test', TechnicalOfflineEnabled: false, ReadOnlyMode: false });
        if (url.pathname.startsWith('/api/')) {
            if (req.method !== 'GET') {
                writes++;
                const chunks = []; for await (const chunk of req) chunks.push(chunk);
                const dto = JSON.parse(Buffer.concat(chunks).toString());
                if (reject) return json({ mensagem: 'Alteração recusada para teste' }, 400);
                if (req.method === 'PUT') Object.assign(plans[0], dto);
                else if (url.pathname.endsWith('/inativar')) plans[0].ativo = false;
                else if (url.pathname.endsWith('/ativar')) plans[0].ativo = true;
                else plans.push({ ...dto, id: '00000000-0000-0000-0000-000000000003', ativo: true, equipamentoNome: 'Bomba teste', tecnicoResponsavelNome: 'Técnico teste' });
                return json(plans[0]);
            }
            if (url.pathname === '/api/equipamentos') return json([{ id: equipmentId, nome: 'Bomba teste' }]);
            if (url.pathname === '/api/usuarios') return json([{ id: technicianId, nome: 'Técnico teste' }]);
            if (url.pathname === '/api/planos-preventivos') return json(plans.filter(p => p.ativo || url.searchParams.get('incluirInativos') === 'true'));
            return json([]);
        }
        const file = path.resolve(root, '.' + (url.pathname === '/' ? '/index.html' : decodeURIComponent(url.pathname)));
        if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404); return res.end(); }
        res.writeHead(200, { 'Content-Type': mime[path.extname(file)] || 'application/octet-stream', 'Blazor-Environment': 'Production' });
        fs.createReadStream(file).pipe(res);
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser;
    try {
        browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH || 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe' });
        const context = await browser.newContext({ serviceWorkers: 'block', viewport: { width: 390, height: 844 } });
        await context.addInitScript(() => sessionStorage.setItem('nexservice:preventive-test:session', JSON.stringify({ token: 'mock', email: 'gestor@example.test', nome: 'Gestor teste', papeis: ['Gestor'], expiraEm: '2099-01-01T00:00:00Z' })));
        const page = await context.newPage();
        await page.goto('http://127.0.0.1:' + server.address().port);
        await page.getByRole('button', { name: 'Preventivas', exact: true }).click();
        await page.getByRole('button', { name: 'Criar plano', exact: true }).click();
        await page.getByText('Informe a tarefa.', { exact: true }).waitFor();
        assert.equal(writes, 0);
        await page.getByLabel('Equipamento do plano', { exact: true }).selectOption(equipmentId);
        await page.getByLabel('Técnico responsável pelo plano', { exact: true }).selectOption(technicianId);
        await page.getByLabel('Tarefa', { exact: true }).fill('Lubrificar bomba');
        await page.getByLabel('Tarefa', { exact: true }).blur();
        await page.locator('.validation-summary-errors').waitFor({ state: 'hidden' });
        await page.getByRole('button', { name: 'Criar plano', exact: true }).click();
        await page.getByText('Plano confirmado pelo servidor.', { exact: true }).waitFor();
        const card = page.getByRole('article', { name: 'Plano preventivo', exact: true });
        await card.getByText('Lubrificar bomba', { exact: true }).waitFor();
        await card.getByRole('button', { name: 'Editar plano', exact: true }).click();
        await page.getByLabel('Tarefa', { exact: true }).fill('Trocar filtro');
        reject = true;
        await page.getByRole('button', { name: 'Salvar alterações', exact: true }).click();
        await page.getByRole('alert').filter({ hasText: 'Alteração recusada' }).waitFor();
        assert.equal(await page.getByLabel('Tarefa', { exact: true }).inputValue(), 'Trocar filtro');
        reject = false;
        await page.getByRole('button', { name: 'Salvar alterações', exact: true }).click();
        await card.getByText('Trocar filtro', { exact: true }).waitFor();
        await card.getByRole('button', { name: 'Inativar plano', exact: true }).click();
        await card.getByText('Inativo', { exact: true }).waitFor();
        await page.getByLabel('Situação do plano', { exact: true }).selectOption('ativo');
        await page.getByText('Nenhum plano disponível nos filtros atuais.', { exact: true }).waitFor();
        await page.getByLabel('Situação do plano', { exact: true }).selectOption('inativo');
        await card.getByRole('button', { name: 'Ativar plano', exact: true }).click();
        await page.getByLabel('Situação do plano', { exact: true }).selectOption('');
        await card.getByText('Ativo', { exact: true }).waitFor();
        await card.getByRole('button', { name: 'Editar plano', exact: true }).click();
        await page.getByLabel('Tarefa', { exact: true }).fill('Descartar edição');
        await page.getByRole('button', { name: 'Cancelar edição', exact: true }).click();
        assert.equal(await page.getByLabel('Tarefa', { exact: true }).inputValue(), '');
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        fs.mkdirSync('tests/artifacts', { recursive: true });
        await page.screenshot({ path: 'tests/artifacts/frontend-mobile.png', fullPage: true });
        assert.equal(await page.locator('.app-header').evaluate(e => getComputedStyle(e).backgroundColor), 'rgb(10, 57, 51)');
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.screenshot({ path: 'tests/artifacts/frontend-desktop.png', fullPage: true });
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        // O técnico consulta os planos, sem acesso aos controles de gestão.
        const tech = await browser.newContext({ serviceWorkers: 'block' });
        await tech.addInitScript(() => sessionStorage.setItem('nexservice:preventive-test:session', JSON.stringify({ token: 'mock', email: 'tecnico@example.test', nome: 'Técnico teste', papeis: ['Tecnico'], expiraEm: '2099-01-01T00:00:00Z' })));
        const techPage = await tech.newPage();
        await techPage.goto('http://127.0.0.1:' + server.address().port);
        await techPage.getByRole('button', { name: 'Preventivas', exact: true }).click();
        await techPage.getByRole('article', { name: 'Plano preventivo', exact: true }).waitFor();
        assert.equal(await techPage.getByRole('button', { name: /Criar plano|Editar plano|Inativar plano|Ativar plano/ }).count(), 0);
        const guest = await browser.newContext({ serviceWorkers: 'block', viewport: { width: 390, height: 844 } });
        const loginPage = await guest.newPage();
        await loginPage.goto('http://127.0.0.1:' + server.address().port);
        await loginPage.getByRole('button', { name: 'Entrar', exact: true }).waitFor();
        assert(await loginPage.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await loginPage.screenshot({ path: 'tests/artifacts/frontend-login.png', fullPage: true });
        console.log('PASS: criação, edição, erro sem perda do formulário, cancelamento, ativação/inativação, filtros, tela móvel e consulta do técnico.');
    } finally { if (browser) await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
