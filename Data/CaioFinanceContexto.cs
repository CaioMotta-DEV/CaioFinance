using CaioFinance.Models;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Data;

public class CaioFinanceContexto(DbContextOptions<CaioFinanceContexto> opcoes) : DbContext(opcoes)
{
    public DbSet<ContaBanco> ContasBanco => Set<ContaBanco>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<LancamentoFuturo> LancamentosFuturos => Set<LancamentoFuturo>();
    public DbSet<Cartao> Cartoes => Set<Cartao>();
    public DbSet<GastoCartao> GastosCartao => Set<GastoCartao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContaBanco>(entidade =>
        {
            entidade.ToTable("CONTA_BANCO");
            entidade.HasKey(item => item.nCdConta);
            entidade.Property(item => item.nCdConta).ValueGeneratedOnAdd();
            entidade.Property(item => item.sNmInstituicao).HasMaxLength(80).IsRequired();
            entidade.Property(item => item.sNmApelido).HasMaxLength(80);
            entidade.Property(item => item.dVlSaldo).HasPrecision(14, 2);
            entidade.Property(item => item.nCdSituacao).HasConversion<int>();
            entidade.Property(item => item.tDtAtualizacao).HasColumnType("timestamp without time zone");
            entidade.Property(item => item.sIdItemPluggy).HasMaxLength(40);
            entidade.Property(item => item.sIdContaPluggy).HasMaxLength(40);
            entidade.HasIndex(item => item.sIdItemPluggy);
            entidade.HasIndex(item => item.sIdContaPluggy).IsUnique();
        });

