const assert = require('node:assert/strict');
const fs = require('node:fs');
const { randomUUID } = require('node:crypto');
const { chromium } = require('playwright');

// Fixture criada exclusivamente no banco local isolado (5100/5126).
(async () => {
    const api = 'http://127.0.0.1:5100';
    const credentials = JSON.parse(fs.readFileSync('.local-demo/cadastros/credentials.json', 'utf8').replace(/^\uFEFF/, ''));
    async function request(path, token, body) {
        const r = await fetch(api + path, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(20000) });
        assert(r.ok, `HTTP ${r.status} em ${path}`);
        return r.json();
    }
    assert.equal((await request('/api/runtime')).readOnly, false);
    const admin = await request('/api/auth/login', null, { email: credentials.Email, senha: credentials.Password });
    const technician = await request('/api/auth/login', null, { email: 'carlos.tecnico@sustennutri.com.br', senha: credentials.Password });
    const users = await request('/api/usuarios?tipo=Tecnico', admin.token);
    const equipment = await request('/api/equipamentos', admin.token);
    const order = await request('/api/ordensservico', admin.token, { equipamentoId: equipment[0].id, descricaoProblema: 'Teste local iniciar ' + randomUUID(), prioridade: 'Alta' });
    const userId = JSON.parse(Buffer.from(technician.token.split('.')[1], 'base64url')).sub;
    assert(users.some(u => u.id === userId));
    await request(`/api/ordensservico/${order.id}/atribuir`, admin.token, { tecnicoId: userId });
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH || 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe' });
    try {
        const page = await browser.newPage({ serviceWorkers: 'block' });
        await page.addInitScript(session => sessionStorage.setItem('nexservice:session', JSON.stringify(session)), technician);
        await page.goto('http://127.0.0.1:5126');
        const card = page.locator('article').filter({ hasText: order.numeroOS });
        await card.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
        await page.getByRole('button', { name: 'Iniciar', exact: true }).click();
        await page.getByLabel('Motivo da pausa').fill('Pausa de teste local');
        await page.getByRole('button', { name: 'Pausar', exact: true }).click();
        await page.getByRole('button', { name: 'Retomar', exact: true }).click();
        await page.getByLabel('Causa da falha', { exact: true }).selectOption({ index: 1 });
        await page.getByRole('button', { name: 'Concluir', exact: true }).click();
        await page.getByRole('heading', { name: 'Atendimento técnico', exact: true }).waitFor({ state: 'detached' });
        const result = await request(`/api/ordensservico/${order.id}`, technician.token);
        assert.equal(result.status, 'Concluida');
        assert.deepEqual(result.registros.map(r => r.tipo), ['Inicio', 'Pausa', 'Retomada', 'Conclusao']);
        console.log('PASS: iniciar, preencher motivo, pausar, retomar e concluir pela interface com API/PostgreSQL locais reais.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
