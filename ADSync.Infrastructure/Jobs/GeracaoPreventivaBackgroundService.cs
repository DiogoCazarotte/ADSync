using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ADSync.Infrastructure.Jobs;

// RF13: a cada intervalo, confere os planos preventivos cuja data chegou e abre a O.S.
// sozinho, sem depender de alguém lembrar. É o motor de "sair do corretivo para o preventivo".
//
// Roda de hora em hora de propósito: como cada plano só gera uma O.S. nova depois que a
// anterior dele é concluída ou cancelada (ver ListarPendentesAsync), não há risco de duplicar
// mesmo rodando várias vezes por dia.
public class GeracaoPreventivaBackgroundService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GeracaoPreventivaBackgroundService> _logger;

    public GeracaoPreventivaBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<GeracaoPreventivaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequena espera para a API terminar de subir antes da primeira rodada.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GerarPendentesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao gerar O.S. preventivas automáticas.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<int> GerarPendentesAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdSyncDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serializa seleção e gravação entre instâncias da API no mesmo banco.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094996826)", ct);
        var repoPlanos = scope.ServiceProvider.GetRequiredService<IPlanoPreventivoRepository>();
        var repoOrdens = scope.ServiceProvider.GetRequiredService<IOrdemServicoRepository>();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var pendentes = await repoPlanos.ListarPendentesAsync(hoje);

        if (pendentes.Count == 0)
            return 0;

        foreach (var plano in pendentes)
        {
            ct.ThrowIfCancellationRequested();
            var numeroOS = await repoOrdens.ObterProximoNumeroAsync();
            var ordemServico = plano.GerarOrdemServico(numeroOS, DateTimeOffset.UtcNow);
            await repoOrdens.AdicionarAsync(ordemServico);
        }

        // Um único SaveChanges grava as novas O.S. e o novo ProximaExecucao de cada plano,
        // pois repoPlanos e repoOrdens compartilham o mesmo DbContext (escopo).
        await repoPlanos.SalvarAlteracoesAsync();
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "{Quantidade} O.S. preventiva(s) gerada(s) automaticamente em {Quando}.",
            pendentes.Count, DateTimeOffset.UtcNow);
        return pendentes.Count;
    }
}
