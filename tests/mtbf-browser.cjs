const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const origin = process.env.INDICADORES_ORIGIN || 'http://127.0.0.1:5127';
const id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const errors = [], records = new Map();
    let reject = false, writes = 0;
    page.on('pageerror', error => errors.push(error.message));
    // Todas as APIs são simuladas; nunca consulta ou grava no Supabase.
    await page.route('**/api/**', async route => {
      const req = route.request(), url = new URL(req.url());
      let data = [];
      if (url.pathname === '/api/auth/login') data = { token: 'fixture-token', nome: 'Gestor', email: 'mtbf@example.test', papeis: ['Gestor'], expiraEm: '2099-12-31T23:59:59Z' };
      else if (url.pathname === '/api/equipamentos') data = [{ id, nome: 'Costura 1', codigoPatrimonio: 'C01', setor: 'Costura', ativo: true }];
      else if (url.pathname === '/api/operacao-diaria') data = [...records.values()].filter(r => r.data === url.searchParams.get('dataInicio'));
      else if (url.pathname.startsWith('/api/operacao-diaria/')) {
        assert.equal(req.method(), 'PUT'); writes++;
        if (reject) return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ mensagem: 'Falha simulada.' }) });
        const date = url.pathname.split('/').at(-1);
        data = { equipamentoId: id, equipamentoNome: 'Costura 1', data: date, horasOperadas: req.postDataJSON().horasOperadas };
        records.set(date, data);
      }
      else if (url.pathname === '/api/indicadores') data = {
        mtbfHabilitado: true, dataInicio: url.searchParams.get('dataInicio'), dataFim: url.searchParams.get('dataFim'),
        geral: { totalConcluidas: 0, horasOperadas: 16, falhasMtbf: 2, equipamentosMtbf: 1, equipamentosSemCobertura: 1, mtbfHoras: 8 },
        setoresDisponiveis: ['Costura'], porSetor: [], equipamentos: [], evolucao: []
      };
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
    });
    await page.goto(origin);
    await page.locator('#login-email').fill('mtbf@example.test');
    await page.locator('#login-password').fill('SomenteSimulacao!123');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await page.getByRole('heading', { name: 'Olá, Gestor.' }).waitFor();
    await page.getByRole('button', { name: 'Menu de navegação', exact: true }).click();
    await page.getByRole('navigation', { name: 'Áreas' }).getByRole('button', { name: 'Indicadores', exact: true }).click();
    const mtbf = page.getByRole('region', { name: 'MTBF por horas de operação' });
    await mtbf.getByRole('heading', { name: 'MTBF · horas de operação' }).waitFor();
    assert.match(await mtbf.innerText(), /8,0 h/);
    assert.match(await mtbf.innerText(), /1 equipamento\(s\) excluído/);
    await page.getByRole('button', { name: 'Registrar', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Registrar', exact: true }).click();
    await page.getByLabel('Horas operadas', { exact: true }).fill('8');
    await page.getByRole('button', { name: 'Salvar horas do dia', exact: true }).click();
    await page.getByRole('button', { name: 'Corrigir', exact: true }).waitFor();
    assert.equal(records.size, 1);
    await page.getByRole('button', { name: 'Corrigir', exact: true }).click();
    await page.getByLabel('Horas operadas', { exact: true }).fill('10');
    await page.getByRole('button', { name: 'Salvar horas do dia', exact: true }).click();
    await page.waitForFunction(() => [...document.querySelectorAll('td')].some(td => td.textContent === '10,00'));
    assert.equal(records.size, 1); assert.equal([...records.values()][0].horasOperadas, 10);
    reject = true;
    await page.getByLabel('Horas operadas', { exact: true }).fill('12');
    await page.getByRole('button', { name: 'Salvar horas do dia', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'o formulário foi preservado' }).waitFor();
    assert.equal(await page.getByLabel('Horas operadas', { exact: true }).inputValue(), '12');
    assert.equal([...records.values()][0].horasOperadas, 10);
    for (const width of [320, 390, 768, 1440]) {
      await page.setViewportSize({ width, height: 1000 });
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Overflow em ${width}px`);
    }
    assert.equal(writes, 3); assert.deepEqual(errors, []);
    console.log('PASS: MTBF sem conclusões, cobertura, registro/correção, erro preservando formulário e quatro resoluções.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
