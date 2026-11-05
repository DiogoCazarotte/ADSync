# NexService: MVP demonstrativo

> **Histórico da etapa anterior, realizada em banco demonstrativo local.** Não representa validação no Supabase. Após a orientação de preservar o banco existente, o fluxo local foi desativado, o PostgreSQL portátil foi parado sem exclusão dos dados e os testes com gravação foram bloqueados. Use o [README atual](../README.md) e a [validação do Supabase](SUPABASE.md). Não execute os passos de abertura/consumo de peças deste roteiro no Supabase sem autorização.

Reconstrução local em Windows, em 04/11/2026. O repositório inspecionado tinha o backend de manutenção e o cliente template Blazor. Não havia `AGENTS.md` no repositório ou nos diretórios ancestrais consultados, nem arquivos do MVP da nuvem. O Git foi localizado na instalação do Visual Studio; o diff inicial mostrou apenas as primeiras edições desta execução, confirmando ausência de alterações versionadas preexistentes. User Secrets existentes e perfis antigos foram preservados. Não houve commit, push, deploy nem alteração de recursos remotos.

## Entregue no código

- Interface NexService em verde-petróleo, responsiva, cultura pt-BR, login por e-mail/senha e navegação por perfil. Autorização de ações continua na API.
- Ordens ordenadas por criticidade, busca por equipamento/número, filtros por status/prioridade. A consulta usa `api/ordensservico?minhas=true`: operador vê solicitações próprias, técnico vê atribuições e gestor vê todas.
- Abertura por operador/gestor; atribuição pelo gestor; início, pausa com motivo, retomada, peça/quantidade e conclusão com causa/observações pelo técnico. Detalhes mostram responsável, diagnóstico, trabalho ativo, eventos e peças.
- Equipamentos e peças em consulta; histórico usa as ordens disponíveis para o perfil atual. Administração completa não foi entregue.
- Consulta de preventivas; criação de plano pelo gestor com equipamento, técnico, tarefa, periodicidade e primeira execução. Reutiliza o job existente (primeira rodada 10 segundos após iniciar a API, depois a cada hora).
- Indicadores somente para gestor, período selecionável, resumo e setores. Nulos aparecem como “—”. Contagens reais podem ser zero. `MtbfDias` aparece como **Intervalo médio entre chamados**: mede intervalos entre aberturas de corretivas do conjunto consultado, não horas reais de operação. MTTR mostra trabalho ativo descontando pausas, não toda a indisponibilidade.
- `ManutencaoClient` concentra HTTP autenticado (timeout 15 s), erros, fila e cache por conta. Token e sessão em `sessionStorage`; consultas e fila em `localStorage`. Cache informa data de obtenção e condição desatualizada quando a rede falha. Atualização das consultas é manual.
- Somente abertura tem fila offline: persiste antes do envio, conserva `ClientSyncId`/data original e só remove depois de confirmação **e** persistência local da remoção. Falhas de rede/autenticação/validação mantêm pendências. Falha na primeira gravação mantém formulário e impede mensagem falsa de sucesso. Falha na remoção mantém fila para reenvio idempotente.
- Tentativa automática a cada 20 segundos com o app aberto e botão manual. Logout limpa sessão e dados em memória, preservando fila/cache por conta. Operações são serializadas por semáforo e ações da tela impedem troca de conta enquanto estão em andamento. Não há coordenação entre abas/dispositivos.
- Ações técnicas exigem conexão e não entram em fila/reenvio automático: uma resposta perdida na baixa de peça não pode provocar repetição automática.
- API reutiliza uma instância de `NpgsqlNullNameTranslator` em ambos os conjuntos de cinco mapeamentos. Nenhum aviso do EF foi suprimido. Idempotência verifica o autor; outro usuário recebe 403. O controller continua definindo o autor pelo token.
- `tools/DemoDatabase` cria o modelo atual com `EnsureCreatedAsync` e `os_numero_seq`, usando os cinco enums. Só permite host exatamente `127.0.0.1` e banco `adsync_dev`, validados por parser. Não apaga dados, não corrige esquema antigo e não deve ser misturado com migrations.
- Manifesto PWA e service worker Release; API configurável por `ApiBaseUrl`, padrão de mesma origem fora de Development. Não há segredos no cliente.

## Como apresentar

Preparação e comandos completos no [README](../README.md). Inicie o PostgreSQL demonstrativo, inicialize o esquema se necessário, depois API em 5099 e cliente em 5125. Abra http://localhost:5125/. Para PWA inteiramente offline, use a versão Release servida em http://127.0.0.1:5180/ com proxy `/api`.

1. **Renata / Operadora** (`renata.operadora@sustennutri.com.br`): entre com `Seed:SenhaDev`, abra chamado com equipamento, descrição e criticidade. Mostre a consulta de solicitações próprias, busca e filtros. Bloqueie apenas a API para demonstrar a fila no desenvolvimento; recarregue e confira a pendência preservada. Reconecte e sincronize. Somente a abertura tem esse suporte.
2. **Paulo / Gestor** (`paulo.gestor@sustennutri.com.br`): entre, localize o chamado, abra detalhes e atribua Carlos. Consulte equipamentos/peças e histórico; crie preventiva com data futura. Consulte indicadores por período e explique os significados de MTTR e intervalo médio. Para mostrar “—”, escolha período sem ordens concluídas.
3. **Carlos / Técnico** (`carlos.tecnico@sustennutri.com.br`): confira as atribuições, inicie, pause com motivo, retome, registre uma peça em estoque e conclua com causa e observações. Mostre os eventos, tempos e diagnóstico. Essas ações dependem de conexão.
4. Volte ao gestor para conferir conclusão/indicadores. As consultas têm botão de atualização; não há atualização em tempo real entre usuários.
5. Para demonstrar a PWA offline, faça primeira visita/login conectado, aguarde o service worker assumir, desligue toda a rede do navegador, abra chamado e recarregue. Reconecte e mantenha a tela aberta: a fila tenta sincronizar a cada 20 segundos, sem clique.

