using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

public class OrdemServicoService : IOrdemServicoService
{
    private readonly IOrdemServicoRepository _repository;

    public OrdemServicoService(IOrdemServicoRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrdemServicoDto> AbrirAsync(CriarOrdemServicoDto dto)
    {
        // Idempotencia: se essa O.S. ja foi sincronizada antes (ex: a internet caiu
        // no meio do envio e o app tentou de novo), devolve a que ja existe
        // em vez de criar uma duplicada.
        if (dto.ClientSyncId is not null)
        {
            var existente = await _repository.ObterPorClientSyncIdAsync(dto.ClientSyncId.Value);
            if (existente is not null)
                return MapearParaDto(existente);
        }

        if (!Enum.TryParse<PrioridadeOS>(dto.Prioridade, ignoreCase: true, out var prioridade))
            prioridade = PrioridadeOS.Media;

        var ordemServico = new OrdemServico
        {
            Id = Guid.NewGuid(),
            ClientSyncId = dto.ClientSyncId,
            NumeroOS = GerarNumeroOS(),
            EquipamentoId = dto.EquipamentoId,
            OperadorId = dto.OperadorId,
            DescricaoProblema = dto.DescricaoProblema,
            Status = StatusOS.Aberta,
            Prioridade = prioridade,
            DataAbertura = DateTimeOffset.UtcNow,
            CriadoOffline = dto.CriadoOffline
        };

        await _repository.AdicionarAsync(ordemServico);
        await _repository.SalvarAlteracoesAsync();

        // Recarrega com os relacionamentos (Equipamento, Tecnico) para montar o DTO de resposta
        var criada = await _repository.ObterPorIdAsync(ordemServico.Id);
        return MapearParaDto(criada!);
    }

    public async Task<OrdemServicoDto?> ObterPorIdAsync(Guid id)
    {
        var ordemServico = await _repository.ObterPorIdAsync(id);
        return ordemServico is null ? null : MapearParaDto(ordemServico);
    }

    public async Task<List<OrdemServicoDto>> ListarAsync(StatusOS? status, PrioridadeOS? prioridade)
    {
        var lista = await _repository.ListarAsync(status, prioridade);
        return lista.Select(MapearParaDto).ToList();
    }

    private static string GerarNumeroOS()
    {
        // Simples e suficiente para o MVP: OS-20260921-4821
        // (dá para trocar depois por uma sequência real vinda do banco, se precisar)
        return $"OS-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    private static OrdemServicoDto MapearParaDto(OrdemServico os)
    {
        return new OrdemServicoDto
        {
            Id = os.Id,
            NumeroOS = os.NumeroOS,
            Status = os.Status.ToString(),
            Prioridade = os.Prioridade.ToString(),
            EquipamentoNome = os.Equipamento?.Nome ?? string.Empty,
            TecnicoNome = os.Tecnico?.Nome,
            DescricaoProblema = os.DescricaoProblema,
            DataAbertura = os.DataAbertura,
            DataConclusao = os.DataConclusao
        };
    }
}
