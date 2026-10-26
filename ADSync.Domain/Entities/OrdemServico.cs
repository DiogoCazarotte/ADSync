using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;

namespace ADSync.Domain.Entities;

public class OrdemServico
{
    public Guid Id { get; set; }

    // Preenchido quando a O.S. foi gerada automaticamente por um plano preventivo (RF13).
    public Guid? PlanoPreventivoId { get; set; }
    public PlanoPreventivo? PlanoPreventivo { get; set; }

    // Gerado no PWA/IndexedDB enquanto offline; usado para evitar O.S. duplicada na sincronização.
    public Guid? ClientSyncId { get; set; }

    public string NumeroOS { get; set; } = string.Empty;

    public Guid EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    public Guid OperadorId { get; set; }
    public Usuario? Operador { get; set; }

    public Guid? TecnicoId { get; set; }
    public Usuario? Tecnico { get; set; }

    public Guid? CausaFalhaId { get; set; }
    public CausaFalha? CausaFalha { get; set; }

    public string DescricaoProblema { get; set; } = string.Empty;
    public StatusOS Status { get; set; } = StatusOS.Aberta;
    public PrioridadeOS Prioridade { get; set; } = PrioridadeOS.Media;

    // Corretiva = reparo após falha; Preventiva = manutenção planejada (base do indicador de % preventiva).
    public TipoManutencao TipoManutencao { get; set; } = TipoManutencao.Corretiva;

