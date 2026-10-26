using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;

namespace ADSync.Domain.Entities;

// RF13: plano de manutenção preventiva por equipamento. É o que faz o sistema
// gerar a O.S. sozinho quando a data chega, em vez de depender de alguém lembrar.
// Periodicidade por TEMPO (dias), que é o que a SustenNutri usa hoje.
public class PlanoPreventivo
{
    public Guid Id { get; set; }

    public Guid EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    // Responsável fixo: quem a O.S. preventiva será atribuída automaticamente.
    public Guid TecnicoResponsavelId { get; set; }
    public Usuario? TecnicoResponsavel { get; set; }

    // A tarefa em si, ex.: "Lubrificar rolamentos", "Trocar correia".
    public string Descricao { get; set; } = string.Empty;

    public int PeriodicidadeDias { get; set; }

    // Próxima data em que uma nova O.S. preventiva deve ser aberta.
    public DateOnly ProximaExecucao { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<OrdemServico> OrdensGeradas { get; set; } = new List<OrdemServico>();

    public void AtualizarDados(string descricao, int periodicidadeDias, Equipamento equipamento, Usuario tecnicoResponsavel)
    {
        ValidarResponsavel(tecnicoResponsavel);
        ValidarPeriodicidade(periodicidadeDias);

        if (!equipamento.Ativo)
            throw new RegraNegocioException("O equipamento informado está inativo.");

        Descricao = descricao.Trim();
        PeriodicidadeDias = periodicidadeDias;
        EquipamentoId = equipamento.Id;
        Equipamento = equipamento;
        TecnicoResponsavelId = tecnicoResponsavel.Id;
        TecnicoResponsavel = tecnicoResponsavel;
    }

    public void Inativar() => Ativo = false;
    public void Ativar() => Ativo = true;

    // Chamado pelo job de geração automática quando ProximaExecucao chega e não há
    // O.S. em aberto deste plano (ver IPlanoPreventivoRepository.ListarPendentesAsync).
    public OrdemServico GerarOrdemServico(string numeroOS, DateTimeOffset agora)
    {
        if (!Ativo)
            throw new RegraNegocioException("O plano preventivo está inativo.");

        var ordemServico = new OrdemServico
        {
            PlanoPreventivoId = Id,
            PlanoPreventivo = this,
            EquipamentoId = EquipamentoId,
            Equipamento = Equipamento,
            // A preventiva é gerada pelo sistema e já nasce atribuída ao técnico do plano;
            // ele aparece como operador e técnico ao mesmo tempo, pois ninguém "abriu" o chamado.
            OperadorId = TecnicoResponsavelId,
            TecnicoId = TecnicoResponsavelId,
            Tecnico = TecnicoResponsavel,
            NumeroOS = numeroOS,
            DescricaoProblema = $"Manutenção preventiva: {Descricao}",
            Status = StatusOS.Aberta,
            Prioridade = PrioridadeOS.Media,
            TipoManutencao = TipoManutencao.Preventiva,
            DataAbertura = agora,
            CriadoOffline = false
        };

        ProximaExecucao = ProximaExecucao.AddDays(PeriodicidadeDias);
        OrdensGeradas.Add(ordemServico);

        return ordemServico;
    }

    private static void ValidarResponsavel(Usuario tecnico)
    {
        if (tecnico.TipoUsuario != TipoUsuario.Tecnico || !tecnico.Ativo)
            throw new RegraNegocioException("O responsável pelo plano deve ser um técnico ativo.");
    }

    private static void ValidarPeriodicidade(int periodicidadeDias)
    {
        if (periodicidadeDias <= 0)
            throw new RegraNegocioException("A periodicidade deve ser maior que zero dias.");
    }
}