# NexService — ADSync

Frontend no VS Code com Tailwind CSS e daisyUI: [guia de edição e execução](docs/FRONTEND.md).

> Documentação em evolução: partes históricas abaixo ainda descrevem etapas anteriores. A gestão dos planos preventivos foi completada na interface; funcionamento, limites da geração automática e teste isolado em [docs/PREVENTIVAS.md](docs/PREVENTIVAS.md).

Atualização das preventivas: geração automática habilitada por autorização após revisão dos planos existentes. Requer reinício da API; seed continua desligado. As referências históricas abaixo à geração desativada não representam a configuração atual.

Frontend Blazor WebAssembly e API ASP.NET Core em .NET 10, usando o **PostgreSQL/Supabase já configurado nos User Secrets da API**.

## Atualização: testes com gravação autorizados

Atendimento offline do Técnico: ativado após autorização e aplicação da tabela de recibos no Supabase. Uso e testes em [docs/ATENDIMENTO-OFFLINE.md](docs/ATENDIMENTO-OFFLINE.md). Esse documento atualiza as limitações históricas de offline listadas ao final deste README.

A preparação offline agora é automática após login e reconexão. Para a experiência PWA completa, execute `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Start-Pwa.ps1` e use **http://127.0.0.1:5180**, com a API em 5099. O botão **Instalar NexService** depende de confirmação do navegador. O endereço 5125 continua destinado ao desenvolvimento; não oferece a mesma recarga offline.

Por solicitação do usuário, a execução atual em `http://localhost:5125` permite operações no Supabase: `Safety:ReadOnly=false` na API e `ReadOnlyMode=false` no cliente. Seed e geração automática de preventivas continuam desligados; não há inicialização ou migração no startup. A conexão permanece nos User Secrets existentes.

Entre novamente com sua conta existente. A sessão, o cache e a fila usam `StorageScope=supabase`, separados da demonstração local; pendências antigas não são enviadas ao Supabase. As ações feitas neste endereço agora gravam no banco real.

O perfil da solução é **NexService - Supabase**. As seções abaixo sobre somente leitura e `docs/SUPABASE.md` registram a validação anterior e não descrevem o modo atual. Os testes `read-only.cjs` exigem retornar explicitamente API e cliente ao modo de leitura; não devem ser executados esperando esse modo na configuração atual. Testes automáticos que criam dados continuam destinados ao banco local isolado.

## Executar no Windows / Visual Studio 2026

Abra `ADSync.slnx` ou use o PowerShell na pasta do projeto:

```powershell
cd C:\Users\Diogo\Documents\Projeto-ADSync\ADSync
& ./tools/Use-LocalSdk.ps1
dotnet restore ADSync.slnx
dotnet build ADSync.slnx --no-restore
```

O script usa o SDK 10.0.403 portátil já preparado nesta máquina, sem alterar o PATH global. Em outra máquina, é necessário ter SDK .NET 10 instalado. As dependências NuGet declaradas foram preservadas. Pare a API antes de recompilar para evitar DLLs bloqueadas no Windows; reinicie o cliente após recompilar/publicar para atualizar o mapa de recursos Blazor.

**Terminal 1 — API:**

```powershell
& ./tools/Start-Api.ps1
```

**Terminal 2 — cliente, na mesma pasta:**

```powershell
& ./tools/Start-DemoClient.ps1
```

Abra **http://localhost:5125/**. A API atende em **http://localhost:5099/**. `Ctrl+C` encerra cada processo. Não é necessário iniciar banco local, Docker ou inicializador de banco.

## Rodar com F5 no Visual Studio

O arquivo `ADSync.slnLaunch` inclui o perfil **NexService - Supabase (somente leitura)**, com API e cliente iniciados juntos. A API usa o perfil **NexService Local** (5099); o cliente usa **http** (5125). O perfil da API solicita a abertura do navegador diretamente em **http://localhost:5125/**; a abertura pelo cliente fica desativada para evitar duas abas. O nome Local refere-se à API na máquina; a conexão continua sendo a existente nos User Secrets do Supabase. Os perfis anteriores foram preservados.

