namespace ADSync.Application.DTOs;

public class RelatorioMensalDto
{
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public DateTimeOffset GeradoEm { get; set; }
    public bool MesEmAndamento { get; set; }
    public int OrdensAbertasNoMes { get; set; }
    public ResumoMensalDto Resumo { get; set; } = new();
    public ResumoMensalDto? MesAnterior { get; set; }
    public List<AtividadeMensalDto> Atividades { get; set; } = [];
    public List<SetorMensalDto> Setores { get; set; } = [];
    public List<ConsumoMensalDto> Pecas { get; set; } = [];
    public List<RecomendacaoMensalDto> Recomendacoes { get; set; } = [];
    public List<AgendaPreventivaDto> AgendaAtual { get; set; } = [];
    public int PendentesAtuais { get; set; }
}

public class ResumoMensalDto
{
    public int Concluidas { get; set; }
    public int Corretivas { get; set; }
    public int Preventivas { get; set; }
    public double? PercentualPreventiva { get; set; }
    public double? HorasTrabalhadas { get; set; }
    public int OrdensComTempoValido { get; set; }
    public int OrdensSemTempoValido { get; set; }
    public double? MttrMinutos { get; set; }
    public int AmostraMttr { get; set; }
}

public class AtividadeMensalDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string Equipamento { get; set; } = "";
    public string Patrimonio { get; set; } = "";
    public string Setor { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Tecnico { get; set; } = "";
    public DateTimeOffset ConcluidaEm { get; set; }
    public string Descricao { get; set; } = "";
    public string? Causa { get; set; }
    public string? Observacoes { get; set; }
    public double? MinutosTrabalhados { get; set; }
    public List<ConsumoMensalDto> Pecas { get; set; } = [];
}

public class SetorMensalDto
{
    public string Setor { get; set; } = "";
    public int Corretivas { get; set; }
    public int Preventivas { get; set; }
}

public class ConsumoMensalDto
{
    public Guid PecaId { get; set; }
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public int Quantidade { get; set; }
}

public class RecomendacaoMensalDto
{
    public string Prioridade { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Evidencia { get; set; } = "";
    public string Acao { get; set; } = "";
}

public class AgendaPreventivaDto
{
    public string Equipamento { get; set; } = "";
    public string Tarefa { get; set; } = "";
    public string Responsavel { get; set; } = "";
    public DateOnly ProximaExecucao { get; set; }
    public bool Vencida { get; set; }
}
