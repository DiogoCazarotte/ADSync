# NexService — ADSync

Sistema de gestão e controle da manutenção industrial, voltado ao registro de ordens de serviço (O.S.), à organização dos atendimentos e ao planejamento de manutenções preventivas a partir do histórico dos equipamentos.

Frontend **Blazor WebAssembly** com **Vue.js 3**, Tailwind CSS e daisyUI; API **ASP.NET Core em .NET 10**, utilizando **PostgreSQL hospedado no Supabase**.

A aplicação reúne abertura e acompanhamento de chamados, atribuição de técnicos, controle de peças, manutenção preventiva, indicadores e relatórios mensais com impressão/PDF. Também pode ser instalada como PWA, com suporte a operações específicas offline e sincronização posterior.

## Tecnologias utilizadas

| Camada | Tecnologias |
| --- | --- |
| Frontend | Blazor WebAssembly, Razor, HTML, CSS, JavaScript, Vue.js 3.5.46, Tailwind CSS 4 e daisyUI 5 |
| Backend | C#, .NET 10, ASP.NET Core Web API |
| Banco de dados | PostgreSQL, Supabase, Entity Framework Core, Npgsql e LINQ |
| Comunicação e segurança | API REST, HTTP, JSON, ASP.NET Core Identity e JWT |
| Offline | PWA, service worker e armazenamento local do navegador |
| Infraestrutura | Docker, Docker Compose, Node.js/npm e NuGet |

## Estrutura do projeto

| Projeto | Responsabilidade |
| --- | --- |
| ADSync.Client | Interface Blazor, integração Vue.js e comunicação com a API. |
| ADSync.Api | Controllers, autenticação, autorização e configuração. |
| ADSync.Application | Serviços, interfaces, casos de uso e DTOs. |
| ADSync.Domain | Entidades, enums e regras de negócio. |
| ADSync.Infrastructure | Persistência, repositórios, Identity e serviços de manutenção. |

## Atualização da aplicação

A área **Equipamentos e peças** passou a utilizar Vue.js para renderizar tabelas com pesquisa por nome, patrimônio ou setor, filtro por situação da máquina, pesquisa de peças por nome ou código, ordenação alfabética e contagem dos resultados. O histórico de O.S. é exibido em uma janela modal; equipamentos inativos ficam disponíveis ao Gestor.

O Vue.js controla somente as tabelas dessa área. A integração ocorre por JavaScript Interop, enquanto a sessão, os modais e o restante da interface permanecem no Blazor. A biblioteca Vue é disponibilizada localmente, sem depender de CDN.

Foram adicionados componentes reutilizáveis de modais, avisos e notificações de sucesso, erro e conexão. Operadores e gestores podem editar descrições de O.S. dentro de seu escopo; o Gestor pode redefinir senhas de usuários.

A geração automática de preventivas está habilitada. O Técnico responsável também pode iniciar ou continuar o atendimento pelo próprio plano, a partir da data prevista no horário de Brasília. O seed continua desligado.

## Executar no Windows / Visual Studio 2026

Requisitos: SDK .NET 10, Node.js com npm, PostgreSQL previamente configurado e credenciais da API nos User Secrets. O Visual Studio é opcional para execução por terminal.

Abra `ADSync.slnx` ou use o PowerShell na raiz do projeto:

```powershell
& ./tools/Use-LocalSdk.ps1
npm.cmd ci
npm.cmd run build:vendor
npm.cmd run build:css
dotnet restore ADSync.slnx
dotnet build ADSync.slnx --no-restore
```

O script aproveita o SDK portátil em `.local-demo/dotnet`, quando disponível. Em outra máquina, é necessário ter o SDK .NET 10 instalado. Pare a API antes de recompilar para evitar DLLs bloqueadas no Windows; reinicie o cliente após recompilar/publicar para atualizar os recursos Blazor.

**Terminal 1 — API:**

```powershell
& ./tools/Start-Api.ps1
```

**Terminal 2 — cliente, na mesma pasta:**

```powershell
& ./tools/Start-DemoClient.ps1
```

Abra **http://localhost:5125/**. A API atende em **http://localhost:5099/**. `Ctrl+C` encerra cada processo. Não é necessário iniciar banco local ou Docker para acessar o Supabase configurado.

**Atenção:** esses scripts iniciam a API com gravação e geração preventiva habilitadas. Execute-os apenas no ambiente autorizado para essas operações.

