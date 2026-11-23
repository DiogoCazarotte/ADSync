namespace ADSync.Domain;

public static class CalendarioManutencao
{
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static DateOnly Data(DateTimeOffset instante) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, Fuso).DateTime);
}
