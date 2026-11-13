# Relatórios mensais

Disponível em **Menu → Relatórios**, exclusivamente para o Gestor. Escolha o mês, gere a consulta e use **Imprimir / salvar PDF** para abrir a impressão do navegador. Selecione o destino **Salvar como PDF**. Não exige serviço externo nem uma nova dependência npm/NuGet.

O relatório reúne resumo executivo, comparação com o mês anterior completo, distribuição por setor, atividades concluídas com diagnóstico/observações/técnico/peças, consumo consolidado, recomendações fundamentadas, agenda preventiva atual e estimativa financeira opcional.

## Critérios

- `GET /api/relatorios/mensal?ano=2026&mes=11`, com autenticação e papel Gestor. Parâmetros inválidos ou meses futuros retornam 400. Cache HTTP desabilitado; a interface consulta online sem cache de negócio.
- Mês civil em **UTC**, início inclusivo e início do mês seguinte exclusivo. O mês corrente termina no instante da consulta e recebe indicação de resultado parcial. O mês anterior é o mês civil completo, mesmo que tenha outra quantidade de dias.
- Atividades: somente O.S. concluídas pela data de conclusão, incluindo as abertas antes do mês. A contagem de aberturas é independente e inclui ordens depois canceladas.
- Tempo ativo: soma dos trechos válidos entre início/retomada e pausa/conclusão. Inclui o trabalho anterior ao mês das ordens concluídas no mês; não representa horas de calendário trabalhadas exclusivamente dentro daquele mês. Ordens sem sequência válida são excluídas das métricas de tempo, com amostra/exclusões explícitas. MTTR considera apenas corretivas e não mede toda a indisponibilidade.
- Peças: consumo acumulado nas ordens concluídas, sem atribuir uma data de utilização que o cadastro não registra. Não há preços unitários cadastrados.
- Recorrência: duas ou mais corretivas concluídas no mês para o mesmo equipamento ativo. Sugere avaliar plano novo ou revisar o existente, sem presumir causa nem criar ordens.
- Pendências de prioridade alta/crítica, estoque crítico zerado e agenda preventiva são a **situação atual na emissão**, inclusive ao consultar um mês antigo. A agenda inclui planos ativos com equipamento ativo, datas de geração vencidas e os próximos 30 dias. A data de geração vencida não comprova manutenção atrasada: pode haver uma O.S. já aberta. Responsáveis indisponíveis precisam de avaliação do gestor.
- Cadastros atuais podem mudar nomes e agrupamentos. Consultas posteriores podem incluir sincronizações novas. Não é um fechamento histórico imutável nem um arquivo armazenado no banco.

## Estimativa financeira

O cadastro atual não contém custo de parada, custo de peças/mão de obra nem comprovação de parada evitada. A seção opcional pede **custo estimado de parada por hora**, **horas estimadas como evitadas** e **fonte/justificativa**. Valores positivos, limitados nos campos, e justificativa são obrigatórios para exibir o resultado.

`Valor potencial bruto = custo estimado por hora × horas estimadas como evitadas`.

Identificado como estimativa não auditada do gestor; não representa economia comprovada, não desconta despesas e não pressupõe que toda preventiva evitou falhas. Premissas aparecem na impressão, ficam apenas na memória da tela e são limpas ao gerar uma nova consulta. Para conservar a versão emitida, salve o PDF. Sem premissas, aparece **Estimativa não calculada**, nunca um zero financeiro presumido.

## Validação

```powershell
dotnet run --project tests/RelatoriosChecks
dotnet build ADSync.Client
```

O teste de serviço/controller usa somente dados em memória. Valida limites do mês, ano bissexto, comparação de janeiro, parcial sem futuro, amostras de tempo, consumo, regras, autorização declarada e entradas inválidas. Não lê segredos nem acessa Supabase.

Interface com API simulada: inicie um cliente separado em `http://127.0.0.1:5128` com `dotnet run --project ADSync.Client --no-build --no-launch-profile --urls http://127.0.0.1:5128` e rode `node tests/relatorios-browser.cjs`. Todas as chamadas de API, inclusive o login, são interceptadas; nenhum dado é gravado. Artefatos ficam em `tests/artifacts/relatorios/`.

Não há alteração de esquema, migração, seed ou escrita de negócio. Reinicie API e cliente depois da recompilação para carregar a funcionalidade.
