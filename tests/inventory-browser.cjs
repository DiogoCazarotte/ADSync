const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const baseUrl = process.env.INVENTORY_URL || 'http://localhost:5125';
const equipment = [
  { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', nome: 'Esteira de produção', codigoPatrimonio: 'EQ-001', setor: 'Produção', ativo: true },
  { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', nome: 'Compressor de ar', codigoPatrimonio: 'EQ-002', setor: 'Utilidades', ativo: false },
  { id: 'cccccccc-cccc-cccc-cccc-cccccccccccc', nome: 'Compressor auxiliar', codigoPatrimonio: 'EQ-003', setor: 'Utilidades', ativo: true }
];
const parts = [{ id: 'dddddddd-dddd-dddd-dddd-dddddddddddd', codigo: 'PC-001', nome: 'Rolamento do motor', quantidadeEstoque: 12, critica: true }];
const orders = [{ id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', numeroOS: 'OS-042', equipamentoNome: equipment[0].nome, descricaoProblema: '<img src=x onerror="window.inventoryXss=true">', status: 'EmAndamento', prioridade: 'Alta', dataAbertura: '2026-11-20T10:00:00Z' }];

(async () => {
  const browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL || 'msedge', headless: true });
  fs.mkdirSync('tests/artifacts/inventory', { recursive: true });
  try {
    for (const [width, role, mode] of [[1440, 'Gestor', 'normal'], [390, 'Gestor', 'normal'], [320, 'Tecnico', 'normal'], [390, 'Gestor', 'empty'], [1440, 'Gestor', 'failure']]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 }, serviceWorkers: 'block' });
      const page = await context.newPage();
      page.setDefaultTimeout(10000);
      const errors = [], requests = [];
      let updated = false;
      page.on('pageerror', error => errors.push(error.message));
      await page.route('**/appsettings*.json', route => route.fulfill({ json: { ApiBaseUrl: 'http://localhost:5099/', ReadOnlyMode: role === 'Tecnico', TechnicalOfflineEnabled: false, StorageScope: 'inventory-test' } }));
      await page.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        requests.push({ url, method: route.request().method() });
        let data = [];
        if (url.pathname === '/api/auth/login') data = { token: 'mock', nome: 'Diogo teste', email: 'test@example.test', papeis: [role], expiraEm: '2099-12-31T23:59:59Z' };
        else if (mode !== 'empty' && url.pathname === '/api/equipamentos') {
          if (mode === 'failure' && url.searchParams.has('incluirInativos')) return route.fulfill({ status: 503, json: { detail: 'Consulta indisponível.' } });
          data = equipment.filter(item => item.ativo || (role === 'Gestor' && url.searchParams.get('incluirInativos') === 'true'));
          if (updated) data = data.map(item => item.id === equipment[1].id ? { ...item, nome: 'Compressor revisado' } : item);
        }
        else if (mode !== 'empty' && url.pathname === '/api/pecas') data = parts;
        else if (mode !== 'empty' && url.pathname === '/api/ordensservico') data = orders;
        else if (url.pathname === '/api/runtime') data = { readOnly: role === 'Tecnico', seedEnabled: false, preventiveEnabled: false };
        await route.fulfill({ json: data });
      });
      const navigate = async name => {
        await page.getByRole('button', { name: 'Menu de navegação', exact: true }).click();
        await page.getByRole('navigation').getByRole('button', { name, exact: true }).click();
      };
      await page.goto(baseUrl);
      await page.locator('#login-email').fill('test@example.test');
      await page.locator('#login-password').fill('Visual123!');
      await page.getByRole('button', { name: 'Entrar', exact: true }).click();
      await page.getByRole('heading', { name: 'Olá, Diogo.' }).waitFor();
      await navigate('Equipamentos e peças');
      await page.locator('[data-v-app]').waitFor();
      const rows = page.locator('.inventory-equipment-table tbody tr');
      const search = page.getByLabel('Buscar equipamento', { exact: true });
      const status = page.getByLabel('Situação da máquina', { exact: true });
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Page overflow');
      if (mode === 'empty') {
        await page.getByText('Nenhum equipamento disponível.', { exact: true }).waitFor();
        await page.getByText('Nenhuma peça disponível.', { exact: true }).waitFor();
      } else if (mode === 'failure') {
        await page.getByRole('alert').filter({ hasText: 'Não foi possível consultar as máquinas inativas.' }).waitFor();
        assert.equal(await rows.count(), 2);
        assert.equal(await status.locator('option[value="inactive"]').count(), 0);
      } else {
        const initialRequests = requests.length;
        await search.pressSequentially('producao');
        assert.equal(await rows.count(), 1);
        assert.match(await rows.innerText(), /Esteira de produção/);
        await page.locator('.inventory-history-button').click();
        const modal = page.getByRole('dialog');
        await modal.waitFor();
        await modal.getByText('Em andamento', { exact: true }).waitFor();
        assert.equal(await modal.locator('img').count(), 0, 'History must escape HTML');
        assert.equal(await page.evaluate(() => window.inventoryXss), undefined);
        await page.keyboard.press('Escape');
        await modal.waitFor({ state: 'hidden' });
        assert.equal(await search.inputValue(), 'producao', 'Preserve filters across modal render');
        await page.getByRole('button', { name: 'Limpar filtros', exact: true }).click();
        await search.fill('COMPRESSOR');
        await status.selectOption('active');
        assert.equal(await rows.count(), 1);
        assert.match(await rows.innerText(), /Compressor auxiliar/);
        if (role === 'Gestor') {
          await status.selectOption('inactive');
          assert.equal(await rows.count(), 1);
          assert.match(await rows.innerText(), /Compressor de ar/);
          assert.equal(requests.length, initialRequests, 'Typing and filtering must not request the API');
          if (width === 1440) {
            updated = true;
            await page.getByRole('button', { name: 'Atualizar consultas', exact: true }).click();
            await page.getByRole('rowheader', { name: 'Compressor revisado', exact: true }).waitFor();
            assert.equal(await search.inputValue(), 'COMPRESSOR');
            assert.equal(await status.inputValue(), 'inactive');
          }
        } else {
          assert.equal(await status.locator('option[value="inactive"]').count(), 0);
          assert(!requests.some(r => r.url.searchParams.has('incluirInativos')), 'Technicians must not request inactive equipment');
        }
        await search.fill('sem-resultado');
        await page.getByText('Nenhum equipamento encontrado.', { exact: false }).waitFor();
        await page.getByRole('button', { name: 'Limpar filtros', exact: true }).click();
        await page.getByLabel('Buscar peça', { exact: true }).fill('pc-001');
        assert.equal(await rows.count(), role === 'Gestor' ? 3 : 2, 'Independent part search');
        await page.getByLabel('Buscar peça', { exact: true }).fill('inexistente');
        await page.getByText('Nenhuma peça encontrada.', { exact: false }).waitFor();
        await page.getByLabel('Buscar peça', { exact: true }).fill('');
        const oldHost = await page.locator('[data-v-app]').elementHandle();
        await navigate('Visão geral');
        await page.waitForFunction(host => !host.__vue_app__, oldHost);
        await navigate('Equipamentos e peças');
        await page.locator('[data-v-app]').waitFor();
        assert.equal(await search.inputValue(), '');
        await status.selectOption('active');
        assert.equal(await rows.count(), 2, 'Filters work after remount');
      }
      await page.screenshot({ path: `tests/artifacts/inventory/${width}-${role}-${mode}.png`, fullPage: true });
      assert.deepEqual(errors, []);
      assert(requests.every(r => r.method === 'GET' || r.url.pathname === '/api/auth/login'), 'Unexpected write request');
      console.log(`PASS ${width}px ${role} ${mode}: Vue search, status, permissions, lifecycle and mocked API.`);
      await context.close();
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