### Frontend no VS Code

Os componentes e páginas ficam em `ADSync.Client/Layout` e `ADSync.Client/Pages`. A integração Vue está em `ADSync.Client/wwwroot/inventory.js` e os estilos em `ADSync.Client/Styles/app.css`.

```powershell
npm.cmd run watch:css
```

Use `npm.cmd run build:css` para gerar o CSS final e `npm.cmd run build:vendor` para preparar a biblioteca Vue local. A compilação do cliente também possui alvos para esses recursos.

## Rodar com F5 no Visual Studio

O arquivo `ADSync.slnLaunch` inclui o perfil **NexService - Supabase**, com API e cliente iniciados juntos. A API usa o perfil **NexService Local** (5099); o cliente usa **http** (5125). O nome Local refere-se à API na máquina; a conexão permanece nos User Secrets.

1. Abra `ADSync.slnx`. Se a solução já estava aberta, feche e reabra para carregar o perfil.
2. No seletor ao lado do botão de execução, escolha **NexService - Supabase**.
3. Pressione **F5**. A aplicação deve abrir em **http://localhost:5125/**.
4. Use **Shift+F5** para encerrar a depuração.

Se o perfil não aparecer, clique com o botão direito na **solução ADSync → Configurar Projetos de Inicialização** (ou **Propriedades → Projeto de Inicialização**). Selecione múltiplos projetos e configure:

| Projeto | Ação | Destino de depuração |
| --- | --- | --- |
| ADSync.Api | Iniciar | NexService Local |
| ADSync.Client | Iniciar | http |
| Demais projetos | Nenhum | — |

Se necessário, habilite os perfis de inicialização de múltiplos projetos nas opções do Visual Studio. Consulte a [documentação do Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects?view=visualstudio).

Não deixe instâncias do terminal ocupando 5099/5125 ao usar F5. Não selecione `DemoDatabase` como projeto de inicialização. Os scripts de terminal utilizam `--no-launch-profile`.

## Conexão e proteção dos dados

A API carrega `ConnectionStrings:AdSync` e `Jwt:Key` dos User Secrets em desenvolvimento ou de variáveis de ambiente no Docker. A chave JWT exige pelo menos 32 caracteres.

No Visual Studio, use **ADSync.Api → Gerenciar Segredos do Usuário**. Não copie valores para frontend, Git, logs, screenshots ou documentação.

O script da API recusa a execução se encontrar `ConnectionStrings__AdSync` no ambiente, evitando uma sobrescrita acidental da conexão. Caso tenha definido essa variável no terminal, remova apenas a sobrescrita desse processo:

```powershell
Remove-Item Env:ConnectionStrings__AdSync -ErrorAction SilentlyContinue
```

Configuração atual em `ADSync.Api/appsettings.json`:

```json
{
  "Safety": { "ReadOnly": false },
  "Offline": { "Enabled": true },
  "Mtbf": { "Enabled": true },
  "Seed": { "Enabled": false },
  "Preventivas": { "GeracaoAutomaticaHabilitada": true }
}
```

No cliente, `ReadOnlyMode=false`, `TechnicalOfflineEnabled=true` e `StorageScope=supabase`. Pendências de outros ambientes não devem ser reutilizadas no banco real.

O modo somente leitura permanece disponível como proteção. Para utilizá-lo, configure `Safety:ReadOnly=true`, desative seed e geração preventiva e defina `ReadOnlyMode=true` no cliente, utilizando perfis/scripts que não sobrescrevam esses valores. A API bloqueia mutações de negócio e intercepta gravações EF. Caso o Identity precise gravar durante o login, a autenticação poderá ser recusada.

Não há inicialização, migration ou alteração automática do esquema no startup. O banco deve possuir previamente as estruturas necessárias, incluindo recibos offline e registros de horas de operação.

## Executar com Docker

Com Docker e Docker Compose instalados, configure privadamente `.env.docker` a partir de `.env.docker.example`. No Windows, caso os User Secrets já estejam configurados:

```powershell
& ./tools/Initialize-DockerEnv.ps1
docker compose up --build -d
```

O script cria o arquivo apenas se ele não existir, sem exibir credenciais. Mantenha `.env.docker` fora do Git e confira os controles de gravação e geração preventiva antes de iniciar.

Abra **http://localhost:8080/**. O container serve API e frontend na mesma origem, enquanto o PostgreSQL/Supabase permanece externo. O Node.js prepara Vue e CSS durante o build e não integra a imagem final como servidor.

