using System.Text.Json;
using ADSync.Application.DTOs;
using ADSync.Application.Services;
using ADSync.Client.Services;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;

var a = new Equipamento { Id = Guid.NewGuid(), Nome = "Motor", CodigoPatrimonio = "A" };
var b = new Equipamento { Id = Guid.NewGuid(), Nome = "Motor", CodigoPatrimonio = "B" };
var outro = Guid.NewGuid();
var tecnico = Guid.NewGuid();
var corretiva = new OrdemServico { Id = Guid.NewGuid(), EquipamentoId = a.Id, Equipamento = a, TecnicoId = tecnico, NumeroOS = "OS-1" };
var preventiva = new OrdemServico { Id = Guid.NewGuid(), EquipamentoId = a.Id, Equipamento = a, TecnicoId = outro, NumeroOS = "OS-2", TipoManutencao = TipoManutencao.Preventiva, PlanoPreventivoId = Guid.NewGuid() };
var ordemB = new OrdemServico { Id = Guid.NewGuid(), EquipamentoId = b.Id, Equipamento = b, TecnicoId = tecnico, NumeroOS = "OS-3" };
var repo = new RepositorioMemoria([corretiva, preventiva, ordemB]);
// Apenas a listagem é exercitada; ela não utiliza os demais repositórios.
var service = new OrdemServicoService(repo, null!, null!, null!, null!);
var resumos = await service.ListarAsync(null, null, null, null);
var history = EquipmentHistory.Index(resumos);

Check("API: IDs corretos nas ordens corretivas e preventivas", resumos[0].EquipamentoId == a.Id && resumos[1].EquipamentoId == a.Id && resumos[1].GeradaAutomaticamente && resumos[2].EquipamentoId == b.Id);
Check("Equipamentos com o mesmo nome não misturam históricos", history[a.Id].Select(o => o.Id).SequenceEqual(new[] { corretiva.Id, preventiva.Id }) && history[b.Id].Single().Id == ordemB.Id);
Check("Contagem por equipamento", history[a.Id].Count() == 2 && history[b.Id].Count() == 1);
Check("Equipamento sem ordens", !history[Guid.NewGuid()].Any());

a.Nome = "Motor renomeado";
Check("Renomear não perde ordens, mesmo com o nome anterior no cache", EquipmentHistory.Index(resumos)[a.Id].Count() == 2);
var novosResumos = await service.ListarAsync(null, null, null, null);
Check("API retorna nome atualizado e preserva ID", novosResumos[0].EquipamentoNome == a.Nome && novosResumos[0].EquipamentoId == a.Id);

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var roundTrip = JsonSerializer.Deserialize<List<OrdemServicoDto>>(JsonSerializer.Serialize(resumos, json), json)!;
Check("ID persiste na serialização da API/cache", EquipmentHistory.Index(roundTrip)[a.Id].Count() == 2);
var legado = JsonSerializer.Deserialize<OrdemServicoDto>("{\"equipamentoNome\":\"Motor\",\"numeroOS\":\"OS-antiga\"}", json)!;
var comLegado = EquipmentHistory.Index(roundTrip.Append(legado));
Check("Cache antigo sem ID não contamina o histórico", legado.EquipamentoId == Guid.Empty && !comLegado[Guid.Empty].Any() && comLegado[b.Id].Count() == 1);

var limitados = await service.ListarAsync(null, null, tecnico, null);
Check("Histórico respeita a lista limitada pelo perfil", EquipmentHistory.Index(limitados)[a.Id].Single().Id == corretiva.Id && EquipmentHistory.Index(limitados)[b.Id].Single().Id == ordemB.Id);
Console.WriteLine("Todos os 9 testes passaram. Nenhum banco de dados foi acessado.");

static void Check(string nome, bool resultado)
{
    if (!resultado) throw new InvalidOperationException($"FALHOU: {nome}");
    Console.WriteLine($"OK: {nome}");
}

sealed class RepositorioMemoria(List<OrdemServico> ordens) : IOrdemServicoRepository
{
    public Task<List<OrdemServico>> ListarAsync(StatusOS? status = null, PrioridadeOS? prioridade = null, Guid? tecnicoId = null, Guid? operadorId = null) =>
        Task.FromResult(ordens.Where(o => (!status.HasValue || o.Status == status) && (!prioridade.HasValue || o.Prioridade == prioridade) && (!tecnicoId.HasValue || o.TecnicoId == tecnicoId) && (!operadorId.HasValue || o.OperadorId == operadorId)).ToList());
    public Task<OrdemServico?> ObterPorIdAsync(Guid id) => throw new NotSupportedException();
    public Task<OrdemServico?> ObterDetalhadoAsync(Guid id) => throw new NotSupportedException();
    public Task<OrdemServico?> ObterPorClientSyncIdAsync(Guid clientSyncId) => throw new NotSupportedException();
    public Task<string> ObterProximoNumeroAsync() => throw new NotSupportedException();
    public Task AdicionarAsync(OrdemServico ordemServico) => throw new NotSupportedException();
    public Task<OrdemServico> AdicionarComAtribuicaoAsync(OrdemServico ordemServico) => throw new NotSupportedException();
    public Task<bool> SalvarAlteracoesAsync() => throw new NotSupportedException();
}
