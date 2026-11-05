const {randomUUID}=require('node:crypto');
const {assert,fs,request,auth}=require('./helpers.cjs');
(async()=>{
 const manager=await auth('gestor');
 if(process.argv[2]==='prepare'){
  const equipment=await request('api/equipamentos',manager.token);
  const technicians=await request('api/usuarios?tipo=Tecnico',manager.token);
  const today=new Date().toISOString().slice(0,10);
  const plan=await request('api/planos-preventivos',manager.token,{equipamentoId:equipment[0].id,tecnicoResponsavelId:technicians[0].id,descricao:`Job preventivo ${randomUUID()}`,periodicidadeDias:30,proximaExecucao:today});
  fs.mkdirSync('.local-demo',{recursive:true});
  fs.writeFileSync('.local-demo/preventive-job.json',JSON.stringify({plan,today}));
  console.log('Plano vencendo hoje criado. Reinicie a API demonstrativa e execute check após a primeira rodada do job (10 s).');
 }else{
  const {plan,today}=JSON.parse(fs.readFileSync('.local-demo/preventive-job.json','utf8'));
  const orders=await request('api/ordensservico?minhas=true',manager.token);
  const matching=[];
  for(const order of orders.filter(o=>o.geradaAutomaticamente)){
   const detail=await request('api/ordensservico/'+order.id,manager.token);
   if(detail.descricaoProblema.includes(plan.descricao))matching.push(detail);
  }
  assert.equal(matching.length,1);assert.equal(matching[0].tecnicoId,plan.tecnicoResponsavelId);
  const updated=await request('api/planos-preventivos/'+plan.id,manager.token);
  assert(updated.proximaExecucao>today);
  console.log('PASS: job gerou exatamente uma preventiva atribuída e avançou a próxima execução.');
 }
})().catch(e=>{console.error(e);process.exitCode=1});
