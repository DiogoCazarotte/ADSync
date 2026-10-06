namespace ADSync.Domain.Enums;

// Os nomes dos membros abaixo precisam ser EXATAMENTE iguais aos valores
// cadastrados nos enums do Postgres (tipo_usuario_enum, status_os_enum, etc.),
// porque o mapeamento no DbContext usa NpgsqlNullNameTranslator.

public enum TipoUsuario
{
    Operador,
    Tecnico,
    Gestor
}

public enum StatusOS
{
    Aberta,
    EmAndamento,
    Pausada,
    Concluida,
    Cancelada
}

public enum PrioridadeOS
{
    Baixa,
    Media,
    Alta,
    Critica
}

public enum TipoRegistroTempo
{
    Inicio,
    Pausa,
    Retomada,
    Conclusao
}