Para encerrar, utilize `docker compose down`.

### Executar em outra máquina

Com a imagem pronta:

```powershell
docker save -o adsync.tar adsync:local
```

Na outra máquina, com Docker instalado, carregue a imagem, utilize `compose.portable.yaml` e configure privadamente `.env.docker` para o ambiente autorizado:

```powershell
docker load -i adsync.tar
docker compose -f compose.portable.yaml up -d
```

A porta Docker está vinculada ao computador local. Uma publicação pública exige HTTPS e revisão da segurança do ambiente.

## Visibilidade das ordens de serviço

### Atribuição automática na abertura (RF02 / RN02)

Novas O.S. abertas por Operador/Gestor recebem o técnico ativo com menor quantidade de ordens abertas, em andamento ou pausadas. Concluídas e canceladas não entram na carga. Em empate, têm preferência os técnicos sem histórico de atribuição, depois aqueles que receberam a última ordem há mais tempo (data de abertura), e por último o Id para desempate estável. Essa regra não considera disponibilidade por turno ou especialidade.

Sem técnico ativo, a O.S. permanece Aberta e sem responsável, disponível ao Gestor para atribuição manual. Ordens antigas não são redistribuídas. Chamados offline recebem atribuição quando chegam ao servidor; preservam a data original e os reenvios não trocam o responsável nem duplicam a O.S. Aberturas simultâneas são serializadas por uma trava transacional do PostgreSQL. Planos preventivos mantêm o responsável definido no plano.

| Perfil | Acesso principal |
| --- | --- |
| Operador | Abrir e acompanhar as próprias ordens, além de editar suas descrições. |
| Técnico | Consultar ordens atribuídas, iniciar, pausar, retomar, concluir e registrar peças; consultar equipamentos, estoque e preventivas. |
| Gestor | Consultar todas as ordens, abrir e atribuir chamados, editar descrições, administrar cadastros, planos, usuários, indicadores e relatórios. |

Na API, o Gestor consulta todas as ordens; o Operador consulta somente as que abriu; o Técnico consulta somente as atribuídas a ele. A restrição vale para listagem e detalhes. Ordens fora do escopo retornam 404, assim como identificadores inexistentes.

O Técnico só pode iniciar uma ordem atribuída a ele. Pausar exige motivo; registrar peças exige ordem em andamento/pausada e peça/quantidade válidas. Concluir exige ordem em andamento. A causa da falha é obrigatória em corretivas e opcional em preventivas.

A edição da descrição exige conexão e permite até 2000 caracteres. O Operador pode editar apenas as próprias ordens; o Gestor pode editar qualquer ordem. A versão atual ainda não restringe essa edição por status.

## Cadastros do gestor

A área **Usuários**, exclusiva do Gestor, oferece busca e consulta de contas ativas/inativas. Em ambiente autorizado para escrita, permite criar usuários com senha inicial, editar nome/e-mail/perfil, ativar/desativar contas e redefinir senhas. O Gestor não pode desativar a própria conta nem retirar seu próprio perfil de Gestor.

A redefinição exige nova senha e confirmação, com validação pelo ASP.NET Core Identity. Não se trata de recuperação autônoma por e-mail. A API consulta a situação e o perfil atual a cada requisição autenticada; contas desativadas e tokens com perfil antigo são recusados. Alterações de usuários exigem conexão.

A área **Cadastros** permite consultar e buscar equipamentos, peças e causas de falha, incluindo registros inativos quando aplicável. Em ambiente autorizado para escrita, oferece criação e edição de cadastros, ativação/inativação de equipamentos e causas e entrada de estoque de peças. A edição da peça não altera seu saldo; reposições utilizam a operação específica de entrada.

Na área **Equipamentos e peças**, Gestor e Técnico consultam as tabelas, o estoque, a criticidade das peças e o histórico de ordens disponível para seu perfil.

## Planos preventivos (RF13)

O Gestor cadastra planos por equipamento, técnico responsável, descrição da tarefa, periodicidade em dias e próxima execução. A geração automática verifica planos vencidos e ativos, com equipamento e técnico habilitados, abre a ordem preventiva e avança a próxima data do plano.

O Técnico responsável também pode iniciar a manutenção diretamente pela área **Preventivas** a partir da data prevista no horário de Brasília. Caso exista uma ordem aberta, em andamento ou pausada daquele plano, ela é reaproveitada. A criação automática e o início pelo técnico compartilham uma trava transacional para evitar ordens duplicadas.

