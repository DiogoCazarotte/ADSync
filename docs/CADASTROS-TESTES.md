# Testes locais dos cadastros

Ambiente separado do Supabase. Não usa nem altera seus User Secrets. O banco tem dados fictícios próprios; não há cópia do banco remoto.

| Recurso | Endereço |
| --- | --- |
| Frontend de testes | http://127.0.0.1:5126 |
| API de testes | http://127.0.0.1:5100 |
| PostgreSQL de testes | 127.0.0.1:55433, banco adsync_dev |
| Dados PostgreSQL | .local-demo/pgdata-cadastros |
| Login e senha exclusivos | .local-demo/cadastros/credentials.json |

A aplicação original em 5099/5125 continua somente leitura. No ambiente de testes, o perfil Gestor pode gravar. Use exclusivamente a URL 5126 para testar os cadastros. As contas criadas pelo seed são fictícias, apesar dos endereços de exemplo; nenhum e-mail é enviado.

## Iniciar

No terminal do VS Code, na raiz do projeto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Start-CadastroTests.ps1
```

Requer o SDK e o PostgreSQL portátil já presentes em `.local-demo`, Node.js e dependências NuGet restauradas. O script cria um cluster separado, preserva dados entre execuções, compila cópias isoladas e inicia API/cliente sem janelas adicionais. Se as portas estiverem ocupadas, encerra sem iniciar outra instância.

Abra o arquivo local de credenciais no VS Code e use os campos `Email` e `Password`. Não compartilhe esse arquivo; `.local-demo` está ignorada pelo Git. O PostgreSQL local aceita conexões sem senha apenas em loopback; é destinado a esta máquina de desenvolvimento, sem exposição de portas na rede.

O frontend publicado para testes fica em `.local-demo/cadastros/client`. Alterações no código frontend devem ser feitas em `ADSync.Client` pelo VS Code; para atualizar a cópia, encerre API/cliente de testes e execute o script novamente.

## Validação automatizada

Para gestão de usuários, execute `node tests/usuarios.cjs`. Valida criação e edição pela interface, perfis, e-mail duplicado, restrição ao Gestor, proteção contra desativar/rebaixar a própria conta, rejeição de tokens com perfil anterior e bloqueio de acesso de contas inativas. As contas fictícias permanecem no banco local para inspeção.

Com o ambiente aberto:

```powershell
node tests/cadastros.cjs
```

Usa Playwright e Brave instalados nesta máquina (ou `CHROMIUM_PATH`). Valida pela interface: criação, edição, ativação/inativação de equipamentos e causas; criação/edição de peças e reposição com conferência de saldo. Cria registros com nomes únicos e os preserva no banco local para inspeção. Não executa testes antigos contra a API Supabase.

## Encerrar

No PowerShell, identifique os processos que escutam **5100 e 5126**, confira os comandos e encerre somente a API em `.local-demo/cadastros/api/ADSync.Api.dll` e o Node executando `tools/serve-cadastro-tests.cjs`. As instâncias iniciadas pelo script ficam registradas em `.local-demo/cadastros/processes.json`; confira a identidade antes de encerrar, pois PIDs podem ser reutilizados. Não encerre a aplicação original nas portas 5099/5125.

Para encerrar o banco de testes, sem apagar dados:

```powershell
& ./.local-demo/postgresql/pgsql/bin/pg_ctl.exe -D .local-demo/pgdata-cadastros -m fast -w stop
```

Logs ficam em `.local-demo/cadastros`. Nenhum reset ou exclusão de banco faz parte deste fluxo.
