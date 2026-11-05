# Integração com o Supabase existente

> Histórico da validação somente leitura. Posteriormente, o usuário autorizou sair desse modo para testar no banco existente. A configuração atual permite gravações, mantendo seed e geração automática de preventivas desativados. Consulte a atualização no README. A fila do Supabase tem armazenamento separado da demonstração local.

A orientação atual substitui o preparo de banco demonstrativo. A conexão existente permanece nos User Secrets da API; nenhum valor foi exibido, versionado ou copiado para arquivos públicos. Não foi executado inicializador, seed, migration, reset, recriação, script SQL ou teste de criação de ordens/consumo de estoque no Supabase.

## Controles de execução

- `Seed:Enabled=false` por padrão: `DbSeeder.SeedAsync` só é chamado em Development se houver habilitação explícita.
- `Preventivas:GeracaoAutomaticaHabilitada=false` por padrão: o job que cria ordens não é registrado.
- `Safety:ReadOnly=true` por padrão: middleware recusa operações de negócio que gravam e interceptor bloqueia `SaveChanges`/`SaveChangesAsync`, inclusive chamadas internas do Identity.
- API recusa inicialização se modo somente leitura estiver combinado com seed/job habilitados.
- `tools/Start-Api.ps1` força esses três controles, usa os User Secrets existentes e recusa sobrescrita de conexão no ambiente. Não define connection string.
- Perfis do Visual Studio conservam as portas anteriores; todos desativam seed/job e ativam somente leitura. O perfil NexService Local usa API em 5099, **não um banco local**.
- `ADSync.slnLaunch` oferece **NexService - Supabase (somente leitura)** para F5, iniciando API (NexService Local) e cliente (http) juntos. Os perfis ativos nas preferências locais da IDE foram ajustados sem alterar as demais configurações. Compilação validada após o ajuste; o clique de F5 dentro da interface do Visual Studio não foi executado nesta sessão. A seleção inicial do perfil fica a cargo do usuário na IDE.
- `tools/Start-DemoApi.ps1` agora é apenas um alias do script seguro. Preparação de banco local e configuração demonstrativa foram desativadas. Os dados locais anteriores não foram apagados; o processo PostgreSQL portátil foi parado.
- Cliente em `ReadOnlyMode=true`: ações de escrita ficam ocultas, botão de sincronização desativado e temporizador não envia pendências. Consultas usam a API diretamente, sem reaproveitar cache demonstrativo. A sessão de validação usa chave própria em `sessionStorage`, sem reutilizar a sessão antiga do banco local. As pendências persistidas antigas são preservadas.
- `GET /api/runtime` permite verificar os três controles sem expor configuração sensível.

O login normal usa o Identity existente. Se ele precisar atualizar contador de falhas, resetar bloqueio ou refazer hash, o interceptor impedirá essa gravação; nesse caso o login pode retornar 403. Não há bypass de senha nem atualização automática de credenciais. Não habilite gravações apenas para contornar essa condição sem analisar e obter autorização.

## Verificações desta etapa

| Verificação | Resultado |
| --- | --- |
| Build Debug com SDK .NET 10.0.403 | Executado: zero avisos e zero erros |
| Leitura direta com o modelo EF existente | Executado: 3 perfis, 17 equipamentos, 5 peças, 2 ordens e 1 plano no momento da consulta |
| Identity e enums | Executado: consultas e materialização de registros existentes, sem mudanças de modelo/esquema |
| Bloqueio de SaveChanges | Executado: testes síncrono/assíncrono em contexto com conexão local inutilizável; interceptor interrompe antes de conectar. Nenhum banco real é usado nesses testes |
| Login existente e frontend | Executado: login da conta de gestor já existente e consultas de ordens, equipamentos/peças, preventivas e indicadores pelo navegador móvel, sem chamadas de negócio que gravam |
| Consultas consecutivas | Executado: 35 GETs autenticados consecutivos com HTTP 200, sem exceção de providers internos do EF |
| Sessão/fila anteriores | Executado: contexto novo com sessão antiga simulada; login exigido novamente, pendência preservada e nenhum reenvio depois de 20 segundos |
| Logout e viewport | Executado: sessão de validação removida; viewport 390 × 844 sem rolagem horizontal |

`tools/ReadOnlyDatabase` faz somente consultas EF (`AsNoTracking`, contagens e amostras). Carrega a conexão diretamente dos User Secrets. Não cria, inicializa nem atualiza bancos ou esquemas; erros são resumidos sem despejar conexão, nomes de servidor ou credenciais.

`tests/read-only.cjs` valida `/api/runtime`, faz login com conta existente e percorre as áreas de consulta do navegador. Nenhuma operação de negócio que grava é enviada. Usa contexto novo, simula uma pendência antiga apenas no armazenamento desse navegador e cruza o intervalo de 20 segundos para verificar ausência de reenvio. A senha entra por `READONLY_PASSWORD` ou é lida privadamente do segredo existente; não é impressa nem salva em screenshots/logs. Se as credenciais não corresponderem à conta existente, interrompe com erro resumido. Não cria conta.

Os testes demonstrativos antigos permanecem como histórico e são bloqueados por padrão. O wrapper exige opção explícita para testes destrutivos e recusa API somente leitura; os scripts diretos também exigem opt-in. Esses controles não autorizam testes no Supabase: autorização do usuário continua necessária.

Não foram testados nesta etapa criação/edição/conclusão de ordens, baixa de estoque, geração de preventivas, alterações de usuários nem migrações. Os resultados de escrita em `MVP.md` referem-se exclusivamente ao banco local da etapa anterior. Nenhuma necessidade de alteração estrutural foi identificada nas consultas executadas.
