using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

public class OrdemServicoService : IOrdemServicoService
{
    private readonly IOrdemServicoRepository _repository;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPecaRepository _pecas;
    private readonly ICausaFalhaRepository _causas;
    private readonly IEquipamentoRepository _equipamentos;

    public OrdemServicoService(
        IOrdemServicoRepository repository,
        IUsuarioRepository usuarios,
        IPecaRepository pecas,
        ICausaFalhaRepository causas,
        IEquipamentoRepository equipamentos)
    {
        _repository = repository;
        _usuarios = usuarios;
        _pecas = pecas;
        _causas = causas;
        _equipamentos = equipamentos;
    }

    // =====================================================================
    //  Abrir / consultar / listar
    // =====================================================================

    public async Task<OrdemServicoDto> AbrirAsync(CriarOrdemServicoDto dto)
    {
        // Idempotencia: se essa O.S. ja foi sincronizada antes (ex: a internet caiu
        // no meio do envio e o app tentou de novo), devolve a que ja existe
        // em vez de criar uma duplicada.
        if (dto.ClientSyncId is not null)
        {
            var existente = await _repository.ObterPorClientSyncIdAsync(dto.ClientSyncId.Value);
            if (existente is not null)
                return MapearResumo(existente);
        }

        var equipamento = await _equipamentos.ObterPorIdAsync(dto.EquipamentoId)
            ?? throw new NaoEncontradoException("Equipamento não encontrado.");

        if (!equipamento.Ativo)
            throw new RegraNegocioException("O equipamento informado está inativo.");

        if (!Enum.TryParse<PrioridadeOS>(dto.Prioridade, ignoreCase: true, out var prioridade))
            prioridade = PrioridadeOS.Media;

        var ordemServico = new OrdemServico
        {
            Id = Guid.NewGuid(),
            ClientSyncId = dto.ClientSyncId,
            NumeroOS = GerarNumeroOS(),
            EquipamentoId = dto.EquipamentoId,
            OperadorId = dto.OperadorId,
            DescricaoProblema = dto.DescricaoProblema.Trim(),
            Status = StatusOS.Aberta,
            Prioridade = prioridade,
            DataAbertura = ValidarQuando(dto.DataAbertura),
            CriadoOffline = dto.CriadoOffline
        };

        await _repository.AdicionarAsync(ordemServico);
        await _repository.SalvarAlteracoesAsync();

        // Recarrega com os relacionamentos (Equipamento, Operador, Tecnico) para montar a resposta
        var criada = await _repository.ObterPorIdAsync(ordemServico.Id);
        return MapearResumo(criada!);
    }

    public async Task<OrdemServicoDetalheDto?> ObterDetalheAsync(Guid id)
    {
        var os = await _repository.ObterDetalhadoAsync(id);
        return os is null ? null : MapearDetalhe(os);
    }

    public async Task<List<OrdemServicoDto>> ListarAsync(
        StatusOS? status, PrioridadeOS? prioridade, Guid? tecnicoId, Guid? operadorId)
    {
        var lista = await _repository.ListarAsync(status, prioridade, tecnicoId, operadorId);
        return lista.Select(MapearResumo).ToList();
    }

    // =====================================================================
    //  Ações do fluxo (as regras em si ficam na entidade OrdemServico)
    // =====================================================================

    public async Task<OrdemServicoDetalheDto> AtribuirTecnicoAsync(Guid id, Guid tecnicoId)
    {
        var os = await ObterOuFalharAsync(id);

        var tecnico = await _usuarios.ObterPorIdAsync(tecnicoId)
            ?? throw new NaoEncontradoException("Técnico não encontrado.");

        os.AtribuirTecnico(tecnico);
        return await SalvarEMapearAsync(os);
    }

    public async Task<OrdemServicoDetalheDto> IniciarAsync(Guid id, Guid usuarioId, DateTimeOffset? ocorridoEm)
    {
        var os = await ObterOuFalharAsync(id);
        os.Iniciar(usuarioId, ValidarQuando(ocorridoEm));
        return await SalvarEMapearAsync(os);
    }

    public async Task<OrdemServicoDetalheDto> PausarAsync(Guid id, Guid usuarioId, string motivo, DateTimeOffset? ocorridoEm)
    {
        var os = await ObterOuFalharAsync(id);
        os.Pausar(usuarioId, motivo, ValidarQuando(ocorridoEm));
        return await SalvarEMapearAsync(os);
    }

    public async Task<OrdemServicoDetalheDto> RetomarAsync(Guid id, Guid usuarioId, DateTimeOffset? ocorridoEm)
    {
        var os = await ObterOuFalharAsync(id);
        os.Retomar(usuarioId, ValidarQuando(ocorridoEm));
        return await SalvarEMapearAsync(os);
    }

    public async Task<OrdemServicoDetalheDto> ConcluirAsync(
        Guid id, Guid usuarioId, Guid causaFalhaId, string? observacoes, DateTimeOffset? ocorridoEm)
    {
        var os = await ObterOuFalharAsync(id);

        var causa = await _causas.ObterPorIdAsync(causaFalhaId)
            ?? throw new NaoEncontradoException("Causa de falha não encontrada.");

        os.Concluir(usuarioId, causa, observacoes, ValidarQuando(ocorridoEm));
        return await SalvarEMapearAsync(os);
    }