1. Abra `ADSync.slnx`. Se a solução já estava aberta, feche e reabra para carregar o novo perfil.
2. No seletor ao lado do botão verde de execução, escolha **NexService - Supabase (somente leitura)**.
3. Pressione **F5**. A aplicação deve abrir em **http://localhost:5125/**.
4. Use **Shift+F5** para encerrar a depuração.

Se o perfil não aparecer, clique com o botão direito na **solução ADSync → Configurar Projetos de Inicialização** (ou **Propriedades → Projeto de Inicialização**). Selecione múltiplos projetos e configure:

| Projeto | Ação | Destino de depuração |
| --- | --- | --- |
| ADSync.Api | Iniciar | NexService Local |
| ADSync.Client | Iniciar | http |
| Demais projetos, inclusive DemoDatabase | Nenhum | — |

Se necessário, habilite perfis de inicialização de múltiplos projetos em **Ferramentas → Opções → Recursos de Visualização**, conforme a [documentação do Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects?view=visualstudio). A seleção do perfil ativo é feita na IDE; os testes por terminal não equivalem a pressionar F5 dentro do Visual Studio.

Não deixe as instâncias iniciadas pelo terminal ocupando 5099/5125 ao usar F5. Pare-as com `Ctrl+C` primeiro. Não selecione `DemoDatabase` como projeto de inicialização. Os scripts de terminal usam `--no-launch-profile`, portanto a configuração do F5 não substitui suas portas.

## Conexão e proteção dos dados

A API carrega `ConnectionStrings:AdSync` e `Jwt:Key` dos **User Secrets existentes**. Não substitua a conexão pela configuração demonstrativa anterior. Para verificar configurações privadamente no Visual Studio, use **ADSync.Api → Gerenciar Segredos do Usuário**; não copie valores para frontend, Git, logs, screenshots ou documentação.

O script da API recusa a execução se encontrar `ConnectionStrings__AdSync` no ambiente, para evitar sobrescrita acidental dos User Secrets. Se você tiver definido essa variável no terminal durante a etapa anterior, remova apenas a sobrescrita desse terminal antes de executar:

```powershell
Remove-Item Env:ConnectionStrings__AdSync -ErrorAction SilentlyContinue
```

Controles explícitos em `ADSync.Api/appsettings.json`, também fixados pelos scripts/perfis de validação:

```json
{
  "Safety": { "ReadOnly": true },
  "Seed": { "Enabled": false },
  "Preventivas": { "GeracaoAutomaticaHabilitada": false }
}
```

O seed não é mais automático em Development. O job preventivo também fica desligado, pois cria ordens e avança datas de planos. Em somente leitura, a API bloqueia mutações de negócio e intercepta gravações EF, inclusive atualizações internas do Identity no login. Não há migration, inicialização ou alteração de esquema no startup.

O cliente está em `ReadOnlyMode=true`: consultas usam dados atuais da API; abertura, atribuição, ações técnicas, criação de planos e envio da fila estão desativados. Filas antigas do navegador permanecem armazenadas e não são enviadas. Sessões e cache da demonstração anterior não são reutilizados para apresentar dados do Supabase. Faça login novamente com uma conta **já existente** no seu banco; nenhum usuário será criado ou terá senha redefinida.

Se o Identity precisar gravar para atualizar hash/contador de bloqueio, o login poderá retornar 403 em somente leitura. Analise essa condição antes de autorizar mudanças; não desative as proteções automaticamente.

## Validar sem alterar o Supabase

Para conferir os controles da API:

```powershell
Invoke-RestMethod http://localhost:5099/api/runtime
```

Resultado esperado: `readOnly=True`, `seedEnabled=False`, `preventiveEnabled=False`.

