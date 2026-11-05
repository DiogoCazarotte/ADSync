const {randomUUID}=require('node:crypto');
const {assert,origin,request,auth,browser,login,idle,queue,create}=require('./helpers.cjs');
(async()=>{
 const operator=await auth('operador'),b=await browser();
 try{
  const page=await b.newPage({viewport:{width:390,height:844}});
  await page.goto(origin);await login(page,'operador');
  let sent=0;
  await page.route('**/api/ordensservico',r=>{sent++;return r.abort();});
  await page.evaluate(()=>{
   window.originalStorageSet=Storage.prototype.setItem;
   Storage.prototype.setItem=function(key,value){if(key.endsWith(':queue'))throw new DOMException('Sem espaço','QuotaExceededError');return window.originalStorageSet.call(this,key,value);};
  });
  const retained=`Formulário preservado quando disco cheio ${randomUUID()}`;
  await create(page,retained);
  assert.equal(await page.getByLabel('Descrição do problema',{exact:true}).inputValue(),retained);
  assert.equal((await queue(page,'operador')).length,0);assert.equal(sent,0);
  await page.evaluate(()=>Storage.prototype.setItem=window.originalStorageSet);
  // The form is still open after the failed write.
  await page.getByRole('button',{name:'Salvar chamado',exact:true}).click();await idle(page);
  assert.equal((await queue(page,'operador')).length,1);
  const pending=(await queue(page,'operador'))[0];
  await page.unroute('**/api/ordensservico');
  for(const code of [401,400]) {
   await page.route('**/api/ordensservico',r=>r.fulfill({status:code,contentType:'application/json',body:JSON.stringify({mensagem:'Erro demonstrativo'})}));
   await page.getByRole('button',{name:'Tentar sincronizar',exact:true}).click();await idle(page);
   assert.equal((await queue(page,'operador'))[0].clientSyncId,pending.clientSyncId);
   await page.unroute('**/api/ordensservico');
  }
  await page.evaluate(()=>{
   Storage.prototype.setItem=function(key,value){if(key.endsWith(':queue')&&value==='[]')throw new DOMException('Falha ao remover fila','QuotaExceededError');return window.originalStorageSet.call(this,key,value);};
  });
  await page.getByRole('button',{name:'Tentar sincronizar',exact:true}).click();await idle(page);
  assert.equal((await queue(page,'operador')).length,1);
  assert.equal((await request('api/ordensservico?minhas=true',operator.token)).filter(o=>o.descricaoProblema===pending.descricaoProblema).length,1);
  await page.evaluate(()=>Storage.prototype.setItem=window.originalStorageSet);
  await page.getByRole('button',{name:'Tentar sincronizar',exact:true}).click();await idle(page);
  assert.equal((await queue(page,'operador')).length,0);
  assert.equal((await request('api/ordensservico?minhas=true',operator.token)).filter(o=>o.descricaoProblema===pending.descricaoProblema).length,1);
  console.log('PASS: falha de armazenamento preserva formulário; rede, 401 e 400 preservam fila; remoção local falha sem perder chamado ou duplicar servidor.');
  const lost=`Resposta perdida ${randomUUID()}`;
  let release,signal;
  const held=new Promise(resolve=>release=resolve),received=new Promise(resolve=>signal=resolve);
  await page.route('**/api/ordensservico',async route=>{await route.fetch();signal();await held;await route.abort();});
  const opening=create(page,lost);
  await received;
  assert.equal(await page.getByRole('button',{name:'Sair',exact:true}).isDisabled(),true);
  assert.equal(await page.getByLabel('Descrição do problema',{exact:true}).isDisabled(),true);
  release();await opening;
  assert.equal((await queue(page,'operador')).length,1);
  await page.unroute('**/api/ordensservico');
  await page.getByRole('button',{name:'Tentar sincronizar',exact:true}).click();await idle(page);
  assert.equal((await queue(page,'operador')).length,0);
  assert.equal((await request('api/ordensservico?minhas=true',operator.token)).filter(o=>o.descricaoProblema===lost).length,1);
  assert.equal(await page.evaluate(()=>Object.values(localStorage).some(v=>v.includes('"token":'))),false);
  assert.equal(await page.evaluate(()=>!!sessionStorage.getItem('nexservice:session')),true);
  console.log('PASS: edição/troca de conta bloqueadas durante envio; resposta perdida resulta em uma ordem; token somente em sessionStorage.');
 } finally{await b.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
