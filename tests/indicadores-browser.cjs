const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const origin = process.env.INDICADORES_ORIGIN || 'http://127.0.0.1:5127';
const iso = value => value.toISOString().slice(0, 10);
const day = value => new Date(value + 'T00:00:00Z');
const add = (value, count) => new Date(value.getTime() + count * 86400000);
const summary = (total = 12) => ({ totalConcluidas: total, totalPreventivas: total ? 8 : 0, totalCorretivas: total ? 4 : 0,
  percentualPreventiva: total ? 66.7 : null, mttrMinutos: total ? 62.5 : null,
  amostraMttr: total ? 3 : 0, corretivasSemTempo: total ? 1 : 0, mtbfDias: total ? 4.2 : null, amostraIntervalos: total ? 2 : 0 });

(async () => {
  fs.mkdirSync('tests/artifacts/indicadores', { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
    const errors = [], queries = [];
    let writes = 0, mode = 'data';
    page.on('pageerror', error => errors.push(error.message));
    // Todas as APIs são interceptadas, inclusive o login. Não acessa o Supabase.
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url());
      let data = [];
      if (url.pathname === '/api/auth/login') data = { token: 'fixture-token', nome: 'Gestor de teste', email: 'gestor@example.test', papeis: ['Gestor'], expiraEm: '2099-12-31T23:59:59Z' };
      else if (request.method() !== 'GET') writes++;
      else if (url.pathname === '/api/equipamentos') data = [{ id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', nome: 'Esteira', setor: 'Produção', ativo: true }];
      else if (url.pathname === '/api/indicadores') {
        queries.push(url.searchParams);
        if (mode === 'error') return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ mensagem: 'Consulta indisponível para teste.' }) });
        const start = day(url.searchParams.get('dataInicio')), end = day(url.searchParams.get('dataFim'));
        const count = Math.round((end - start) / 86400000) + 1;
        const monthly = count > 31, evolution = [];
        for (let cursor = monthly ? new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), 1)) : start; cursor <= end;) {
          evolution.push({ data: iso(cursor), preventivas: 0, corretivas: 0 });
          cursor = monthly ? new Date(Date.UTC(cursor.getUTCFullYear(), cursor.getUTCMonth() + 1, 1)) : add(cursor, 1);
        }
        if (mode !== 'empty') {
          evolution[0].preventivas = 4; evolution[0].corretivas = 2;
          evolution[evolution.length - 1].preventivas += 4; evolution[evolution.length - 1].corretivas += 2;
        }
        data = { dataInicio: iso(start), dataFim: iso(end), setor: url.searchParams.get('setor') || null,
          dataInicioAnterior: iso(add(start, -count)), dataFimAnterior: iso(add(start, -1)),
          geral: summary(mode === 'empty' ? 0 : 12), anterior: { ...summary(), totalConcluidas: 10, percentualPreventiva: 50, mttrMinutos: 80 },
          setoresDisponiveis: ['Produção', 'Expedição'], evolucaoMensal: monthly, evolucao: evolution,
          porSetor: [{ ...summary(), setor: url.searchParams.get('setor') || 'Produção' }],
          equipamentos: [{ equipamentoId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', nome: 'Esteira de produção', setor: 'Produção', corretivas: 4, mttrMinutos: 62.5 }] };
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
    });
    await page.goto(origin);
    await page.locator('#login-email').fill('gestor@example.test');
    await page.locator('#login-password').fill('SomenteSimulacao!123');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await page.getByRole('heading', { name: 'Olá, Gestor.' }).waitFor();
    await page.getByRole('button', { name: 'Menu de navegação', exact: true }).click();
    await page.getByRole('navigation', { name: 'Áreas' }).getByRole('button', { name: 'Indicadores', exact: true }).click();
    await page.getByRole('heading', { name: 'Evolução das conclusões', exact: true }).waitFor();
    assert.equal(Math.round((day(queries[0].get('dataFim')) - day(queries[0].get('dataInicio'))) / 86400000), 29);
    assert.match(await page.locator('.indicator-metric').nth(1).innerText(), /\+16,7 p\.p\./);
    assert.match(await page.locator('.indicator-metric').nth(2).innerText(), /−17,5 min/);
    assert.match(await page.locator('.indicators-quality').innerText(), /1 corretiva/);
    assert.equal(await page.locator('.indicator-column').count(), 30);
    await page.getByText('Ver valores do gráfico', { exact: true }).click();
    assert.equal(await page.locator('.indicators-chart-data tbody tr').count(), 30);
    await page.getByText('Ver valores do gráfico', { exact: true }).click();
    for (const [label, width, height] of [['desktop', 1440, 1100], ['tablet', 768, 1024], ['mobile', 390, 844], ['small', 320, 720]]) {
      await page.setViewportSize({ width, height });
      await page.evaluate(() => window.scrollTo(0, 0));
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, `${label}: página com overflow horizontal`);
      await page.screenshot({ path: `tests/artifacts/indicadores/${label}.png`, fullPage: true });
    }
    await page.setViewportSize({ width: 1440, height: 1100 });
    for (const days of [7, 90]) {
      await page.getByRole('button', { name: `Últimos ${days} dias`, exact: true }).click();
      await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).waitFor();
      await page.waitForFunction(() => document.querySelector('.indicators').getAttribute('aria-busy') === 'false');
      const query = queries.at(-1);
      assert.equal(Math.round((day(query.get('dataFim')) - day(query.get('dataInicio'))) / 86400000), days - 1);
      if (days === 7) assert.equal(await page.locator('.indicator-column').count(), 7);
      if (days === 90) assert.match(await page.locator('.indicators-trend figcaption').innerText(), /Por mês/);
    }
    await page.getByLabel('Setor', { exact: true }).selectOption('Produção');
    await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.indicators-context').textContent.includes('Produção'));
    assert.equal(queries.at(-1).get('setor'), 'Produção');
    const beforeInvalid = queries.length;
    await page.getByLabel('Data inicial', { exact: true }).fill('2027-12-31');
    await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'A data inicial deve ser menor' }).waitFor();
    assert.equal(queries.length, beforeInvalid, 'Período inválido foi enviado à API');
    mode = 'empty';
    await page.getByRole('button', { name: 'Últimos 7 dias', exact: true }).click();
    await page.getByRole('heading', { name: 'Nenhuma O.S. concluída neste período', exact: true }).waitFor();
    assert.match(await page.locator('.indicator-metric').nth(2).innerText(), /— min/);
    mode = 'error';
    const priorContext = await page.locator('.indicators-context').innerText();
    await page.getByRole('button', { name: 'Últimos 30 dias', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'O resultado da consulta anterior continua exibido' }).waitFor();
    assert.equal(await page.locator('.indicators-context').innerText(), priorContext);
    assert.deepEqual(errors, []);
    assert.equal(writes, 0);
    console.log('PASS: métricas e comparação, gráfico e tabela acessível, filtros, 7/30/90 dias, quatro resoluções, vazio, erro e preservação das datas; nenhuma API real acessada.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
