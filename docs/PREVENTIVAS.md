# Planos preventivos — documentação em evolução

A área **Preventivas** oferece criação, edição, busca e ativação/inativação dos planos para o Gestor. Técnico consulta os planos ativos. Operador não acessa essa área. No modo somente leitura, os controles de alteração não aparecem.

Cada plano possui equipamento, técnico responsável, tarefa, periodicidade de 1 a 3650 dias e próxima execução. Equipamento e técnico devem estar ativos. A edição preserva a situação atual do plano; ativação e inativação são ações separadas. Inativar não cancela ordens já geradas. O gestor pode listar os inativos e reativá-los.

As alterações exigem conexão e confirmação da API; não entram na fila offline. Erros preservam o formulário. Quando uma resposta se perder, atualize os planos para conferir a gravação antes de repetir. A consulta pode usar cache se houver falha de rede, com o aviso de desatualização do cliente.

## Geração de ordens

O agendador está **habilitado na configuração atual** (`Preventivas:GeracaoAutomaticaHabilitada=true`), incluindo os perfis da API e `tools/Start-Api.ps1`. A mudança exige reiniciar a API. Seed permanece desligado. Os scripts do ambiente isolado de cadastros mantêm a geração desligada por padrão.

Quando habilitado em um ambiente adequado, o job verifica os vencimentos 10 segundos após iniciar a API e depois a cada hora, usando a data UTC. Seleciona planos ativos, equipamento e responsável ativos, e sem ordem anterior aberta, em andamento ou pausada. Gera uma ordem preventiva atribuída ao técnico do plano e avança a data pela periodicidade. Datas muito antigas avançam um intervalo por geração; não são reposicionadas automaticamente para o futuro.

A seleção e a gravação do job agora ocorrem na mesma transação, com uma trava transacional do PostgreSQL compartilhada entre as instâncias da API. Isso impede duas rodadas simultâneas de gerar ordens para o mesmo plano. A gravação da ordem e o avanço da data são confirmados juntos. Edições manuais concorrentes dos planos ainda precisam de validação específica.

Antes da ativação autorizada, a consulta somente leitura ao Supabase em 06/11/2026 encontrou um plano ativo e nenhum elegível para geração imediata. A habilitação não altera o esquema do Supabase. Planos que vencerem depois serão processados enquanto a API estiver em execução. Para interromper novas gerações, desabilitar a opção nos perfis e no script utilizados e reiniciar a API; ordens existentes são preservadas.

Validação local: `dotnet run --project tests/AssignmentChecks` usa o job e os repositórios reais em um banco descartável de nome aleatório no PostgreSQL de testes em `127.0.0.1:55433`, sem ler User Secrets. Seis rodadas simultâneas geraram uma única ordem, atribuída ao técnico correto, e avançaram uma periodicidade. Foram verificados planos inativos/futuros, técnico inativo, bloqueio por ordem pausada e liberação após conclusão. As regressões existentes de atribuição e atendimento offline também passaram.

## Verificação da interface sem banco

Na raiz do projeto:

```powershell
.local-demo/dotnet/dotnet.exe publish ADSync.Client -c Release -o .local-demo/preventivas/client --no-restore
node tests/preventivas.cjs
```

O teste usa API simulada e uma porta local aleatória. Exercita criação, edição, rejeição sem perda de dados, cancelamento, ativação/inativação, filtros, viewport móvel e consulta do técnico. Não valida gravações reais no Supabase nem execução do job.
