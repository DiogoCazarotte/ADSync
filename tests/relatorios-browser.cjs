const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const origin = process.env.RELATORIOS_ORIGIN || 'http://127.0.0.1:5128';
const summary = { concluidas: 3, corretivas: 2, preventivas: 1, percentualPreventiva: 33.3, horasTrabalhadas: 1.5,
  ordensComTempoValido: 2, ordensSemTempoValido: 1, mttrMinutos: 45, amostraMttr: 2 };
const part = { pecaId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', codigo: 'P-01', nome: 'Rolamento', quantidade: 2 };
function report(year, month, empty) {
  const start = `${year}-${String(month).padStart(2, '0')}-01`;
  const end = new Date(Date.UTC(year, month, 0)).toISOString().slice(0, 10);
  return { dataInicio: start, dataFim: end, geradoEm: '2026-11-13T15:00:00Z', mesEmAndamento: true, ordensAbertasNoMes: 4,
    resumo: empty ? { concluidas: 0, corretivas: 0, preventivas: 0, percentualPreventiva: null, horasTrabalhadas: null,
      ordensComTempoValido: 0, ordensSemTempoValido: 0, mttrMinutos: null, amostraMttr: 0 } : summary,
    mesAnterior: { ...summary, concluidas: 2 }, pendentesAtuais: 1,
    atividades: empty ? [] : [{ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', numero: 'OS-001', equipamento: 'Esteira', patrimonio: 'E-01', setor: 'Produção',
      tipo: 'Corretiva', tecnico: 'Ana', concluidaEm: start + 'T12:00:00Z', descricao: 'Inspeção e troca do rolamento', causa: 'Desgaste',
      observacoes: 'Teste de funcionamento realizado. <script>window.injected = true</script>', minutosTrabalhados: 45, pecas: [part] }],
    setores: empty ? [] : [{ setor: 'Produção', corretivas: 2, preventivas: 1 }], pecas: empty ? [] : [part],
    recomendacoes: empty ? [] : [{ prioridade: 'Atenção', titulo: 'Investigar recorrência: Esteira', evidencia: '2 corretivas concluídas no mês.', acao: 'Revisar o plano preventivo ativo.' }],
    agendaAtual: [{ equipamento: 'Esteira', tarefa: 'Lubrificação', responsavel: 'Ana', proximaExecucao: '2026-11-12', vencida: true }] };
}

(async () => {
  fs.mkdirSync('tests/artifacts/relatorios', { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
    const errors = [], queries = [];
    let writes = 0, mode = 'data', role = 'Gestor';
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => { window.print = () => { window.__reportPrinted = true; }; });
    // Intercept every API call; neither the login nor the report reaches Supabase.
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url());
      let data = [];
      if (url.pathname === '/api/auth/login') data = { token: 'fixture-token', nome: 'Gestor de teste', email: 'gestor@example.test', papeis: [role], expiraEm: '2099-12-31T23:59:59Z' };
      else if (request.method() !== 'GET') writes++;
      else if (url.pathname === '/api/relatorios/mensal') {
        queries.push(url.search);
        if (mode === 'error') return route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ mensagem: 'Consulta indisponível.' }) });
        data = report(+url.searchParams.get('ano'), +url.searchParams.get('mes'), mode === 'empty');
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
    });
    async function login() {
      await page.locator('#login-email').fill('gestor@example.test');
      await page.locator('#login-password').fill('SomenteSimulacao!123');
      await page.getByRole('button', { name: 'Entrar', exact: true }).click();
      await page.getByRole('heading', { name: 'Olá, Gestor.' }).waitFor();
    }
    await page.goto(origin);
    await login();
    await page.getByRole('button', { name: 'Menu de navegação', exact: true }).click();
    await page.getByRole('navigation', { name: 'Áreas' }).getByRole('button', { name: 'Relatórios', exact: true }).click();
    await page.getByRole('heading', { name: '02 · Manutenções realizadas', exact: true }).waitFor();
    assert.match(await page.locator('.report-cover').innerText(), /Mês em andamento/);
    assert.equal(await page.locator('.report-estimate').count(), 0);
    assert.match(await page.getByRole('region', { name: 'Estimativa de perdas evitadas', exact: true }).innerText(), /Estimativa não calculada/);
    assert.equal(await page.evaluate(() => window.injected), undefined);
    await page.getByLabel('Custo estimado de parada (R$/h)', { exact: true }).fill('500');
    await page.getByLabel('Horas de parada estimadas como evitadas', { exact: true }).fill('4');
    await page.getByRole('heading', { name: '06 · Estimativa de perdas evitadas', exact: true }).click();
    assert.equal(await page.locator('.report-estimate').count(), 0, 'Valores sem justificativa geraram estimativa.');
    await page.getByLabel('Fonte e justificativa das premissas', { exact: true }).fill('OS-001; estimativa do gestor conforme orçamento interno.');
    await page.locator('.report-estimate').waitFor();
    assert.match(await page.locator('.report-estimate').innerText(), /2\.000,00/);
    assert.match(await page.locator('.report-estimate').innerText(), /não auditada/);
    for (const [label, width, height] of [['desktop', 1440, 1000], ['tablet', 768, 1024], ['mobile', 390, 844], ['small', 320, 720]]) {
      await page.setViewportSize({ width, height });
      await page.evaluate(() => window.scrollTo(0, 0));
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, `${label}: overflow horizontal`);
      await page.screenshot({ path: `tests/artifacts/relatorios/${label}.png`, fullPage: true });
    }
    await page.getByRole('button', { name: 'Imprimir / salvar PDF', exact: true }).click();
    const frameElement = page.locator('iframe[title="Impressão do relatório mensal"]');
    await frameElement.waitFor({ state: 'attached' });
    const frame = await (await frameElement.elementHandle()).contentFrame();
    await frame.waitForFunction(() => document.querySelector('.monthly-document') && [...document.styleSheets].some(s => s.href));
    assert.equal(await frame.locator('input, textarea, nav, .report-no-print').count(), 0);
    assert.match(await frame.locator('.report-estimate').innerText(), /OS-001/);
    assert.equal(await frame.evaluate(() => window.injected), undefined);
    const pdfPage = await browser.newPage();
    await pdfPage.setContent(await frame.content(), { waitUntil: 'networkidle' });
    await pdfPage.emulateMedia({ media: 'print' });
    await pdfPage.screenshot({ path: 'tests/artifacts/relatorios/print.png', fullPage: true });
    await pdfPage.pdf({ path: 'tests/artifacts/relatorios/relatorio-mensal.pdf', format: 'A4', printBackground: true, preferCSSPageSize: true });
    await pdfPage.close();
    // A failed query must preserve the original report month and manual assumptions.
    const cover = await page.locator('.report-cover').innerText();
    mode = 'error';
    await page.getByLabel('Mês de referência', { exact: true }).fill('2026-10');
    await page.getByRole('button', { name: 'Gerar relatório', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'A consulta anterior continua exibida' }).waitFor();
    assert.equal(await page.locator('.report-cover').innerText(), cover);
    assert.match(await page.locator('.report-estimate').innerText(), /2\.000,00/);
    mode = 'empty';
    await page.getByRole('button', { name: 'Gerar relatório', exact: true }).click();
    await page.getByText('Nenhuma manutenção concluída no mês selecionado.', { exact: true }).waitFor();
    assert.equal(await page.locator('.report-estimate').count(), 0);
    assert.match(await page.locator('.report-metrics').innerText(), /— h/);
    assert.equal(await page.getByLabel('Fonte e justificativa das premissas').inputValue(), '');
    // Role navigation must not expose reports to a technician.
    await page.locator('summary[aria-label="Menu do usuário"]').click();
    await page.getByRole('button', { name: 'Sair', exact: true }).click();
    role = 'Tecnico';
    await login();
    await page.getByRole('button', { name: 'Menu de navegação', exact: true }).click();
    assert.equal(await page.getByRole('navigation', { name: 'Áreas' }).getByRole('button', { name: 'Relatórios', exact: true }).count(), 0);
    assert.equal(writes, 0);
    assert.deepEqual(errors, []);
    console.log(`PASS: relatório, premissas financeiras, impressão/PDF, texto seguro, 4 resoluções, falha preservada, vazio e perfil técnico; ${queries.length} consultas simuladas, nenhuma gravação.`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
