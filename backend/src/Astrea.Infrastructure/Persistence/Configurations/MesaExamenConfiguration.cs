using Astrea.Domain.Academic;
using Astrea.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class MesaExamenConfiguration : IEntityTypeConfiguration<MesaExamen>
{
    public void Configure(EntityTypeBuilder<MesaExamen> builder)
    {
        builder.ToTable("mesas_examen");
        builder.HasKey(mesa => mesa.Id);
        builder.Property(mesa => mesa.Id).ValueGeneratedNever();
        builder.Property(mesa => mesa.Turno).HasMaxLength(60).IsRequired();
        builder.Property(mesa => mesa.Aula).HasMaxLength(50);
        builder.Property(mesa => mesa.Observaciones).HasMaxLength(500);
        builder.Property(mesa => mesa.MotivoCancelacion).HasMaxLength(300);
        builder.Property(mesa => mesa.Estado).HasConversion<string>().HasMaxLength(15);
        builder.Ignore(mesa => mesa.Inicio);

        // Fecha (date) y hora (time) en hora del instituto, tal como se publican: filtrar por día no depende del desfase UTC.
        builder.Property(mesa => mesa.Fecha).HasColumnType("date");
        builder.Property(mesa => mesa.Hora).HasColumnType("time without time zone");

        // Integrantes del tribunal como text[]: se listan y se buscan, pero no tienen datos propios.
        builder.PrimitiveCollection(mesa => mesa.Tribunal)
            .HasField("_tribunal")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("tribunal")
            .IsRequired();

        builder.HasOne<PlanMateria>()
            .WithMany()
            .HasForeignKey(mesa => mesa.PlanMateriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Búsqueda principal: por rango de fechas, en orden de fecha y hora.
        builder.HasIndex(mesa => new { mesa.Estado, mesa.Fecha, mesa.Hora });
        builder.HasIndex(mesa => new { mesa.PlanMateriaId, mesa.Fecha });
    }
}
