const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {chromium}=require('playwright');
(async()=>{
 const api='http://localhost:5099/',origin='http://localhost:5125/';
 const runtime=await (await fetch(api+'api/runtime')).json();
 assert.equal(runtime.readOnly,true);assert.equal(runtime.seedEnabled,false);assert.equal(runtime.preventiveEnabled,false);
 console.log('PASS: API somente leitura; seed e job preventivo desativados.');
 // Credentials are read in memory, never written to artifacts or console.
 let password=process.env.READONLY_PASSWORD;
 if(!password){
  const file=path.join(process.env.APPDATA,'Microsoft/UserSecrets/c6a39c55-2a48-4e31-bf5a-9996b0d92007/secrets.json');
  const secrets=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
  password=secrets['Seed:SenhaDev'];
 }
 assert(password,'Defina READONLY_PASSWORD para uma conta existente.');
 const email=process.env.READONLY_EMAIL || 'paulo.gestor@sustennutri.com.br';
 const brave='C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe';
 const browser=await chromium.launch({headless:true,...(fs.existsSync(brave)?{executablePath:brave}:{})});
 try{
  const page=await browser.newPage({viewport:{width:390,height:844}});
  await page.addInitScript(({email})=>{
   // Simulated leftovers from the old local demo; never sent to the server.
   localStorage.setItem(`nexservice:${email}:queue`,JSON.stringify([{clientSyncId:'bd4ef61b-3bd6-4cee-a6c6-b4835262e80b',descricaoProblema:'Pendência local preservada'}]));
   sessionStorage.setItem('nexservice:session',JSON.stringify({token:'old-demo-token',email,nome:'Sessão antiga',papeis:['Gestor']}));
  },{email});
  const writes=[],failed=[];
  page.on('request',r=>{if(r.url().includes('/api/')&&!['GET','OPTIONS'].includes(r.method())&&!r.url().endsWith('/api/auth/login'))writes.push(r.method());});
  page.on('response',r=>{if(r.url().includes('/api/')&&r.status()>=400)failed.push(r.status());});
  await page.goto(origin);
  await page.getByLabel('E-mail',{exact:true}).fill(email);
  await page.getByLabel('Senha',{exact:true}).fill(password);
  await page.getByRole('button',{name:'Entrar',exact:true}).click();
  await page.getByRole('button',{name:'Sair',exact:true}).waitFor({timeout:30000});
  const idle=()=>page.waitForFunction(()=>[...document.querySelectorAll('button')].some(b=>b.textContent==='Sair'&&!b.disabled));
  await idle();
  const token=await page.evaluate(()=>JSON.parse(sessionStorage.getItem('nexservice:readonly:session')).token);
  for(let i=0;i<35;i++){
   const response=await page.request.get(api+'api/ordensservico?minhas=true',{headers:{Authorization:`Bearer ${token}`}});
   assert.equal(response.status(),200);
  }
  console.log('PASS: 35 consultas autenticadas consecutivas, somente leitura.');
  assert.equal(await page.getByText('Abrir chamado',{exact:true}).count(),0);
  assert.equal(await page.getByRole('button',{name:'Tentar sincronizar',exact:true}).isDisabled(),true);
  for(const section of ['Equipamentos e peças','Preventivas','Indicadores','Ordens de serviço']){
   const button=page.getByRole('button',{name:section,exact:true});
   if(await button.count()){await button.click();await idle();}
  }
  assert.equal(await page.getByText('Criar plano preventivo',{exact:true}).count(),0);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
  // Cross the 20-second timer boundary: no queued call may be sent.
  await page.waitForTimeout(21000);
  assert.deepEqual(writes,[]);assert.deepEqual(failed,[]);
  assert.equal(await page.evaluate(email=>JSON.parse(localStorage.getItem(`nexservice:${email}:queue`)).length,email),1);
  await page.getByRole('button',{name:'Sair',exact:true}).click();
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('nexservice:readonly:session')),null);
  console.log('PASS: login existente e consultas do frontend/API; sessão antiga isolada e pendência preservada sem reenvio após 20 segundos; logout validado.');
 }finally{await browser.close();}
})().catch(error=>{
 // Do not dump browser state, responses, credentials or raw connection errors.
 console.error(`FAIL: validação somente leitura (${error.name}). Verifique disponibilidade da API/cliente e credenciais existentes; detalhes sensíveis omitidos.`);
 process.exitCode=1;
});
