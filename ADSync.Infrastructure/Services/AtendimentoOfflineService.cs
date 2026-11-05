using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Services;

public class AtendimentoOfflineService(AdSyncDbContext db, IOrdemServicoService service)
{
    public async Task<OrdemServicoDetalheDto> AplicarAsync(Guid usuarioId, EventoAtendimentoDto evento)
    {
        if (evento.Id == Guid.Empty || evento.OrdemServicoId == Guid.Empty || evento.OcorridoEm == default ||
            evento.OcorridoEm > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new RegraNegocioException("Evento sem identificador ou com data/hora inválida.");
        evento.OcorridoEm = evento.OcorridoEm.ToUniversalTime();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evento))));
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({evento.OrdemServicoId.ToString()}, 0))");
        var receipt = await db.EventosSincronizados.FindAsync(evento.Id);
        if (receipt is not null)
        {
            if (receipt.UsuarioId != usuarioId || receipt.Hash != hash)
                throw new AcessoNegadoException("Identificador de evento já utilizado por outra operação.");
            return JsonSerializer.Deserialize<OrdemServicoDetalheDto>(receipt.Resposta)!;
        }
        var detail = await service.ObterDetalheAsync(evento.OrdemServicoId, tecnicoId: usuarioId)
            ?? throw new AcessoNegadoException("A ordem não está mais atribuída a você. Pendências preservadas para revisão com o gestor.");
        var latest = await db.EventosSincronizados.Where(e => e.OrdemServicoId == evento.OrdemServicoId)
            .MaxAsync(e => (DateTimeOffset?)e.OcorridoEm);
        if (evento.OcorridoEm < detail.DataAbertura ||
            (latest.HasValue && evento.OcorridoEm < latest) || detail.Registros.Any(r => r.Timestamp > evento.OcorridoEm))
            throw new RegraNegocioException("Evento anterior ao estado atual da ordem. Revise as pendências com o gestor.");
        var result = evento.Acao switch
        {
            "iniciar" => await service.IniciarAsync(evento.OrdemServicoId, usuarioId, evento.OcorridoEm),
            "pausar" => await service.PausarAsync(evento.OrdemServicoId, usuarioId, evento.Motivo ?? "", evento.OcorridoEm),
            "retomar" => await service.RetomarAsync(evento.OrdemServicoId, usuarioId, evento.OcorridoEm),
            "pecas" when evento.PecaId.HasValue && evento.Quantidade > 0 => await service.RegistrarPecaAsync(evento.OrdemServicoId, usuarioId, evento.PecaId.Value, evento.Quantidade),
            "concluir" when evento.CausaFalhaId.HasValue => await service.ConcluirAsync(evento.OrdemServicoId, usuarioId, evento.CausaFalhaId.Value, evento.Observacoes, evento.OcorridoEm),
            _ => throw new RegraNegocioException("Ação ou dados do atendimento inválidos.")
        };
        db.EventosSincronizados.Add(new EventoSincronizado { Id = evento.Id, OrdemServicoId = evento.OrdemServicoId,
            UsuarioId = usuarioId, OcorridoEm = evento.OcorridoEm, Hash = hash, Resposta = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return result;
    }
}
