using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Exceptions;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

// RF13: cadastro dos planos preventivos. A geração automática da O.S. em si
// fica no job de segundo plano (GeracaoPreventivaBackgroundService), que usa
// o mesmo repositório para achar os planos pendentes.
public class PlanoPreventivoService : IPlanoPreventivoService
{
    private readonly IPlanoPreventivoRepository _repo;
    private readonly IEquipamentoRepository _equipamentos;
    private readonly IUsuarioRepository _usuarios;

    public PlanoPreventivoService(
        IPlanoPreventivoRepository repo, IEquipamentoRepository equipamentos, IUsuarioRepository usuarios)
    {
        _repo = repo;
        _equipamentos = equipamentos;
        _usuarios = usuarios;
    }

    public async Task<List<PlanoPreventivoDto>> ListarAsync(bool incluirInativos, Guid? equipamentoId)
    {
        var lista = await _repo.ListarAsync(incluirInativos, equipamentoId);
        return lista.Select(Mapear).ToList();
    }

    public async Task<PlanoPreventivoDto?> ObterAsync(Guid id)
    {
        var p = await _repo.ObterPorIdAsync(id);
        return p is null ? null : Mapear(p);
    }

    public async Task<PlanoPreventivoDto> CriarAsync(SalvarPlanoPreventivoDto dto)
    {
        var equipamento = await ObterEquipamentoOuFalharAsync(dto.EquipamentoId);
        var tecnico = await ObterTecnicoOuFalharAsync(dto.TecnicoResponsavelId);

        var plano = new PlanoPreventivo { ProximaExecucao = dto.ProximaExecucao };
        plano.AtualizarDados(dto.Descricao, dto.PeriodicidadeDias, equipamento, tecnico);

        await _repo.AdicionarAsync(plano);
        await _repo.SalvarAlteracoesAsync();
        return Mapear(plano);
    }

    public async Task<PlanoPreventivoDto> AtualizarAsync(Guid id, SalvarPlanoPreventivoDto dto)
    {
        var plano = await ObterOuFalharAsync(id);
        var equipamento = await ObterEquipamentoOuFalharAsync(dto.EquipamentoId);
        var tecnico = await ObterTecnicoOuFalharAsync(dto.TecnicoResponsavelId);

        plano.AtualizarDados(dto.Descricao, dto.PeriodicidadeDias, equipamento, tecnico);
        plano.ProximaExecucao = dto.ProximaExecucao;

        await _repo.SalvarAlteracoesAsync();
        return Mapear(plano);
    }

    public async Task<PlanoPreventivoDto> AlterarSituacaoAsync(Guid id, bool ativo)
    {
        var plano = await ObterOuFalharAsync(id);

        if (plano.Ativo != ativo)
        {
            if (ativo) plano.Ativar(); else plano.Inativar();
            await _repo.SalvarAlteracoesAsync();
        }

        return Mapear(plano);
    }

    private async Task<PlanoPreventivo> ObterOuFalharAsync(Guid id) =>
        await _repo.ObterPorIdAsync(id) ?? throw new NaoEncontradoException("Plano preventivo não encontrado.");

    private async Task<Equipamento> ObterEquipamentoOuFalharAsync(Guid id) =>
        await _equipamentos.ObterPorIdAsync(id) ?? throw new NaoEncontradoException("Equipamento não encontrado.");

    private async Task<Usuario> ObterTecnicoOuFalharAsync(Guid id) =>
        await _usuarios.ObterPorIdAsync(id) ?? throw new NaoEncontradoException("Técnico não encontrado.");

    private static PlanoPreventivoDto Mapear(PlanoPreventivo p) => new()
    {
        Id = p.Id,
        EquipamentoId = p.EquipamentoId,
        EquipamentoNome = p.Equipamento?.Nome ?? string.Empty,
        TecnicoResponsavelId = p.TecnicoResponsavelId,
        TecnicoResponsavelNome = p.TecnicoResponsavel?.Nome ?? string.Empty,
        Descricao = p.Descricao,
        PeriodicidadeDias = p.PeriodicidadeDias,
        ProximaExecucao = p.ProximaExecucao,
        Ativo = p.Ativo
    };
}