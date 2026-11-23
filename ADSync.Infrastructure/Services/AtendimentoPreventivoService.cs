using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Services;

public class AtendimentoPreventivoService(AdSyncDbContext db, IOrdemServicoRepository ordens, IOrdemServicoService atendimento, TimeProvider clock)
{
    public async Task<OrdemServicoDetalheDto> IniciarAsync(Guid planoId, Guid tecnicoId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Compartilha a trava do agendador: técnico e job nunca geram duas OS da mesma execução.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094996826)", ct);
        var plano = await db.PlanosPreventivos.Include(p => p.Equipamento).Include(p => p.TecnicoResponsavel)
            .SingleOrDefaultAsync(p => p.Id == planoId, ct)
            ?? throw new NaoEncontradoException("Plano preventivo não encontrado.");
        var ordem = await db.OrdensServico.Where(o => o.PlanoPreventivoId == planoId &&
                (o.Status == StatusOS.Aberta || o.Status == StatusOS.EmAndamento || o.Status == StatusOS.Pausada))
            .OrderBy(o => o.DataAbertura).FirstOrDefaultAsync(ct);
        if ((ordem is null ? plano.TecnicoResponsavelId : ordem.TecnicoId) != tecnicoId)
            throw new AcessoNegadoException("A manutenção preventiva não está atribuída a você.");

        var agora = clock.GetUtcNow();
        if (ordem is null)
        {
            if (CalendarioManutencao.Data(agora) < plano.ProximaExecucao)
                throw new RegraNegocioException($"A manutenção preventiva estará disponível a partir de {plano.ProximaExecucao:dd/MM/yyyy}.");
            ordem = plano.GerarOrdemServico(await ordens.ObterProximoNumeroAsync(), agora);
            await ordens.AdicionarAsync(ordem);
        }
        // Mesma trava dos eventos offline, para não disputar o início com uma sincronização.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({ordem.Id.ToString()}, 0))", ct);
        if (db.Entry(ordem).State != EntityState.Added) await db.Entry(ordem).ReloadAsync(ct);
        if (ordem.TecnicoId != tecnicoId)
            throw new AcessoNegadoException("A manutenção preventiva não está mais atribuída a você.");
        if (ordem.Status == StatusOS.Aberta) ordem.Iniciar(tecnicoId, agora);
        else if (ordem.Status is not (StatusOS.EmAndamento or StatusOS.Pausada))
            throw new RegraNegocioException("Esta ordem já foi finalizada. Atualize os planos.");
        await db.SaveChangesAsync(ct);
        var result = await atendimento.ObterDetalheAsync(ordem.Id, tecnicoId: tecnicoId)
            ?? throw new NaoEncontradoException("Ordem preventiva não encontrada.");
        await tx.CommitAsync(ct);
        return result;
    }
}