Para consultar o banco usando o modelo EF existente, sem seed, SQL de alteração ou inicializador:

```powershell
& ./tools/Use-LocalSdk.ps1
dotnet run --project tools/ReadOnlyDatabase
```

O utilitário lê a conexão dos User Secrets, faz consultas sem rastreamento e imprime apenas contagens/resultados resumidos. Não exibe conexão, credenciais ou dados pessoais.

Testes de proteção **sem banco real**:

```powershell
dotnet run --project tests/ReadOnlyChecks
```

Para o navegador, com API/cliente abertos e Node.js disponível:

```powershell
npm.cmd ci
node tests/read-only.cjs
```

O teste usa Chromium do Brave nesta máquina. Faz somente login e consultas; verifica que a sessão antiga não é reutilizada e que pendências não são enviadas após 20 segundos. Usa `READONLY_EMAIL` para selecionar uma conta existente; o padrão é o e-mail de gestor da demonstração anterior. A senha vem de `READONLY_PASSWORD` ou do valor existente de `Seed:SenhaDev`, lido em memória. Isso não executa seed. Não grave senhas em arquivos/comandos compartilhados. Se essas credenciais não corresponderem ao banco, informe uma conta existente por meio privado antes de repetir.

**Não execute** `tools/DemoDatabase`, `EnsureCreated`, `EnsureDeleted`, migrations, resets, scripts SQL nem os testes antigos de abertura/consumo de estoque contra esse banco. Os testes demonstrativos com gravação estão bloqueados por padrão e continuam exigindo autorização explícita e ambiente apropriado.

Resultados e limites desta etapa: [docs/SUPABASE.md](docs/SUPABASE.md). A validação anterior em banco local ficou registrada separadamente em [docs/MVP.md](docs/MVP.md); ela não comprova operações de escrita no Supabase.

## Visibilidade das ordens de serviço

### Atribuição automática na abertura (RF02 / RN02)

Novas O.S. abertas por Operador/Gestor recebem o técnico ativo com menor quantidade de ordens abertas, em andamento ou pausadas. Concluídas e canceladas não entram na carga. Em empate, têm preferência técnicos sem histórico de atribuição, depois os que receberam a última ordem há mais tempo (data de abertura), e por último o Id para desempate estável. Esse é o critério inicial adotado enquanto a regra operacional definitiva não é acordada com a empresa; não representa disponibilidade por turno ou especialidade.

Sem técnico ativo, a O.S. permanece Aberta e sem responsável, disponível ao Gestor para atribuição manual. O Gestor pode alterar o responsável pelas regras já existentes. Ordens antigas não são redistribuídas. Chamados offline recebem atribuição quando chegam ao servidor; preservam a data original e os reenvios não trocam o responsável nem duplicam a O.S. Aberturas simultâneas são serializadas por uma trava transacional do PostgreSQL, com escolha e gravação na mesma transação. Planos preventivos mantêm o responsável definido no plano. Não há alteração de esquema, seed ou migração.

Teste de integração: `dotnet run --project tests/AssignmentChecks`. Cria e remove um banco exclusivo com nome aleatório no PostgreSQL portátil em `127.0.0.1:55433`, sem ler User Secrets. Verifica ausência de técnicos, menor carga, exclusão de inativos, aberturas e reenvios concorrentes e preservação da atribuição manual.

O Operador tem somente a área de ordens, para abrir e acompanhar as próprias solicitações. Estoque e planos preventivos são restritos a Gestor/Técnico na tela e na API; os dados dessas áreas não são carregados para o Operador. Nos detalhes de uma ordem, o Técnico encontra Iniciar, motivo da pausa, Pausar e Retomar. Registrar peças exige ordem em andamento/pausada e peça/quantidade válidas; concluir exige ordem em andamento e causa selecionada.

