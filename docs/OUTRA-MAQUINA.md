# Levar o ADSync para outro computador

A API e a interface ficam na imagem Docker. O banco continua no Supabase: o outro computador precisa de internet para login, consultas e sincronização. Não precisa instalar Visual Studio, .NET, Node.js ou PostgreSQL.

## Neste computador: gerar a imagem e os arquivos para levar

Abra o Docker Desktop em modo **containers Linux**. No PowerShell, na pasta do projeto:

```powershell
docker build -t adsync:local .
New-Item -ItemType Directory -Force artifacts/adsync-docker
docker image save --output artifacts/adsync-docker/adsync.tar adsync:local
Copy-Item compose.portable.yaml artifacts/adsync-docker/compose.yaml
Copy-Item .env.docker.example artifacts/adsync-docker/.env.docker.example
Copy-Item docs/OUTRA-MAQUINA.md artifacts/adsync-docker/LEIA-ME.md
```

Se algum comando falhar, resolva o erro antes de continuar. Copie a pasta **artifacts/adsync-docker** para um pendrive ou transfira-a ao outro computador. Ela contém a imagem pronta e os arquivos de execução, sem credenciais. A exportação usa [`docker image save`](https://docs.docker.com/reference/cli/docker/image/save/).

A imagem construída neste PC é **Linux x64 (linux/amd64)**, adequada a computadores Intel/AMD x64 com Docker Linux. Para uma máquina ARM, como um Mac com Apple Silicon, prefira copiar o código-fonte completo e construir a imagem diretamente nela com `docker build -t adsync:local .` na raiz do projeto.

## Na outra máquina: instalar e iniciar

1. Instale e abra o [Docker Desktop](https://docs.docker.com/desktop/). No Windows, use WSL 2 e containers Linux; conclua eventuais etapas de virtualização e reinicialização. Em Linux, pode usar Docker Engine com o plugin Compose. O Compose precisa ser 2.30.0 ou superior para aceitar o arquivo de configuração privado no formato `raw`.
2. Abra o PowerShell **dentro da pasta transferida**, que contém `adsync.tar` e `compose.yaml`.
3. Confira o Docker e carregue a imagem:

```powershell
docker info
docker compose version
docker image load --input adsync.tar
```

O comando deve informar `Loaded image: adsync:local`. Ele importa a imagem e sua identificação, conforme a [documentação do Docker](https://docs.docker.com/reference/cli/docker/image/load/).

4. Prepare a configuração privada **uma única vez**:

```powershell
Copy-Item .env.docker.example .env.docker
notepad .env.docker
```

Substitua os valores de exemplo de `ConnectionStrings__AdSync` e `Jwt__Key` pelos valores que o projeto já usa. Eles estão nos User Secrets da API neste computador; o arquivo `.env.docker` existente aqui também contém esses valores. Se preferir, transfira esse arquivo diretamente por um meio privado e pule a cópia do exemplo. O arquivo contém senha de banco e chave de autenticação: mantenha-o fora de repositórios e compartilhamentos públicos.

Use a conexão **PostgreSQL** do Supabase, não a URL HTTP ou a chave `anon`. A chave JWT precisa ter pelo menos 32 caracteres. Mantenha cada valor em uma única linha, sem adicionar aspas ao redor do valor inteiro. Preserve as demais configurações do modelo.

5. Encerre a API antiga antes de iniciar a nova: a configuração atual habilita o agendador de preventivas, que deve rodar em apenas uma instância sobre esse banco. Sincronize antes eventuais pendências no navegador antigo; o cache e a fila offline não são transferidos pela imagem.
6. Inicie a aplicação:

```powershell
docker compose up -d
docker compose ps
```

7. Abra **http://localhost:8080** no navegador **da outra máquina** e faça login com sua conta existente.

O Compose transferido usa apenas a imagem carregada; não compila o projeto nem baixa uma imagem do ADSync. Não há criação de banco, usuários ou migrações. Os dados são os mesmos do Supabase. A porta fica disponível somente no computador em que o container está rodando.

## Conferir e parar

```powershell
Invoke-RestMethod http://localhost:8080/api/runtime
docker compose logs --tail=100 app
docker compose stop
```

O primeiro comando deve mostrar `readOnly=False`, `seedEnabled=False`, `preventiveEnabled=True` e `technicalOfflineEnabled=True`. Ele verifica a configuração da API; o login e as consultas verificam também a conexão com o Supabase.

Para voltar a rodar, abra o Docker Desktop e execute `docker compose start` nessa pasta. Para remover o container, use `docker compose down`; os dados continuam no Supabase. Se a porta 8080 estiver ocupada, altere `127.0.0.1:8080:8080` para `127.0.0.1:8081:8080` no `compose.yaml` e acesse `http://localhost:8081`.

## Atualizar depois de alterar o projeto

Gere outra imagem e exporte novamente neste computador. Na outra máquina, substitua apenas `adsync.tar`, preserve `.env.docker` e execute:

```powershell
docker image load --input adsync.tar
docker compose up -d --force-recreate
```

O Compose recria o container com a imagem atualizada. [Referência de execução](https://docs.docker.com/reference/cli/docker/compose/up/).