    public DateTimeOffset DataAbertura { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DataConclusao { get; set; }
    public string? ObservacoesTecnicas { get; set; }
    public bool CriadoOffline { get; set; }

    public ICollection<OsRegistroTempo> RegistrosTempo { get; set; } = new List<OsRegistroTempo>();
    public ICollection<OsPecaUtilizada> PecasUtilizadas { get; set; } = new List<OsPecaUtilizada>();

    // =====================================================================
    //  REGRAS DE NEGÓCIO
    //  Fluxo de status:
    //    Aberta -> EmAndamento <-> Pausada
    //    EmAndamento -> Concluida
    //    Aberta/Pausada -> Cancelada
    // =====================================================================

    // Gestor define (ou troca) o técnico responsável.
    public void AtribuirTecnico(Usuario tecnico)
    {
        GarantirNaoFinalizada();

        if (Status == StatusOS.EmAndamento)
            throw new RegraNegocioException(
                "A O.S. está em andamento. Pause o atendimento antes de trocar o técnico.");

        if (tecnico.TipoUsuario != TipoUsuario.Tecnico || !tecnico.Ativo)
            throw new RegraNegocioException("O usuário informado não é um técnico ativo.");

        TecnicoId = tecnico.Id;
        Tecnico = tecnico;
    }

    // Técnico inicia o atendimento. Se a O.S. ainda não tem responsável, ele assume.
    public void Iniciar(Guid tecnicoId, DateTimeOffset quando)
    {
        if (Status != StatusOS.Aberta)
            throw new RegraNegocioException(
                $"Só é possível iniciar uma O.S. com status Aberta (status atual: {Status}).");

        if (TecnicoId is null)
            TecnicoId = tecnicoId;
        else
            GarantirResponsavel(tecnicoId);

        RegistrarEvento(TipoRegistroTempo.Inicio, quando);
        Status = StatusOS.EmAndamento;
    }

    public void Pausar(Guid tecnicoId, string motivo, DateTimeOffset quando)
    {
        if (Status != StatusOS.EmAndamento)
            throw new RegraNegocioException(
                $"Só é possível pausar uma O.S. em andamento (status atual: {Status}).");

        GarantirResponsavel(tecnicoId);

        if (string.IsNullOrWhiteSpace(motivo))
            throw new RegraNegocioException("Informe o motivo da pausa.");

        RegistrarEvento(TipoRegistroTempo.Pausa, quando, motivo.Trim());
        Status = StatusOS.Pausada;
    }

    public void Retomar(Guid tecnicoId, DateTimeOffset quando)
    {
        if (Status != StatusOS.Pausada)
            throw new RegraNegocioException(
                $"Só é possível retomar uma O.S. pausada (status atual: {Status}).");

        GarantirResponsavel(tecnicoId);

        RegistrarEvento(TipoRegistroTempo.Retomada, quando);
        Status = StatusOS.EmAndamento;
    }

    // RF05: a causa da falha é obrigatória para concluir.
    public void Concluir(Guid tecnicoId, CausaFalha causa, string? observacoes, DateTimeOffset quando)
    {
        if (Status != StatusOS.EmAndamento)
            throw new RegraNegocioException(
                $"Só é possível concluir uma O.S. em andamento (status atual: {Status}). " +
                "Se estiver pausada, retome antes de concluir.");

        GarantirResponsavel(tecnicoId);

        if (!causa.Ativo)
            throw new RegraNegocioException("A causa de falha informada está inativa.");

        RegistrarEvento(TipoRegistroTempo.Conclusao, quando);

        Status = StatusOS.Concluida;
        DataConclusao = quando;
        CausaFalhaId = causa.Id;
        CausaFalha = causa;

        if (!string.IsNullOrWhiteSpace(observacoes))
            ObservacoesTecnicas = observacoes.Trim();
    }

    // Só O.S. que não estão rodando podem ser canceladas.
    public void Cancelar(string? motivo)
    {
        if (Status != StatusOS.Aberta && Status != StatusOS.Pausada)
            throw new RegraNegocioException(
                $"Só é possível cancelar uma O.S. aberta ou pausada (status atual: {Status}).");

        Status = StatusOS.Cancelada;

        if (!string.IsNullOrWhiteSpace(motivo))
        {
            var linha = $"Cancelada: {motivo.Trim()}";
            ObservacoesTecnicas = string.IsNullOrWhiteSpace(ObservacoesTecnicas)
                ? linha
                : $"{ObservacoesTecnicas}\n{linha}";
        }
    }

    // RF04: registra a peça usada e já dá baixa no estoque (na mesma operação de gravação).
    public void RegistrarPeca(Guid tecnicoId, Peca peca, int quantidade)
    {
        if (Status != StatusOS.EmAndamento && Status != StatusOS.Pausada)
            throw new RegraNegocioException(
                $"Só é possível registrar peças em uma O.S. em andamento ou pausada (status atual: {Status}).");

        GarantirResponsavel(tecnicoId);

        peca.RetirarDoEstoque(quantidade);

        var existente = PecasUtilizadas.FirstOrDefault(p => p.PecaId == peca.Id);
        if (existente is not null)
        {
            existente.Quantidade += quantidade;
        }
        else
        {
            PecasUtilizadas.Add(new OsPecaUtilizada
            {
                PecaId = peca.Id,
                Peca = peca,
                Quantidade = quantidade
            });
        }
    }

    // Soma os trechos trabalhados (Início/Retomada até Pausa/Conclusão).
    // Se ainda está em andamento, o trecho aberto conta até "agora".
    public TimeSpan CalcularTempoAtendimento(DateTimeOffset agora)
    {
        var total = TimeSpan.Zero;
        DateTimeOffset? inicioTrecho = null;

        foreach (var registro in RegistrosTempo.OrderBy(r => r.Timestamp))
        {
            switch (registro.TipoRegistro)
            {
                case TipoRegistroTempo.Inicio:
                case TipoRegistroTempo.Retomada:
                    inicioTrecho = registro.Timestamp;
                    break;

                case TipoRegistroTempo.Pausa:
                case TipoRegistroTempo.Conclusao:
                    if (inicioTrecho is not null)
                    {
                        total += registro.Timestamp - inicioTrecho.Value;
                        inicioTrecho = null;
                    }
                    break;
            }
        }

        if (inicioTrecho is not null && Status == StatusOS.EmAndamento)
            total += agora - inicioTrecho.Value;

        return total;
    }

    // ---------------------------------------------------------------------
    //  Auxiliares privados
    // ---------------------------------------------------------------------

    private void GarantirNaoFinalizada()
    {
        if (Status == StatusOS.Concluida || Status == StatusOS.Cancelada)
            throw new RegraNegocioException($"A O.S. já foi finalizada (status: {Status}).");
    }

    private void GarantirResponsavel(Guid tecnicoId)
    {
        if (TecnicoId != tecnicoId)
            throw new AcessoNegadoException("Esta O.S. não está atribuída a você.");
    }

    private void RegistrarEvento(TipoRegistroTempo tipo, DateTimeOffset quando, string? motivoPausa = null)
    {
        // Eventos precisam respeitar a ordem cronológica (importante na sincronização offline).
        if (RegistrosTempo.Count > 0)
        {
            var ultimo = RegistrosTempo.Max(r => r.Timestamp);
            if (quando < ultimo)
                throw new RegraNegocioException(
                    "A data/hora do evento é anterior ao último registro desta O.S.");
        }

        // O Id não é definido aqui de propósito: o EF Core gera ao gravar.
        RegistrosTempo.Add(new OsRegistroTempo
        {
            TipoRegistro = tipo,
            Timestamp = quando,
            MotivoPausa = motivoPausa
        });
    }
}