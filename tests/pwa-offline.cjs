process.env.CLIENT_BASE_URL ||= 'http://127.0.0.1:5180/';
const {randomUUID}=require('node:crypto');
const {assert,origin,request,auth,browser,login,idle,queue,create}=require('./helpers.cjs');
(async()=>{
 const operator=await auth('operador'),b=await browser();
 try{
  const context=await b.newContext({viewport:{width:390,height:844}}),page=await context.newPage();
  await page.goto(origin);await login(page,'operador');
  await page.evaluate(()=>navigator.serviceWorker.ready.then(()=>true));
  await page.reload();await idle(page);
  await page.waitForFunction(()=>!!navigator.serviceWorker.controller);
  await context.setOffline(true);
  const description=`PWA offline ${randomUUID()}`;
  await create(page,description);assert.equal((await queue(page,'operador')).length,1);
  const pending=(await queue(page,'operador'))[0];
  await page.reload();await idle(page);
  assert.equal((await queue(page,'operador'))[0].clientSyncId,pending.clientSyncId);
  await context.setOffline(false);
  // Deliberately do not click Sync: the timer must drain the persisted queue.
  await page.waitForFunction(email=>JSON.parse(localStorage.getItem(`nexservice:${email}:queue`)||'[]').length===0,'renata.operadora@sustennutri.com.br',{timeout:60000});
  const orders=(await request('api/ordensservico?minhas=true',operator.token)).filter(o=>o.descricaoProblema===description);
  assert.equal(orders.length,1);assert.equal(new Date(orders[0].dataAbertura).getTime(),new Date(pending.dataAbertura).getTime());
  console.log('PASS: PWA Release recarrega sem rede, preserva chamado e sincroniza automaticamente uma única ordem após reconectar.');
 }finally{await b.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
