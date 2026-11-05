const assert = require('node:assert/strict');
const { chromium } = require('playwright');

// API simulada: verifica as operações visíveis sem gravar dados.
(async () => {
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH || 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe' });
    try {
        for (const role of ['Operador', 'Tecnico', 'Gestor']) {
            const page = await browser.newPage({ serviceWorkers: 'block' });
            let status = 'Aberta';
            let failStart = role === 'Tecnico';
            const requests = [];
            await page.addInitScript(role => sessionStorage.setItem('nexservice:session', JSON.stringify({ token: 'mock', email: 'perfil@example.test', nome: 'Teste', papeis: [role], expiraEm: '2099-01-01T00:00:00Z' })), role);
            await page.route('**/api/**', route => {
                const url = route.request().url();
                requests.push(url);
                if (failStart && route.request().method() === 'POST' && url.endsWith('/iniciar')) {
                    failStart = false;
                    return route.fulfill({ status: 503, contentType: 'text/plain', body: 'Falha simulada ao iniciar' });
                }
                if (route.request().method() === 'POST') {
                    status = url.endsWith('/iniciar') || url.endsWith('/retomar') ? 'EmAndamento'
                        : url.endsWith('/pausar') ? 'Pausada' : url.endsWith('/concluir') ? 'Concluida' : status;
                }
                const body = /ordensservico\//.test(url) ? {
                    id: '00000000-0000-0000-0000-000000000001', numeroOS: 'OS-TESTE', status, prioridade: 'Alta', tipoManutencao: 'Corretiva', equipamentoNome: 'Equipamento teste', registros: [], pecas: [], dataAbertura: '2026-01-01T00:00:00Z'
                } : url.includes('ordensservico?') ? [{ id: '00000000-0000-0000-0000-000000000001', numeroOS: 'OS-TESTE', status, prioridade: 'Alta', equipamentoNome: 'Equipamento teste', dataAbertura: '2026-01-01T00:00:00Z' }]
                    : url.includes('api/pecas') ? [{ id: '00000000-0000-0000-0000-000000000002', nome: 'Peça teste', quantidadeEstoque: 10 }]
                    : url.includes('api/causas-falha') ? [{ id: '00000000-0000-0000-0000-000000000003', descricao: 'Causa teste' }] : [];
                return route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) });
            });
            await page.goto('http://127.0.0.1:5126');
            await page.getByRole('button', { name: 'Ver detalhes', exact: true }).click();
            assert.equal(await page.locator('summary').filter({ hasText: 'Abrir chamado' }).count(), role === 'Tecnico' ? 0 : 1);
            assert.equal(await page.getByRole('button', { name: 'Iniciar', exact: true }).count(), role === 'Tecnico' ? 1 : 0);
            assert.equal(await page.getByRole('button', { name: 'Registrar peça', exact: true }).count(), role === 'Tecnico' ? 1 : 0);
            assert.equal(await page.getByRole('button', { name: 'Atribuir técnico', exact: true }).count(), role === 'Gestor' ? 1 : 0);
            assert.equal(await page.getByRole('button', { name: 'Usuários', exact: true }).count(), role === 'Gestor' ? 1 : 0);
            if (role === 'Operador') {
                assert.equal(await page.getByRole('button', { name: 'Equipamentos e peças', exact: true }).count(), 0);
                assert.equal(await page.getByRole('button', { name: 'Preventivas', exact: true }).count(), 0);
                assert(!requests.some(url => /api\/(pecas|causas-falha|planos-preventivos)/.test(url)), 'Operador recebeu dados de manutenção');
            }
            if (role === 'Tecnico') {
                const button = name => page.getByRole('button', { name, exact: true });
                await button('Iniciar').click();
                await page.getByRole('alert').filter({ hasText: 'Falha simulada ao iniciar' }).waitFor();
                assert(await button('Iniciar').isEnabled(), 'Falha no início deixou a tela bloqueada');
                assert(await button('Sair').isEnabled(), 'Falha no início bloqueou a navegação');
                const beforeStart = requests.length;
                await button('Iniciar').click();
                await page.getByLabel('Motivo da pausa').fill('Aguardando liberação');
                assert.deepEqual(requests.slice(beforeStart).map(url => new URL(url).pathname), ['/api/ordensservico/00000000-0000-0000-0000-000000000001/iniciar'], 'Início recarregou consultas que bloqueiam a tela');
                await button('Pausar').click();
                await page.waitForFunction(() => [...document.querySelectorAll('button')].some(b => b.textContent === 'Retomar' && !b.disabled));
                assert(await button('Concluir').isDisabled(), 'Conclusão permitida durante pausa');
                await button('Retomar').click();
                await page.getByLabel('Peça', { exact: true }).selectOption({ index: 1 });
                await button('Registrar peça').click();
                await page.getByLabel('Causa da falha', { exact: true }).selectOption({ index: 1 });
                await button('Concluir').click();
                await page.getByRole('heading', { name: 'Atendimento técnico', exact: true }).waitFor({ state: 'detached' });
                assert.equal(status, 'Concluida');
            }
            console.log('PASS: operações visíveis para ' + role);
            await page.close();
        }
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
