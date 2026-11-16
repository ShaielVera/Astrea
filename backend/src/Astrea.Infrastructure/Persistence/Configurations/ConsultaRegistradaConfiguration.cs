using Astrea.Domain.Agent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

/// <summary>Telemetría anónima del asistente: sin relación con usuarios ni conversaciones.</summary>
internal sealed class ConsultaRegistradaConfiguration : IEntityTypeConfiguration<ConsultaRegistrada>
{
    public void Configure(EntityTypeBuilder<ConsultaRegistrada> builder)
    {
        builder.ToTable("consultas_asistente");
        builder.HasKey(consulta => consulta.Id);
        builder.Property(consulta => consulta.Id).ValueGeneratedNever();
        builder.Property(consulta => consulta.Tema).HasMaxLength(ConsultaRegistrada.LargoMaximoTema).IsRequired();
        builder.Property(consulta => consulta.Resultado).HasConversion<string>().HasMaxLength(20);
        builder.Property(consulta => consulta.Motivo).HasConversion<string>().HasMaxLength(30);
        builder.Property(consulta => consulta.Herramientas).HasColumnType("text[]");

        // Agregaciones por período y depuración de los registros viejos.
        builder.HasIndex(consulta => consulta.Dia);
        builder.HasIndex(consulta => consulta.Fecha);
    }
}
