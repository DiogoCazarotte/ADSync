using Microsoft.Extensions.Configuration;
using Npgsql;

// Executa exclusivamente o script autorizado de recibos. Nunca inicializa o modelo EF.
try
{
    var config = new ConfigurationBuilder().AddUserSecrets("c6a39c55-2a48-4e31-bf5a-9996b0d92007").Build();
    var connection = config.GetConnectionString("AdSync") ?? throw new InvalidOperationException("Configuração ausente.");
    await using var db = new NpgsqlConnection(connection);
    await db.OpenAsync();
    await using var tx = await db.BeginTransactionAsync();
    async Task<object?> Scalar(string sql)
    {
        await using var command = new NpgsqlCommand(sql, db, tx);
        return await command.ExecuteScalarAsync();
    }
    await using (var settings = new NpgsqlCommand("SET LOCAL search_path TO public; SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '30s'", db, tx))
        await settings.ExecuteNonQueryAsync();
    var exists = (bool)(await Scalar("SELECT to_regclass('public.os_eventos_sincronizados') IS NOT NULL"))!;
    if (!exists)
    {
        if (!args.Contains("--apply")) throw new InvalidOperationException("Tabela ausente; use --apply após autorização.");
        var script = await File.ReadAllTextAsync("docs/sql/ATENDIMENTO-OFFLINE.sql");
        script = script.Replace("BEGIN;", "").Replace("COMMIT;", "");
        await using var command = new NpgsqlCommand(script, db, tx);
        await command.ExecuteNonQueryAsync();
    }
    var valid = (bool)(await Scalar("""
        SELECT
          (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='os_eventos_sincronizados') = 6
          AND (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='os_eventos_sincronizados' AND is_nullable='NO' AND
              ((column_name IN ('id','ordem_servico_id','usuario_id') AND data_type='uuid') OR
               (column_name='ocorrido_em' AND data_type='timestamp with time zone') OR
               (column_name='hash' AND data_type='character varying' AND character_maximum_length=64) OR
               (column_name='resposta' AND data_type='text'))) = 6
          AND EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.os_eventos_sincronizados'::regclass AND contype='p' AND pg_get_constraintdef(oid)='PRIMARY KEY (id)')
          AND (SELECT count(*) FROM pg_constraint WHERE conrelid='public.os_eventos_sincronizados'::regclass AND contype='f' AND confdeltype='r') = 2
          AND EXISTS (SELECT 1 FROM pg_class WHERE oid='public.os_eventos_sincronizados'::regclass AND relrowsecurity)
          AND has_table_privilege(current_user,'public.os_eventos_sincronizados','SELECT,INSERT')
          AND EXISTS (SELECT 1 FROM pg_class c JOIN pg_roles r ON r.rolname=current_user
                      WHERE c.oid='public.os_eventos_sincronizados'::regclass AND (r.rolsuper OR r.rolbypassrls OR c.relowner=r.oid))
        """))!;
    if (!valid) throw new InvalidOperationException("Estrutura ou permissões incompatíveis; transação revertida.");
    await tx.CommitAsync();
    Console.WriteLine(exists ? "PASS: tabela existente validada; nenhuma alteração adicional." : "PASS: tabela de recibos criada e validada no banco configurado nos User Secrets.");
    Console.WriteLine("PASS: seis colunas, chave primária, vínculos, RLS e acesso da conexão da API verificados. Dados de negócio não foram alterados.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Falha ao aplicar/verificar recibos ({ex.GetType().Name}{(ex is PostgresException pg ? "; SQLSTATE=" + pg.SqlState : "")}). Conexão e credenciais omitidas.");
    Environment.ExitCode = 1;
}