Na API, o Gestor consulta todas as ordens; o Operador consulta somente as que abriu; o Técnico consulta somente as atribuídas a ele. Essa restrição vale para a listagem, inclusive sem `minhas` ou com `minhas=false`, e para os detalhes por identificador. Ordens fora do escopo retornam 404, assim como identificadores inexistentes. Perfis desconhecidos são recusados. O Técnico só pode iniciar uma ordem previamente atribuída a ele; ordens sem responsável dependem de atribuição pelo Gestor.

Validação sem banco: `dotnet run --project tests/OrderAccessChecks`. Usa controller e serviço reais com repositório em memória para verificar isolamento entre contas, filtros, acesso do gestor e bloqueio de início indevido.

## Cadastros do gestor

A área **Usuários**, exclusiva do Gestor, oferece busca e consulta de contas ativas/inativas. Em ambiente autorizado para escrita, permite criar usuários com senha inicial, editar nome/e-mail/perfil (Operador, Técnico ou Gestor) e ativar/desativar contas. Conta Identity e cadastro de negócio são gravados na mesma transação, sem alteração de esquema. O gestor não pode desativar a própria conta nem retirar o próprio perfil de Gestor. A API consulta a situação e o perfil atual a cada requisição autenticada; contas desativadas e tokens com perfil antigo são recusados. Alterações de usuários exigem conexão e não entram na fila offline; a tela não persiste a listagem em cache local. Recuperação de senha continua pendente.

Validação local da gestão de usuários: com o ambiente de testes abaixo iniciado, execute `node tests/usuarios.cjs`. O teste usa exclusivamente as portas 5100/5126 e cria contas fictícias no PostgreSQL local. A aplicação Supabase permanece somente leitura.

Ambiente local separado para validar gravações: [instruções dos testes de cadastros](docs/CADASTROS-TESTES.md). Esse fluxo usa exclusivamente PostgreSQL local e cópias da aplicação em outras portas; mantém o Supabase protegido.

A área **Cadastros**, disponível após login como Gestor, permite consultar e buscar equipamentos, peças e causas de falha, incluindo equipamentos e causas inativos. Use **Atualizar cadastros** para recarregar as listas.

Em ambiente autorizado para escrita, a tela oferece criação e edição dos três cadastros, ativação/inativação de equipamentos e causas e entrada de estoque de peças. A edição da peça não altera seu saldo; reposições usam a operação específica de entrada. As ações exigem conexão, não entram na fila offline e preservam o formulário quando a API recusa a gravação.

Na configuração atual, somente leitura, os formulários e botões de alteração ficam ocultos e o cliente bloqueia POST/PUT de negócio. Esta implementação não habilita escrita, altera configurações ou modifica o esquema do Supabase. Os fluxos de escrita precisam ser validados em um ambiente apropriado antes do uso em produção.

## PWA e limites do MVP

`ApiBaseUrl` no cliente Development aponta para 5099; o CORS existente permite a origem 5125. Fora de Development, o padrão é mesma origem com encaminhamento de `/api`, ou URL pública configurada com CORS. Nunca coloque segredos em `wwwroot`.

O servidor de desenvolvimento não oferece recarga totalmente offline; isso exige a PWA Release com service worker ativo. A antiga PWA Release local foi encerrada para evitar uso da versão demonstrativa desatualizada. A etapa atual não testa escrita offline no Supabase e não faz deploy.

No MVP anterior somente a abertura tinha fila offline. Atendimento técnico offline com idempotência transacional, concorrência entre abas/dispositivos, MTBF real, definição operacional de MTTR, SLA/retrabalho, administração completa, migrações de banco novo, HTTPS, backups, recuperação de acesso, retenção no celular e paginação continuam pendentes para um piloto real.

Nenhuma contratação, publicação ou alteração de recurso remoto faz parte desta tarefa. Qualquer alteração estrutural ou teste com gravação no Supabase deve ter a necessidade apresentada e receber autorização antes da execução.
