using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Repositories;

public class RelatorioMensalRepository(AdSyncDbContext context) : IRelatorioMensalRepository
{
    public Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo) =>
        context.OrdensServico.AsNoTracking().AsSplitQuery()
            .Include(o => o.Equipamento).Include(o => o.Tecnico).Include(o => o.CausaFalha)
            .Include(o => o.RegistrosTempo).Include(o => o.PecasUtilizadas).ThenInclude(p => p.Peca)
            .Where(o => o.Status == StatusOS.Concluida && o.DataConclusao >= inicio && o.DataConclusao < fimExclusivo)
            .ToListAsync();

    public Task<int> ContarAberturasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo) =>
        context.OrdensServico.AsNoTracking().CountAsync(o => o.DataAbertura >= inicio && o.DataAbertura < fimExclusivo);

    public Task<List<OrdemServico>> ListarPendentesAsync(DateTimeOffset agora) =>
        context.OrdensServico.AsNoTracking().Include(o => o.Equipamento)
            .Where(o => o.DataAbertura <= agora && (o.Status == StatusOS.Aberta || o.Status == StatusOS.EmAndamento || o.Status == StatusOS.Pausada))
            .ToListAsync();

    public Task<List<PlanoPreventivo>> ListarPlanosAtivosAsync() =>
        context.PlanosPreventivos.AsNoTracking().Include(p => p.Equipamento).Include(p => p.TecnicoResponsavel)
            .Where(p => p.Ativo && p.Equipamento != null && p.Equipamento.Ativo).ToListAsync();

    public Task<List<Peca>> ListarPecasCriticasSemEstoqueAsync() =>
        context.Pecas.AsNoTracking().Where(p => p.Critica && p.QuantidadeEstoque <= 0).OrderBy(p => p.Nome).ToListAsync();
}
