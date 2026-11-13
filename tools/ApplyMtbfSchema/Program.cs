using Microsoft.Extensions.Configuration;
using Npgsql;

// Sem --apply, apenas consulta a estrutura. Nunca inicializa o modelo ou cria dados.
try
{
    var config = new ConfigurationBuilder().AddUserSecrets("c6a39c55-2a48-4e31-bf5a-9996b0d92007").Build();
    await using var db = new NpgsqlConnection(config.GetConnectionString("AdSync") ?? throw new InvalidOperationException());
    await db.OpenAsync();
    await using var tx = await db.BeginTransactionAsync();
    async Task<object?> Scalar(string sql)
    {
        await using var command = new NpgsqlCommand(sql, db, tx);
        return await command.ExecuteScalarAsync();
    }
    await using (var settings = new NpgsqlCommand("SET LOCAL search_path TO public; SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '30s'", db, tx))
        await settings.ExecuteNonQueryAsync();
    var exists = (bool)(await Scalar("SELECT to_regclass('public.equipamento_operacao_diaria') IS NOT NULL"))!;
    if (!exists)
    {
        if (!args.Contains("--apply"))
        {
            Console.WriteLine("Tabela MTBF ausente. Aplicação exige --apply após autorização; nenhuma alteração realizada.");
            return;
        }
        var sql = (await File.ReadAllTextAsync("docs/sql/MTBF.sql")).Replace("BEGIN;", "").Replace("COMMIT;", "");
        await using var command = new NpgsqlCommand(sql, db, tx);
        await command.ExecuteNonQueryAsync();
    }
    var valid = (bool)(await Scalar("""
        SELECT
          (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='equipamento_operacao_diaria') = 3
          AND (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='equipamento_operacao_diaria' AND is_nullable='NO' AND
            ((column_name='equipamento_id' AND data_type='uuid') OR (column_name='data' AND data_type='date') OR
             (column_name='horas_operadas' AND data_type='numeric' AND numeric_precision=4 AND numeric_scale=2))) = 3
          AND EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.equipamento_operacao_diaria'::regclass AND contype='p' AND pg_get_constraintdef(oid)='PRIMARY KEY (equipamento_id, data)')
          AND EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.equipamento_operacao_diaria'::regclass AND contype='f' AND confrelid='public.equipamentos'::regclass AND confdeltype='r')
          AND EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.equipamento_operacao_diaria'::regclass AND contype='c' AND conname='ck_operacao_horas')
          AND EXISTS (SELECT 1 FROM pg_class WHERE oid='public.equipamento_operacao_diaria'::regclass AND relrowsecurity)
          AND has_table_privilege(current_user,'public.equipamento_operacao_diaria','SELECT,INSERT,UPDATE')
          AND EXISTS (SELECT 1 FROM pg_class c JOIN pg_roles r ON r.rolname=current_user
            WHERE c.oid='public.equipamento_operacao_diaria'::regclass AND (r.rolsuper OR r.rolbypassrls OR c.relowner=r.oid))
        """))!;
    if (!valid) throw new InvalidOperationException();
    await tx.CommitAsync();
    Console.WriteLine(exists ? "PASS: estrutura MTBF existente validada; sem alterações." : "PASS: tabela MTBF criada e validada; sem lançamentos ou alteração de dados existentes.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Falha ao verificar/aplicar MTBF ({ex.GetType().Name}{(ex is PostgresException pg ? "; SQLSTATE=" + pg.SqlState : "")}). Conexão e credenciais omitidas; alterações não confirmadas são revertidas.");
    Environment.ExitCode = 1;
}
