const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
assert(process.env.ALLOW_DESTRUCTIVE_DEMO_TESTS === 'true', 'Testes com gravação desativados; exigem autorização explícita e banco demonstrativo isolado.');
const api = process.env.API_BASE_URL || 'http://localhost:5099/';
const origin = process.env.CLIENT_BASE_URL || 'http://localhost:5125/';
for (const value of [api, origin]) {
  const u = new URL(value);
  assert(['localhost', '127.0.0.1'].includes(u.hostname) && u.protocol === 'http:', 'Testes somente em HTTP loopback.');
}
assert(process.env.DEMO_PASSWORD, 'Defina DEMO_PASSWORD no processo.');
assert(process.env.DEMO_DATABASE === 'adsync_dev', 'Confirme DEMO_DATABASE=adsync_dev; nunca use produção.');
const emails = {gestor:'paulo.gestor@sustennutri.com.br',tecnico:'carlos.tecnico@sustennutri.com.br',operador:'renata.operadora@sustennutri.com.br'};
async function request(path, token, body, status) {
  const response = await fetch(new URL(path, api), {method:body === undefined?'GET':'POST', headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})}, body:body===undefined?undefined:JSON.stringify(body)});
  if (status) assert.equal(response.status,status,`HTTP inesperado em ${path}`);
  else assert(response.ok,`HTTP ${response.status} em ${path}`);
  return response.status===403?null:response.json();
}
const auth = role => request('api/auth/login',null,{email:emails[role],senha:process.env.DEMO_PASSWORD});
async function browser() {
  const brave='C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe';
  return chromium.launch({headless:true,...((process.env.CHROMIUM_PATH || fs.existsSync(brave))?{executablePath:process.env.CHROMIUM_PATH || brave}:{})});
}
async function login(page, role) {
  await page.getByLabel('E-mail',{exact:true}).fill(emails[role]);
  await page.getByLabel('Senha',{exact:true}).fill(process.env.DEMO_PASSWORD);
  await page.getByRole('button',{name:'Entrar',exact:true}).click();
  await page.getByRole('button',{name:'Sair',exact:true}).waitFor();
  await idle(page);
}
async function idle(page) {
  await page.getByRole('button',{name:'Sair',exact:true}).isEnabled().then(async enabled=>{
    if(!enabled) await page.waitForFunction(()=>[...document.querySelectorAll('button')].some(b=>b.textContent==='Sair' && !b.disabled),{},{timeout:60000});
  });
}
const queue = (page, role) => page.evaluate(email=>JSON.parse(localStorage.getItem(`nexservice:${email}:queue`) || '[]'),emails[role]);
async function create(page, description) {
  const summary=page.getByText('Abrir chamado',{exact:true});
  if(!await summary.evaluate(el=>el.parentElement.open)) await summary.click();
  const select=page.getByLabel('Equipamento',{exact:true});
  await select.selectOption({index:1});
  await page.getByLabel('Descrição do problema',{exact:true}).fill(description);
  await page.getByRole('button',{name:'Salvar chamado',exact:true}).click();
  await idle(page);
}
module.exports={assert,fs,api,origin,emails,request,auth,browser,login,idle,queue,create};
