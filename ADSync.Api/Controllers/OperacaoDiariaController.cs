using ADSync.Application.DTOs;
using ADSync.Domain.Entities;
using ADSync.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/operacao-diaria")]
[Authorize(Roles = "Gestor")]
public class OperacaoDiariaController(AdSyncDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OperacaoDiariaDto>>> Listar(DateOnly dataInicio, DateOnly dataFim)
    {
        if (!configuration.GetValue<bool>("Mtbf:Enabled"))
            return Conflict(new { mensagem = "O registro de operação ainda não foi habilitado neste ambiente." });
        if (dataInicio > dataFim || dataFim.DayNumber - dataInicio.DayNumber > 365)
            return BadRequest(new { mensagem = "Consulte um período de até 366 dias, com datas em ordem." });
        return Ok(await db.OperacoesDiarias.AsNoTracking().Where(o => o.Data >= dataInicio && o.Data <= dataFim)
            .OrderByDescending(o => o.Data).ThenBy(o => o.Equipamento.Nome)
            .Select(o => new OperacaoDiariaDto { EquipamentoId = o.EquipamentoId, EquipamentoNome = o.Equipamento.Nome,
                Data = o.Data, HorasOperadas = o.HorasOperadas }).ToListAsync());
    }

    // PUT substitui o total do dia. Repetir o envio não acumula horas.
    [HttpPut("{equipamentoId:guid}/{data}")]
    public async Task<ActionResult<OperacaoDiariaDto>> Salvar(Guid equipamentoId, DateOnly data, SalvarOperacaoDiariaDto dto)
    {
        if (configuration.GetValue<bool>("Safety:ReadOnly"))
            return StatusCode(403, new { mensagem = "Ambiente somente leitura." });
        if (!configuration.GetValue<bool>("Mtbf:Enabled"))
            return Conflict(new { mensagem = "O registro de operação ainda não foi habilitado neste ambiente." });
        if (data > DateOnly.FromDateTime(DateTime.UtcNow) || data == DateOnly.MinValue
            || dto.HorasOperadas < 0 || dto.HorasOperadas > 24 || decimal.Round(dto.HorasOperadas, 2) != dto.HorasOperadas)
            return BadRequest(new { mensagem = "Informe uma data até hoje (UTC) e de 0 a 24 horas, com até duas casas decimais." });
        var equipamento = await db.Equipamentos.AsNoTracking().SingleOrDefaultAsync(e => e.Id == equipamentoId);
        if (equipamento is null) return NotFound(new { mensagem = "Equipamento não encontrado." });
        // Upsert atômico, protegido pela chave equipamento/data inclusive entre dispositivos.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO equipamento_operacao_diaria (equipamento_id, data, horas_operadas) VALUES ({equipamentoId}, {data}, {dto.HorasOperadas}) ON CONFLICT (equipamento_id, data) DO UPDATE SET horas_operadas = EXCLUDED.horas_operadas");
        return Ok(new OperacaoDiariaDto { EquipamentoId = equipamentoId, EquipamentoNome = equipamento.Nome,
            Data = data, HorasOperadas = dto.HorasOperadas });
    }
}
