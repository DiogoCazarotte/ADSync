const assert = require('node:assert/strict');
const { chromium } = require('playwright');

// All API responses are simulated; no accounts or orders are written.
(async () => {
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH || 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe' });
    let release;
    const pending = new Promise(resolve => { release = resolve; });
    try {
        const page = await browser.newPage({ serviceWorkers: 'block' });
        await page.addInitScript(() => {
            sessionStorage.setItem('nexservice:session', JSON.stringify({ token: 'mock', nome: 'Gestor teste', email: 'gestor@example.test', papeis: ['Gestor'], expiraEm: '2099-01-01T00:00:00Z' }));
            localStorage.setItem('nexservice:gestor@example.test:queue', JSON.stringify([{ descricaoProblema: 'Teste simulado' }]));
        });
        await page.route('**/api/**', async route => {
            if (route.request().method() === 'POST') {
                await pending;
                return route.fulfill({ status: 503, contentType: 'text/plain', body: 'Falha simulada' }).catch(() => {});
            }
            await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
        });
        await page.goto('http://127.0.0.1:5126');
        await page.getByRole('button', { name: 'Usuários', exact: true }).click();
        const email = page.getByLabel('E-mail do usuário');
        await email.fill('rascunho@example.test');
        await page.waitForRequest(r => r.method() === 'POST' && r.url().includes('/api/ordensservico'), { timeout: 30000 });
        assert(await email.isEnabled(), 'Sincronização automática bloqueou o e-mail');
        assert(await page.getByRole('button', { name: 'Salvar usuário', exact: true }).isEnabled());
        await email.fill('durante-sync@example.test');
        await page.getByRole('button', { name: 'Cancelar edição', exact: true }).click();
        assert.equal(await email.inputValue(), '');
        release();
        await page.getByText(/pendência\(s\) preservada/).waitFor();
        await email.fill('depois-sync@example.test');
        assert(await page.getByRole('button', { name: 'Salvar usuário', exact: true }).isEnabled());
        console.log('PASS: e-mail, salvar e cancelar disponíveis durante e após falha da sincronização automática.');
    } finally {
        release();
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