Todas as contas são fictícias; a senha não é documentada em texto. Consulte-a privadamente no gerenciador de User Secrets da API no Visual Studio. Primeiro acesso/login exige rede. Ao fechar a aba, a sessão pode terminar; pendências ficam no dispositivo e reaparecem após login na mesma conta. Não limpe dados do navegador antes de sincronizar.

## Validação local

Ambiente: Windows, PowerShell, Visual Studio 2026 Insiders instalado. SDK global 11 preview inicialmente; SDK **10.0.403** portátil baixado da distribuição oficial e SHA-512 comparado com metadados oficiais. PostgreSQL **17.11** portátil, processo sem serviço, escutando somente `127.0.0.1:55432`; banco `adsync_dev`. Chromium do Brave executado por Playwright **1.64.1**, viewport **390 × 844**. Dependências .NET declaradas foram mantidas.

| Verificação | Resultado |
| --- | --- |
| Restore e build Debug da solução com SDK 10 | Executado: zero avisos e zero erros |
| Publish Release do cliente | Executado: gerado; recomendação opcional `wasm-tools`, sem necessidade para o teste |
| Inicializador em banco novo e repetido | Executado: modelo criado e execuções posteriores sem apagar dados |
| Restrições do inicializador | Executado: recusou host com sufixo malicioso e nome de banco diferente antes de conexão |
| Mesmo `ClientSyncId`/autor | Executado: mesma ordem retornada |
| Mesmo identificador por outra conta | Executado: HTTP 403 |
| Autor enviado pelo cliente | Executado: API usa nome/identidade do token, ignorando autor forjado |
| 35 consultas autenticadas consecutivas | Executado: todas com sucesso, sem exceção de providers internos |
| Fluxo móvel gestor/técnico | Executado: abrir, atribuir, iniciar, pausar, retomar, peça e concluir; eventos, causa, observações e baixa de estoque conferidos na API |
| Perfis e listagem | Executado: navegação por perfil; técnico sem painel, API de indicadores devolve 403; ordens consultadas com `minhas=true` |
| Fila com API bloqueada | Executado: preservação após recarga/logout, isolamento por conta, uma ordem após reconectar, mesma data original |
| Viewport móvel | Executado: sem rolagem horizontal; captura em `tests/artifacts/mobile.png` |
| PWA Release sem rede | Executado: service worker ativo, abertura e recarga offline, sincronização automática após reconectar, uma ordem no servidor |
| Falha inicial de armazenamento | Executado: formulário preservado, fila vazia e nenhum envio antes da persistência |
| HTTP 400/401 e falha ao remover fila | Executado: fila preservada; reenvio depois da falha local sem duplicar ordem no servidor |
| Resposta perdida depois da gravação | Executado: reenvio resulta em uma única ordem; token encontrado somente em `sessionStorage` |
| Operação em andamento | Executado: formulário e botão Sair desabilitados enquanto o envio aguarda resposta |
| Preventivas e estoque na interface | Executado: gestor criou plano e consultou equipamentos/peças; plano confirmado na API |
| Indicadores sem dados | Executado: período em 2099 exibiu “—” para MTTR, sem fabricar zero |
| Job preventivo existente | Executado: plano vencendo hoje, reinício da API e rodada real do job; uma ordem atribuída e próxima execução avançada |

Os testes podem criar chamados extras em execuções interrompidas. Os registros foram mantidos e não houve limpeza destrutiva. Cada execução do fluxo técnico consome uma peça do banco demonstrativo. Os testes da nuvem não foram considerados evidência local; os resultados acima vêm desta máquina.

Durante a validação, seletores sem nome acessível explícito impediram a primeira execução automatizada; foram corrigidos na interface e os testes passaram. Também houve bloqueio de DLL ao compilar com a API aberta e recurso Blazor 404 após publicar com servidor de desenvolvimento antigo; parar os processos para build/publish e reiniciá-los resolveu os problemas. As execuções finais documentadas acima passaram com o SDK 10.

Não foram executados testes de carga, múltiplos dispositivos/abas concorrentes, recuperação de desastre, hospedagem HTTPS real, navegadores móveis físicos, isolamento completo de permissões/dados para produção ou migração para um banco novo em nuvem. A estrutura de teste não equivale a uma auditoria de segurança. Internet intermitente durante a abertura foi exercitada; atendimento técnico offline continua fora do escopo.

## Pendências para piloto real

- Atendimento técnico offline com idempotência transacional, especialmente baixa de estoque.
- Concorrência e coordenação entre abas/dispositivos; conflitos e expiração/renovação da sessão.
- Atribuição automática: critério depende da empresa.
- MTBF real com horas de operação e indisponibilidade; definição operacional de MTTR.
- SLA e retrabalho previstos no PDF: ainda sem critérios implementados nesta entrega.
- Migrações completas para inicialização de um banco novo em nuvem.
- Administração de usuários, equipamentos, causas e estoque; edição/inativação de planos na interface.
- Hosting HTTPS, backups, recuperação de acesso e política de retenção/remoção de dados no celular.
- Paginação, atualização das consultas e evolução de desempenho.

Arquitetura proposta: **PWA em hosting HTTPS → API em serviço gerenciado → PostgreSQL gerenciado**, sem servidor na fábrica. Serviços não foram contratados, recursos remotos não foram alterados e nenhum deploy foi realizado.
