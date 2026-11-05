# Atendimento offline do técnico — RF08, RF09 e RN06

Implementação testada localmente e ativada após autorização do usuário. A tabela de recibos foi aplicada e validada no Supabase; `Offline:Enabled=true` na API e `TechnicalOfflineEnabled=true` no cliente. Seed e geração automática de preventivas permanecem desligados. A verificação de instalação não criou ordens nem consumiu estoque.

## Uso

1. Entre como Técnico enquanto estiver conectado, na mesma conta e no mesmo navegador que utilizará em campo.
2. Aguarde a indicação **Seus atendimentos estão salvos para uso offline**. O cliente baixa automaticamente as ordens atribuídas, seus detalhes e os cadastros necessários após o login, periodicamente e ao voltar a conexão. Não é preciso clicar para preparar. Uma falha de armazenamento impede anunciar a preparação como concluída.
3. Nos detalhes de uma ordem preparada, registre início, peças, pausa, retomada e conclusão. Cada ação é validada localmente e salva antes de atualizar o estado apresentado. A tela identifica os registros ainda pendentes; o estoque mostrado é uma estimativa local até a validação do servidor.
4. Mantenha o aplicativo aberto quando a conexão voltar. A reconexão inicia uma tentativa de sincronização; há também novas tentativas a cada 20 segundos. **Tentar sincronizar** continua disponível. Eventos posteriores são enviados somente após a confirmação do anterior. A preparação é atualizada aproximadamente a cada minuto enquanto houver conexão, sem bloquear a edição durante os downloads.

O modo Release/PWA com service worker instalado permite recarregar sem rede. O servidor de desenvolvimento do Visual Studio não fornece recarga integral offline. Primeiro login e preparação exigem conexão; não se cria uma sessão nova sem autenticação online. Após logout, dados locais são preservados e somente a mesma conta volta a acessá-los. Apagar dados do navegador ou desinstalar a PWA antes de sincronizar perde as pendências.

## Instalação e execução da PWA

Para publicar e servir localmente, execute `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Start-Pwa.ps1`. O script publica uma versão Release em uma pasta exclusiva, troca a versão ativa somente após sucesso e inicia/reutiliza o servidor em `http://127.0.0.1:5180`. A API deve estar em execução na porta 5099, por exemplo pelo Visual Studio. O servidor encaminha `/api/` para essa API na mesma origem; não há alteração do Supabase durante a publicação. Usar a mesma URL nas visitas seguintes para manter o armazenamento da conta.

A aplicação mostra **Instalar NexService** quando o navegador oferece instalação. Essa ação depende da confirmação do usuário; quando não houver botão, são exibidas instruções para o menu do navegador e para iPhone/iPad. Mesmo sem instalar, o service worker prepara os arquivos automaticamente no primeiro acesso. A indicação **Aplicativo disponível offline** refere-se aos arquivos da aplicação; a indicação dos atendimentos refere-se aos dados da conta. Aguarde ambas antes de desligar a rede.

Novas versões são baixadas em segundo plano e não substituem à força uma aplicação aberta. Ao ver o aviso de atualização, sincronize as pendências, feche todas as janelas e reabra. O cache guarda somente arquivos estáticos; API e gravações nunca recebem uma página HTML como resposta offline. A instalação não garante envio com o aplicativo totalmente fechado: a fila é sincronizada com a aplicação aberta. Acesso por celular fora desta máquina exigirá hospedagem acessível com HTTPS; o endereço 127.0.0.1 é apenas para teste neste computador.

## Integridade

