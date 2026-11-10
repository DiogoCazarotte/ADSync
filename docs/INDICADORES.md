# Painel de indicadores

A área **Indicadores**, exclusiva do Gestor, reúne cartões de métricas, comparação com o período anterior, evolução das conclusões, distribuição por tipo, tabela por setor e até cinco equipamentos com mais corretivas concluídas. Há atalhos para os últimos 7, 30 ou 90 dias, datas personalizadas e filtro por setor. O botão de atualização no cabeçalho também recarrega a consulta ativa.

Os indicadores usam a API `GET /api/indicadores?dataInicio=2026-11-01&dataFim=2026-11-10&setor=Produção`. As consultas do painel exigem conexão. Em caso de falha, o resultado anterior permanece identificado pelas suas datas e um aviso informa que a atualização falhou.

## Critérios

- **Base:** somente O.S. com status Concluída e data de conclusão no intervalo escolhido. O início é inclusivo e o limite exclusivo é 00h UTC do dia posterior à data final. Os filtros, o gráfico e a comparação usam esse mesmo critério; a data de abertura pode estar fora do período. O padrão tem exatamente 30 dias, incluindo hoje em UTC.
- **Comparação:** período imediatamente anterior com a mesma quantidade de dias e o mesmo setor. As diferenças são absolutas: quantidade de O.S., pontos percentuais de participação preventiva, minutos de MTTR e dias entre chamados. Não há metas ou cores de aprovação/reprovação presumidas.
- **Participação preventiva:** preventivas concluídas / total de conclusões × 100. Sem conclusões, retorna `null`.
- **MTTR de trabalho ativo:** média dos trechos de trabalho entre início/retomada e pausa/conclusão das corretivas. Ordens sem sequência válida de eventos ficam fora da média, mas continuam nas contagens. O painel mostra o tamanho da amostra e a quantidade excluída. Uma sequência válida com duração zero continua válida. Espera antes de iniciar e pausas não entram no indicador.
- **Intervalo entre chamados:** média dos intervalos entre aberturas consecutivas de corretivas do mesmo equipamento, restrita às ordens concluídas no período. O campo `MtbfDias` foi preservado por compatibilidade, mas a interface usa o nome descritivo. Não corresponde ao MTBF calculado com horas reais de operação nem mede disponibilidade do equipamento.
- **Evolução:** diária até 31 dias; mensal em períodos maiores, incluindo intervalos sem conclusões. Nos meses parciais, contam apenas as conclusões dentro das datas selecionadas. Os valores também estão disponíveis em tabela no próprio gráfico.
- **Setores:** filtro e agrupamento ignoram espaços nas extremidades e diferenças entre maiúsculas/minúsculas. Equipamentos sem setor aparecem como `(sem setor)`. As opções incluem os cadastros disponíveis e os setores encontrados nas duas janelas consultadas.
- **Equipamentos:** classificação por quantidade de corretivas concluídas, com desempate por nome e identificador. O MTTR de cada equipamento segue os mesmos critérios da média geral.

`null` aparece como “—”; ausência de amostra em um dos períodos aparece como “Sem base comparável”. A comparação fica indisponível se não houver datas suficientes antes de 01/01/0001. Datas invertidas e 31/12/9999 retornam HTTP 400, evitando extrapolar o calendário ao calcular o limite exclusivo.

Esta alteração não exige migração, alteração de esquema ou escrita de dados de negócio. Reinicie API e cliente após recompilar para carregar o contrato e os arquivos atualizados.

## Validação isolada

Na raiz, com o SDK .NET 10:

```powershell
dotnet run --project tests/IndicadoresChecks
dotnet build ADSync.Client --no-restore
```

O primeiro comando usa o serviço e o controller reais com repositório em memória. Verifica contagens, pausas, amostras ausentes/inválidas, duração zero válida, setores, fronteiras UTC, comparação, agregação diária/mensal e validação de datas. Não lê credenciais nem acessa banco.

Para verificar a interface, inicie um cliente separado:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project ADSync.Client --no-build --no-launch-profile --urls http://127.0.0.1:5127
```

Em outro terminal:

```powershell
node tests/indicadores-browser.cjs
```

O teste intercepta todas as chamadas `/api/`, inclusive o login, com dados fictícios. Não inicia a API nem acessa o Supabase. Usa Edge em modo headless e verifica métricas, comparação, filtros, tabela do gráfico, quatro resoluções, período vazio, erro e preservação das datas exibidas. Capturas ficam em `tests/artifacts/indicadores/` (ignoradas pelo Git). `INDICADORES_ORIGIN` permite escolher outro endereço do cliente.
