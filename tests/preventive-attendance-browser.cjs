const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const origin = process.env.PREVENTIVE_ORIGIN || 'http://127.0.0.1:5138';
assert(['127.0.0.1', 'localhost'].includes(new URL(origin).hostname));

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const offline of [false, true]) {
      const context = await browser.newContext({ viewport: { width: 1280, height: 1000 } });
      const page = await context.newPage();
      const errors = [], mutations = [];
      let started = false, completed = false;
      const id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
      const technician = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
      const planId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
      const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date());
      const future = '2099-12-31';
      const detail = () => ({ id, numeroOS: 'OS-PREVENTIVA-TESTE', status: completed ? 'Concluida' : 'EmAndamento', prioridade: 'Media',
        tipoManutencao: 'Preventiva', geradaAutomaticamente: true, equipamentoNome: 'Esteira', setor: 'Produção',
        tecnicoId: technician, tecnicoNome: 'Técnico de teste', operadorNome: 'Técnico de teste', descricaoProblema: 'Lubrificar rolamentos',
        dataAbertura: new Date().toISOString(), dataConclusao: completed ? new Date().toISOString() : null, tempoAtendimentoMinutos: 10,
        registros: [{ tipo: 'Inicio', timestamp: new Date().toISOString() }, ...(completed ? [{ tipo: 'Conclusao', timestamp: new Date().toISOString() }] : [])], pecas: [] });
      page.on('pageerror', e => errors.push(e.message));
      await page.route('**/appsettings*.json', route => route.fulfill({ json: { ApiBaseUrl: origin + '/', ReadOnlyMode: false, TechnicalOfflineEnabled: offline } }));
      // Todas as APIs são simuladas; não usa credenciais nem acessa dados reais.
      await page.route('**/api/**', async route => {
        const req = route.request(), path = new URL(req.url()).pathname;
        let data = [];
        if (path === '/api/auth/login') data = { token: 'fixture', nome: 'Técnico de teste', email: 'tecnico@example.test', papeis: ['Tecnico'], expiraEm: '2099-12-31T23:59:59Z' };
        else if (path === '/api/runtime') data = { readOnly: false, technicalOfflineEnabled: offline };
        else if (req.method() !== 'GET') {
          mutations.push({ path, body: req.postDataJSON() });
          if (path === `/api/planos-preventivos/${planId}/iniciar`) started = true;
          else if (path.endsWith('/concluir') || (path.endsWith('/eventos') && req.postDataJSON().acao === 'concluir')) completed = true;
          else throw new Error('Mutação inesperada: ' + path);
          data = detail();
        } else if (path === '/api/planos-preventivos') data = [
          { id: planId, equipamentoNome: 'Esteira de hoje', tecnicoResponsavelId: technician, tecnicoResponsavelNome: 'Técnico de teste', descricao: 'Lubrificar', periodicidadeDias: 30, proximaExecucao: today, ativo: true, inicioLiberado: true, podeAtender: true },
          { id: 'dddddddd-dddd-dddd-dddd-dddddddddddd', equipamentoNome: 'Esteira futura', descricao: 'Inspecionar', periodicidadeDias: 30, proximaExecucao: future, ativo: true, inicioLiberado: false, podeAtender: true },
          { id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', equipamentoNome: 'Outro técnico', descricao: 'Revisar', periodicidadeDias: 30, proximaExecucao: today, ativo: true, inicioLiberado: true, podeAtender: false }
        ];
        else if (path === '/api/ordensservico') data = started ? [detail()] : [];
        else if (path === `/api/ordensservico/${id}`) data = detail();
        return route.fulfill({ json: data });
      });
      await page.goto(origin);
      await page.locator('#login-email').fill('tecnico@example.test');
      await page.locator('#login-password').fill('Simulacao!123');
      await page.getByRole('button', { name: 'Entrar', exact: true }).click();
      await page.getByRole('button', { name: 'Menu de navegação', exact: true }).click();
      await page.getByRole('navigation', { name: 'Áreas' }).getByRole('button', { name: 'Preventivas', exact: true }).click();
      const todayCard = page.getByRole('article').filter({ hasText: 'Esteira de hoje' });
      const futureCard = page.getByRole('article').filter({ hasText: 'Esteira futura' });
      await todayCard.getByRole('button', { name: 'Iniciar manutenção' }).waitFor();
      assert(await todayCard.getByRole('button', { name: 'Iniciar manutenção' }).isEnabled());
      assert(await futureCard.getByRole('button', { name: 'Iniciar manutenção' }).isDisabled());
      assert.equal(await page.getByRole('article').filter({ hasText: 'Outro técnico' }).getByRole('button', { name: 'Iniciar manutenção' }).count(), 0);
      await todayCard.getByRole('button', { name: 'Iniciar manutenção' }).click();
      const dialog = page.getByRole('dialog', { name: 'Detalhes da ordem' });
      await dialog.waitFor();
      await dialog.getByLabel('Observações', { exact: true }).fill('Preventiva executada sem falhas.');
      const confirmation = page.waitForResponse(response => response.url().includes(offline ? '/eventos' : '/concluir') && response.request().method() === 'POST');
      await dialog.getByRole('button', { name: 'Concluir', exact: true }).click();
      await page.waitForFunction(() => document.querySelector('dialog')?.textContent.includes('Concluida'));
      await confirmation;
      assert(completed && mutations.length === 2);
      assert.equal(mutations[1].body.causaFalhaId, null);
      assert.deepEqual(errors, []);
      console.log(`PASS: navegador (${offline ? 'offline habilitado' : 'online'}): data futura bloqueada, somente responsável inicia, OS abre no atendimento e conclui sem causa.`);
      await context.close();
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