- Cada evento tem identificador UUID, ordem, ação e horário UTC original. A fila e sua visão local são gravadas juntas em uma única entrada do armazenamento, separadas por ambiente e conta.
- Web Locks serializa alterações locais entre abas. O navegador precisa oferecer essa API, normalmente em contexto seguro (HTTPS ou localhost). A sincronização não mantém a tela bloqueada enquanto espera a rede.
- O endpoint `POST /api/ordensservico/{id}/eventos` exige Técnico. Confere o responsável atual antes de aplicar eventos novos e recusa eventos anteriores ao estado já confirmado.
- A alteração da O.S., a baixa de estoque e o recibo são confirmados na mesma transação. Uma trava transacional por ordem serializa reenvios; tokens `xmin` de ordem/peça detectam alterações concorrentes com os endpoints online.
- Repetir o mesmo identificador e conteúdo devolve a confirmação original sem refazer a operação. Reutilização do identificador com outra conta ou outro conteúdo é recusada.
- Horário de peças também fica registrado no recibo. Eventos de início/pausa/retomada/conclusão conservam seus horários no histórico normal da ordem.
- Estoque insuficiente, ordem reatribuída, transição inválida e sessão recusada mantêm a fila e exibem a falha. Nenhuma pendência é descartada automaticamente. O gestor deve resolver o conflito antes de tentar sincronizar novamente; não há mesclagem automática de intervenções conflitantes de dois dispositivos.
- Se a API local continuar acessível mas perder o acesso ao Supabase, erros transitórios de conexão retornam HTTP 503, sem expor detalhes do banco e sem presumir uma conta autorizada. O cliente trata erros temporários (408, 429 e 5xx) como indisponibilidade: usa cache para consultas, preserva a fila e permite continuar registrando o trabalho. A reconexão/temporizador tenta sincronizar novamente. Não confundir isso com rejeições reais (401/403/409), que exigem autenticação ou revisão do conflito. Filas antigas bloqueadas indevidamente por HTTP 500/502/503/504 são recuperadas sem apagar eventos.
- Quando o navegador cancela uma requisição, a API não tenta responder à conexão encerrada. Se a desconexão ocorrer durante o envio da resposta, o cancelamento é tratado sem gerar outra resposta de erro. Isso também vale para a verificação da conta; nenhum acesso é liberado em uma requisição cancelada.
- Se a gravação local falhar, o formulário é preservado e não se apresenta a ação como salva. Se a confirmação do servidor se perder, a repetição usa o mesmo identificador.
- Um tempo limite de sincronização não exige repetir o atendimento nem descarta o evento. A mensagem informa quantos eventos aguardam confirmação e que haverá nova tentativa automática. A API precisa estar em execução; a PWA aberta, por si só, não mantém o backend ativo. O teste inclui uma confirmação que ultrapassa o prazo depois de o servidor aplicar a operação, verificando que o reenvio não duplica o início.

## Ativação no Supabase

Script revisável: [sql/ATENDIMENTO-OFFLINE.sql](sql/ATENDIMENTO-OFFLINE.sql). Acrescenta somente `os_eventos_sincronizados`, com chave do evento, ordem, usuário, horário, hash do conteúdo e resposta de confirmação. Não altera tabelas de ordens/estoque nem os enums. Ativa RLS na tabela interna; a conexão da API deve ser proprietária ou ter uma política/permissão apropriada. Não conceder acesso anônimo aos recibos.

Após autorização e aplicação do script, habilitar `Offline:Enabled=true` na API e `TechnicalOfflineEnabled=true` no cliente (incluindo a configuração Development). Reiniciar os projetos e atualizar a PWA. Seed e geração automática de preventivas continuam desligados. `/api/runtime` informa `technicalOfflineEnabled` do servidor. Não existe alteração automática de esquema no startup.

Para interromper novos registros offline, desligar os controles depois de avaliar as pendências dos dispositivos; não apagar a tabela ou as filas. A tabela de recibos precisa ser preservada para que reenvios antigos continuem idempotentes.

## Verificação reproduzível

- `dotnet run --project tests/AssignmentChecks`: cria um banco de nome aleatório no PostgreSQL portátil `127.0.0.1:55433`, valida atribuição e eventos com repositórios reais e remove somente esse banco ao final. Cobre reenvios simultâneos, estoque, horários, reatribuição e rollback quando a gravação do recibo falha. Não lê User Secrets.
- Publicar cliente: `dotnet publish ADSync.Client -c Release -o .local-demo/cadastros/client`.
- `node tests/tecnico-offline.cjs`: abre a PWA publicada em porta aleatória, com API simulada e armazenamento novo. Cobre atendimento sem rede, recarga offline, sincronização por temporizador, resposta perdida, armazenamento cheio, duas abas, conflito persistido e isolamento por conta. Não acessa o Supabase.
- `dotnet run --project tests/OrderAccessChecks` e `dotnet run --project tests/ClientChecks`: regressões de permissões, bloqueios e separação do armazenamento.
- `dotnet run --project tests/ConnectivityChecks`: simula a cadeia de erro de transporte Npgsql antes de abrir qualquer conexão, verifica tratamento 503 e recusa de autenticação enquanto o banco está indisponível. O teste PWA também cobre navegador online/API acessível com banco offline, uso de cache, continuação do atendimento e sincronização após recuperação.