O início pelo plano exige conexão. Ordens preventivas já preparadas no dispositivo seguem o atendimento offline compatível.

## Indicadores e relatórios mensais

O painel do Gestor permite filtrar indicadores por período e setor, acompanhar comparações com o período anterior, gráficos, rankings e resumos de manutenção.

- **MTTR:** média do tempo ativo de reparo das corretivas concluídas com registros válidos; pausas não entram no cálculo.
- **MTBF:** horas de operação divididas pelas falhas corretivas registradas, considerando equipamentos com cobertura completa de horas no período. As horas são lançadas pelo Gestor.
- **Percentual de preventiva:** ordens preventivas concluídas divididas pelo total de ordens concluídas no período, multiplicado por 100.

O relatório mensal reúne atividades, indicadores, peças utilizadas, falhas recorrentes, recomendações justificadas e agenda preventiva. A estimativa financeira é opcional e depende das premissas informadas pelo Gestor; não representa economia comprovada.

Na área **Relatórios mensais**, selecione o mês, gere o relatório e utilize **Imprimir / salvar PDF** para abrir a impressão do navegador e salvar em PDF. Não há exportação direta para Excel ou CSV nesta versão.

## PWA e funcionamento offline

`ApiBaseUrl` no cliente Development aponta para 5099; o CORS permite a origem 5125. Fora de Development, o padrão é mesma origem com encaminhamento de `/api`, ou URL configurada com CORS. Nunca coloque segredos em `wwwroot`.

Para a experiência PWA completa, com a API em execução e as dependências restauradas:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Start-Pwa.ps1
```

Abra **http://127.0.0.1:5180**. O botão **Instalar NexService** depende da confirmação do navegador. O endereço 5125 é destinado ao desenvolvimento e não oferece a mesma recarga offline da PWA Release com service worker ativo.

A preparação offline do Técnico é automática após login e reconexão. Operadores e gestores podem abrir chamados sem rede; técnicos podem registrar início, pausa, retomada, uso de peças e conclusão em ordens previamente preparadas. As ações ficam no armazenamento local do navegador até a confirmação do servidor.

Identificadores únicos e recibos de sincronização evitam duplicações. O servidor valida permissões, situação da ordem e disponibilidade das peças. Em caso de conflito, as pendências precisam ser revisadas.

Login inicial, edição de descrições, cadastros, alteração de usuários e planos, início pelo plano preventivo, indicadores e relatórios exigem conexão. Sincronize pendências antes de limpar o armazenamento ou atualizar a PWA.

## Validar sem alterar o Supabase

Para conferir os controles da API:

```powershell
Invoke-RestMethod http://localhost:5099/api/runtime
```

Na configuração atual, espera-se `readOnly=False`, `seedEnabled=False` e `preventiveEnabled=True`.

Para consultar o banco utilizando o modelo EF existente, sem seed, alteração de esquema ou inicializador:

```powershell
& ./tools/Use-LocalSdk.ps1
dotnet run --project tools/ReadOnlyDatabase
```

O utilitário lê os User Secrets, faz consultas sem rastreamento e imprime resultados resumidos sem expor credenciais. Sua execução pressupõe acesso autorizado ao banco.

O projeto mantém scripts e configurações de automação de testes, incluindo Playwright. Nesta distribuição, as pastas `docs/` e `tests/` não acompanham o código. Os comandos que dependem desses arquivos exigem sua recuperação antes da execução.

**Não execute** `tools/DemoDatabase`, `EnsureCreated`, `EnsureDeleted`, migrations, resets, scripts de alteração de esquema ou testes de gravação contra o Supabase real sem autorização e ambiente apropriado. Validações com dados fictícios devem utilizar banco local isolado.

## Limites e próximos passos

O ADSync encontra-se em fase de validação, com foco no aprimoramento das funcionalidades existentes e na preparação para aplicação em um ambiente industrial real.

As próximas etapas incluem melhorias na associação do histórico de equipamentos, atualização da documentação, ampliação dos testes e validação do sistema em diferentes condições de conectividade e operação.

Como possibilidades de evolução, destacam-se a integração com sensores e CLPs industriais, o monitoramento de parâmetros operacionais e o aprimoramento dos mecanismos de segurança, backup e recuperação de dados.