    public async Task<OrdemServicoDetalheDto> CancelarAsync(Guid id, string? motivo)
    {
        var os = await ObterOuFalharAsync(id);
        os.Cancelar(motivo);
        return await SalvarEMapearAsync(os);
    }

    public async Task<OrdemServicoDetalheDto> RegistrarPecaAsync(Guid id, Guid usuarioId, Guid pecaId, int quantidade)
    {
        var os = await ObterOuFalharAsync(id);

        var peca = await _pecas.ObterPorIdAsync(pecaId)
            ?? throw new NaoEncontradoException("Peça não encontrada.");

        // A entidade valida o estoque e dá a baixa. Como a Peca e a O.S. estão rastreadas
        // pelo mesmo contexto, o SaveChanges abaixo grava os dois na MESMA transação.
        os.RegistrarPeca(usuarioId, peca, quantidade);
        return await SalvarEMapearAsync(os);
    }

    // =====================================================================
    //  Auxiliares
    // =====================================================================

    private async Task<OrdemServico> ObterOuFalharAsync(Guid id)
    {
        return await _repository.ObterDetalhadoAsync(id)
            ?? throw new NaoEncontradoException("Ordem de serviço não encontrada.");
    }

    private async Task<OrdemServicoDetalheDto> SalvarEMapearAsync(OrdemServico os)
    {
        await _repository.SalvarAlteracoesAsync();

        // Recarrega para trazer nomes de técnico/causa/peças atualizados
        var atualizada = await _repository.ObterDetalhadoAsync(os.Id);
        return MapearDetalhe(atualizada!);
    }

    // Valida a data/hora informada pelo app (eventos feitos offline).
    // Não pode estar no futuro (com 5 min de tolerância para diferença de relógio).
    // Sempre devolve em UTC (o PostgreSQL exige offset zero em timestamptz).
    private static DateTimeOffset ValidarQuando(DateTimeOffset? ocorridoEm)
    {
        var agora = DateTimeOffset.UtcNow;

        if (ocorridoEm is null)
            return agora;

        if (ocorridoEm.Value > agora.AddMinutes(5))
            throw new RegraNegocioException("A data/hora informada está no futuro.");

        return ocorridoEm.Value.ToUniversalTime();
    }

    private static string GerarNumeroOS()
    {
        // Simples e suficiente para o MVP: OS-20261014-4821
        return $"OS-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }

    private static OrdemServicoDto MapearResumo(OrdemServico os)
    {
        return new OrdemServicoDto
        {
            Id = os.Id,
            NumeroOS = os.NumeroOS,
            Status = os.Status.ToString(),
            Prioridade = os.Prioridade.ToString(),
            EquipamentoNome = os.Equipamento?.Nome ?? string.Empty,
            OperadorNome = os.Operador?.Nome ?? string.Empty,
            TecnicoNome = os.Tecnico?.Nome,
            DescricaoProblema = os.DescricaoProblema,
            DataAbertura = os.DataAbertura,
            DataConclusao = os.DataConclusao
        };
    }

    private static OrdemServicoDetalheDto MapearDetalhe(OrdemServico os)
    {
        return new OrdemServicoDetalheDto
        {
            Id = os.Id,
            NumeroOS = os.NumeroOS,
            Status = os.Status.ToString(),
            Prioridade = os.Prioridade.ToString(),
            EquipamentoId = os.EquipamentoId,
            EquipamentoNome = os.Equipamento?.Nome ?? string.Empty,
            Setor = os.Equipamento?.Setor ?? string.Empty,
            OperadorNome = os.Operador?.Nome ?? string.Empty,
            TecnicoId = os.TecnicoId,
            TecnicoNome = os.Tecnico?.Nome,
            DescricaoProblema = os.DescricaoProblema,
            CausaFalha = os.CausaFalha?.Descricao,
            ObservacoesTecnicas = os.ObservacoesTecnicas,
            DataAbertura = os.DataAbertura,
            DataConclusao = os.DataConclusao,
            CriadoOffline = os.CriadoOffline,
            TempoAtendimentoMinutos = Math.Round(os.CalcularTempoAtendimento(DateTimeOffset.UtcNow).TotalMinutes, 1),
            Registros = os.RegistrosTempo
                .OrderBy(r => r.Timestamp)
                .Select(r => new RegistroTempoDto
                {
                    Tipo = r.TipoRegistro.ToString(),
                    Timestamp = r.Timestamp,
                    MotivoPausa = r.MotivoPausa
                }).ToList(),
            Pecas = os.PecasUtilizadas
                .Select(p => new PecaUtilizadaDto
                {
                    PecaId = p.PecaId,
                    Codigo = p.Peca?.Codigo ?? string.Empty,
                    Nome = p.Peca?.Nome ?? string.Empty,
                    Quantidade = p.Quantidade
                }).ToList()
        };
    }
}