        modelBuilder.Entity<Categoria>(entidade =>
        {
            entidade.ToTable("CATEGORIA");
            entidade.HasKey(item => item.nCdCategoria);
            entidade.Property(item => item.nCdCategoria).ValueGeneratedOnAdd();
            entidade.Property(item => item.sNmCategoria).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<Lancamento>(entidade =>
        {
            entidade.ToTable("LANCAMENTO");
            entidade.HasKey(item => item.nCdLancamento);
            entidade.Property(item => item.nCdLancamento).ValueGeneratedOnAdd();
            entidade.Property(item => item.sNmOriginal).HasMaxLength(160).IsRequired();
            entidade.Property(item => item.sNmExibicao).HasMaxLength(160).IsRequired();
            entidade.Property(item => item.dVlLancamento).HasPrecision(14, 2);
            entidade.Property(item => item.bFlOculto).HasDefaultValue(false);
            entidade.Property(item => item.nCdOrigem).HasConversion<int>();
            entidade.Property(item => item.sIdTransacaoPluggy).HasMaxLength(40);
            entidade.HasIndex(item => item.sIdTransacaoPluggy).IsUnique();
            entidade.Property(item => item.tDtOcorrencia).HasColumnType("date");
            entidade.Property(item => item.tDtCriacao).HasColumnType("timestamp without time zone");
            entidade.HasIndex(item => item.tDtOcorrencia);
            entidade.HasIndex(item => item.nCdSerie);
            entidade.HasOne(item => item.Conta)
                .WithMany(conta => conta.Lancamentos)
                .HasForeignKey(item => item.nCdConta);
            entidade.HasOne(item => item.Categoria)
                .WithMany(categoria => categoria.Lancamentos)
                .HasForeignKey(item => item.nCdCategoria);
        });

        modelBuilder.Entity<LancamentoFuturo>(entidade =>
        {
            entidade.ToTable("LANCAMENTO_FUTURO");
            entidade.HasKey(item => item.nCdLancamentoFuturo);
            entidade.Property(item => item.nCdLancamentoFuturo).ValueGeneratedOnAdd();
            entidade.Property(item => item.sNmLancamento).HasMaxLength(160).IsRequired();
            entidade.Property(item => item.dVlLancamento).HasPrecision(14, 2);
            entidade.Property(item => item.nCdTipo).HasConversion<int>();
            entidade.Property(item => item.sIdOrigem).HasMaxLength(80);
            entidade.Property(item => item.sDsSerie).HasMaxLength(64);
            entidade.HasIndex(item => item.sIdOrigem).IsUnique();
            entidade.Property(item => item.tDtVencimento).HasColumnType("date");
            entidade.Property(item => item.tDtCriacao).HasColumnType("timestamp without time zone");
            entidade.HasIndex(item => item.tDtVencimento);
            entidade.HasIndex(item => new { item.nCdSerie, item.tDtVencimento });
            entidade.HasOne(item => item.Conta)
                .WithMany(conta => conta.LancamentosFuturos)
                .HasForeignKey(item => item.nCdConta);
            entidade.HasOne(item => item.Categoria)
                .WithMany(categoria => categoria.LancamentosFuturos)
                .HasForeignKey(item => item.nCdCategoria);
        });

        modelBuilder.Entity<Cartao>(entidade =>
        {
            entidade.ToTable("CARTAO");
            entidade.HasKey(item => item.nCdCartao);
            entidade.Property(item => item.nCdCartao).ValueGeneratedOnAdd();
            entidade.Property(item => item.sIdItemPluggy).HasMaxLength(40).IsRequired();
            entidade.Property(item => item.sIdContaPluggy).HasMaxLength(40).IsRequired();
            entidade.Property(item => item.sNmInstituicao).HasMaxLength(80).IsRequired();
            entidade.Property(item => item.sNmCartao).HasMaxLength(80).IsRequired();
            entidade.Property(item => item.sNmApelido).HasMaxLength(80);
            entidade.Property(item => item.sNmBandeira).HasMaxLength(40).IsRequired();
            entidade.Property(item => item.sNrFinal).HasMaxLength(4).IsRequired();
            entidade.Property(item => item.sDsCor).HasMaxLength(7).IsRequired();
            entidade.Property(item => item.bFlManual).HasDefaultValue(false);
            entidade.Property(item => item.bFlOculto).HasDefaultValue(false);
            entidade.Property(item => item.tDtAtualizacao).HasColumnType("timestamp without time zone");
            entidade.HasIndex(item => item.sIdItemPluggy);
            entidade.HasIndex(item => item.sIdContaPluggy).IsUnique();
        });

        modelBuilder.Entity<GastoCartao>(entidade =>
        {
            entidade.ToTable("GASTO_CARTAO");
            entidade.HasKey(item => item.nCdGasto);
            entidade.Property(item => item.nCdGasto).ValueGeneratedOnAdd();
            entidade.Property(item => item.sIdTransacaoPluggy).HasMaxLength(40).IsRequired();
            entidade.Property(item => item.sIdFaturaPluggy).HasMaxLength(40);
            entidade.Property(item => item.sNmDescricao).HasMaxLength(160).IsRequired();
            entidade.Property(item => item.sNmApelido).HasMaxLength(160);
            entidade.Property(item => item.sNmCategoria).HasMaxLength(40).IsRequired();
            entidade.Property(item => item.dVlGasto).HasPrecision(14, 2);
            entidade.Property(item => item.bFlManual).HasDefaultValue(false);
            entidade.Property(item => item.bFlEditado).HasDefaultValue(false);
            entidade.Property(item => item.bFlOculto).HasDefaultValue(false);
            entidade.Property(item => item.bFlContaFixa).HasDefaultValue(false);
            entidade.HasIndex(item => item.nCdSerie);
            entidade.Property(item => item.tDtCompra).HasColumnType("date");
            entidade.Property(item => item.tDtFatura).HasColumnType("date");
            entidade.Property(item => item.tDtCriacao).HasColumnType("timestamp without time zone");
            entidade.HasIndex(item => item.sIdTransacaoPluggy).IsUnique();
            entidade.HasIndex(item => item.tDtFatura);
            entidade.HasOne(item => item.Cartao)
                .WithMany(cartao => cartao.Gastos)
                .HasForeignKey(item => item.nCdCartao);
        });
    }
}
