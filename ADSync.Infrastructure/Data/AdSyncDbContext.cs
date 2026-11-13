using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Npgsql;

namespace ADSync.Infrastructure.Data;

public class AdSyncDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AdSyncDbContext(DbContextOptions<AdSyncDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<OperacaoDiaria> OperacoesDiarias => Set<OperacaoDiaria>();
    public DbSet<CausaFalha> CausasFalha => Set<CausaFalha>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();
    public DbSet<OsRegistroTempo> OsRegistrosTempo => Set<OsRegistroTempo>();
    public DbSet<OsPecaUtilizada> OsPecasUtilizadas => Set<OsPecaUtilizada>();
    public DbSet<PlanoPreventivo> PlanosPreventivos => Set<PlanoPreventivo>();
    public DbSet<EventoSincronizado> EventosSincronizados => Set<EventoSincronizado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // cria as tabelas do Identity (AspNetUsers, AspNetRoles, etc.)

        modelBuilder.Entity<OperacaoDiaria>(e =>
        {
            e.ToTable("equipamento_operacao_diaria", t => t.HasCheckConstraint("ck_operacao_horas", "horas_operadas >= 0 AND horas_operadas <= 24"));
            e.HasKey(x => new { x.EquipamentoId, x.Data });
            e.Property(x => x.EquipamentoId).HasColumnName("equipamento_id");
            e.Property(x => x.Data).HasColumnName("data");
            e.Property(x => x.HorasOperadas).HasColumnName("horas_operadas").HasPrecision(4, 2);
            e.HasOne(x => x.Equipamento).WithMany().HasForeignKey(x => x.EquipamentoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventoSincronizado>(e =>
        {
            e.ToTable("os_eventos_sincronizados");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.OrdemServicoId).HasColumnName("ordem_servico_id");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.OcorridoEm).HasColumnName("ocorrido_em");
            e.Property(x => x.Hash).HasColumnName("hash").HasMaxLength(64);
            e.Property(x => x.Resposta).HasColumnName("resposta");
            e.HasIndex(x => new { x.OrdemServicoId, x.OcorridoEm });
            e.HasOne<OrdemServico>().WithMany().HasForeignKey(x => x.OrdemServicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
        // Coluna de sistema do PostgreSQL: protege também contra mudanças online concorrentes.
        modelBuilder.Entity<OrdemServico>().Property<uint>("Version").IsRowVersion().HasColumnName("xmin");


        modelBuilder.HasPostgresEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: new NpgsqlNullNameTranslator());
        modelBuilder.HasPostgresEnum<StatusOS>("status_os_enum", nameTranslator: new NpgsqlNullNameTranslator());
        modelBuilder.HasPostgresEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: new NpgsqlNullNameTranslator());
        modelBuilder.HasPostgresEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: new NpgsqlNullNameTranslator());
        modelBuilder.HasPostgresEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: new NpgsqlNullNameTranslator());


        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); // Id vem do ApplicationUser, não é gerado aqui
            e.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(14);
            e.HasIndex(x => x.Cpf).IsUnique();
            e.Property(x => x.TipoUsuario).HasColumnName("tipo_usuario").HasColumnType("tipo_usuario_enum");
            e.Property(x => x.Ativo).HasColumnName("ativo");
            e.Property(x => x.DataCriacao).HasColumnName("data_criacao");


            e.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<Usuario>(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<Equipamento>(e =>
        {
            e.ToTable("equipamentos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(150).IsRequired();
            e.Property(x => x.CodigoPatrimonio).HasColumnName("codigo_patrimonio").HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.CodigoPatrimonio).IsUnique();
            e.Property(x => x.Setor).HasColumnName("setor").HasMaxLength(100).IsRequired();
            e.Property(x => x.Ativo).HasColumnName("ativo");
        });


        modelBuilder.Entity<CausaFalha>(e =>
        {
            e.ToTable("causas_falha");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Descricao).IsUnique();
            e.Property(x => x.Ativo).HasColumnName("ativo");
        });


        modelBuilder.Entity<Peca>(e =>
        {
            e.ToTable("pecas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(150).IsRequired();
            e.Property(x => x.QuantidadeEstoque).HasColumnName("quantidade_estoque");
            e.Property(x => x.Version).IsRowVersion(); // mapeia para a coluna de sistema xmin
            e.Property(x => x.Critica).HasColumnName("critica");
        });


        modelBuilder.Entity<OrdemServico>(e =>
        {
            e.ToTable("ordens_servico");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClientSyncId).HasColumnName("client_sync_id");
            e.HasIndex(x => x.ClientSyncId).IsUnique();
            e.Property(x => x.NumeroOS).HasColumnName("numero_os").HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.NumeroOS).IsUnique();

            e.Property(x => x.EquipamentoId).HasColumnName("equipamento_id");
            e.HasOne(x => x.Equipamento).WithMany(eq => eq.OrdensServico)
                .HasForeignKey(x => x.EquipamentoId).OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.OperadorId).HasColumnName("operador_id");
            e.HasOne(x => x.Operador).WithMany(u => u.OrdensComoOperador)
                .HasForeignKey(x => x.OperadorId).OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.TecnicoId).HasColumnName("tecnico_id");
            e.HasOne(x => x.Tecnico).WithMany(u => u.OrdensComoTecnico)
                .HasForeignKey(x => x.TecnicoId).OnDelete(DeleteBehavior.SetNull);

            e.Property(x => x.CausaFalhaId).HasColumnName("causa_falha_id");
            e.HasOne(x => x.CausaFalha).WithMany(c => c.OrdensServico)
                .HasForeignKey(x => x.CausaFalhaId).OnDelete(DeleteBehavior.SetNull);

            e.Property(x => x.DescricaoProblema).HasColumnName("descricao_problema").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasColumnType("status_os_enum");
            e.Property(x => x.Prioridade).HasColumnName("prioridade").HasColumnType("prioridade_os_enum");
            e.Property(x => x.TipoManutencao).HasColumnName("tipo_manutencao").HasColumnType("tipo_manutencao_enum");
            e.Property(x => x.PlanoPreventivoId).HasColumnName("plano_preventivo_id");
            e.HasOne(x => x.PlanoPreventivo).WithMany(pp => pp.OrdensGeradas)
                .HasForeignKey(x => x.PlanoPreventivoId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DataAbertura).HasColumnName("data_abertura");
            e.Property(x => x.DataConclusao).HasColumnName("data_conclusao");
            e.Property(x => x.ObservacoesTecnicas).HasColumnName("observacoes_tecnicas");
            e.Property(x => x.CriadoOffline).HasColumnName("criado_offline");


            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DataAbertura);
        });


        modelBuilder.Entity<OsRegistroTempo>(e =>
        {
            e.ToTable("os_registros_tempo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrdemServicoId).HasColumnName("ordem_servico_id");
            e.HasOne(x => x.OrdemServico).WithMany(os => os.RegistrosTempo)
                .HasForeignKey(x => x.OrdemServicoId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.TipoRegistro).HasColumnName("tipo_registro").HasColumnType("tipo_registro_tempo_enum");
            e.Property(x => x.Timestamp).HasColumnName("timestamp");
            e.Property(x => x.MotivoPausa).HasColumnName("motivo_pausa").HasMaxLength(255);
        });


        modelBuilder.Entity<OsPecaUtilizada>(e =>
        {
            e.ToTable("os_pecas_utilizadas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrdemServicoId).HasColumnName("ordem_servico_id");
            e.HasOne(x => x.OrdemServico).WithMany(os => os.PecasUtilizadas)
                .HasForeignKey(x => x.OrdemServicoId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.PecaId).HasColumnName("peca_id");
            e.HasOne(x => x.Peca).WithMany(p => p.PecasUtilizadas)
                .HasForeignKey(x => x.PecaId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Quantidade).HasColumnName("quantidade");
        });

        modelBuilder.Entity<PlanoPreventivo>(e =>
        {
            e.ToTable("planos_preventivos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.EquipamentoId).HasColumnName("equipamento_id");
            e.HasOne(x => x.Equipamento).WithMany()
                .HasForeignKey(x => x.EquipamentoId).OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.TecnicoResponsavelId).HasColumnName("tecnico_responsavel_id");
            e.HasOne(x => x.TecnicoResponsavel).WithMany()
                .HasForeignKey(x => x.TecnicoResponsavelId).OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(300).IsRequired();
            e.Property(x => x.PeriodicidadeDias).HasColumnName("periodicidade_dias");
            e.Property(x => x.ProximaExecucao).HasColumnName("proxima_execucao").HasColumnType("date");
            e.Property(x => x.Ativo).HasColumnName("ativo");

            e.HasIndex(x => x.ProximaExecucao);
        });
    }
}
