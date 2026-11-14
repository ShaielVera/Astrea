using Astrea.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class CarreraConfiguration : IEntityTypeConfiguration<Carrera>
{
    public void Configure(EntityTypeBuilder<Carrera> builder)
    {
        builder.ToTable("carreras");
        builder.HasKey(carrera => carrera.Id);
        builder.Property(carrera => carrera.Id).ValueGeneratedNever();
        builder.Property(carrera => carrera.Codigo).HasMaxLength(20).IsRequired();
        builder.HasIndex(carrera => carrera.Codigo).IsUnique();
        builder.Property(carrera => carrera.Nombre).HasMaxLength(200).IsRequired();
        builder.HasIndex(carrera => carrera.Nombre);
    }
}

internal sealed class PlanEstudioConfiguration : IEntityTypeConfiguration<PlanEstudio>
{
    public void Configure(EntityTypeBuilder<PlanEstudio> builder)
    {
        builder.ToTable("planes_estudio");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.CodigoVersion).HasMaxLength(50).IsRequired();
        builder.Property(plan => plan.Nombre).HasMaxLength(200).IsRequired();

        builder.HasOne<Carrera>()
            .WithMany()
            .HasForeignKey(plan => plan.CarreraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(plan => plan.Materias)
            .WithOne()
            .HasForeignKey(planMateria => planMateria.PlanEstudioId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Materias)
            .HasField("_materias")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(plan => plan.Correlatividades)
            .WithOne()
            .HasForeignKey(correlatividad => correlatividad.PlanEstudioId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Correlatividades)
            .HasField("_correlatividades")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CorrelatividadConfiguration : IEntityTypeConfiguration<Correlatividad>
{
    public void Configure(EntityTypeBuilder<Correlatividad> builder)
    {
        builder.ToTable("correlatividades");
        builder.HasKey(correlatividad => correlatividad.Id);
        builder.Property(correlatividad => correlatividad.Id).ValueGeneratedNever();
        builder.Property(correlatividad => correlatividad.Tipo).HasConversion<string>().HasMaxLength(10);
        builder.Property(correlatividad => correlatividad.Condicion).HasConversion<string>().HasMaxLength(15);
        builder.Property(correlatividad => correlatividad.Descripcion).HasMaxLength(300);
        builder.Ignore(correlatividad => correlatividad.RequiereVerificacionManual);

        // NO ACTION (no RESTRICT): se verifica al final de la sentencia, así borrar un plan
        // en cascada elimina materias del plan y correlatividades sin conflicto de orden.
        builder.HasOne<PlanMateria>()
            .WithMany()
            .HasForeignKey(correlatividad => correlatividad.PlanMateriaDestinoId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<PlanMateria>()
            .WithMany()
            .HasForeignKey(correlatividad => correlatividad.PlanMateriaRequisitoId)
            .OnDelete(DeleteBehavior.NoAction);

        // Consulta principal: requisitos de una materia para cursar o rendir.
        builder.HasIndex(correlatividad => new { correlatividad.PlanMateriaDestinoId, correlatividad.Tipo });

        // Respaldo en la base de la regla de dominio "sin requisitos repetidos".
        builder.HasIndex(correlatividad => new { correlatividad.PlanMateriaDestinoId, correlatividad.Tipo, correlatividad.PlanMateriaRequisitoId })
            .IsUnique()
            .HasFilter("plan_materia_requisito_id IS NOT NULL");
    }
}

internal sealed class MateriaConfiguration : IEntityTypeConfiguration<Materia>
{
    public void Configure(EntityTypeBuilder<Materia> builder)
    {
        builder.ToTable("materias");
        builder.HasKey(materia => materia.Id);
        builder.Property(materia => materia.Id).ValueGeneratedNever();
        builder.Property(materia => materia.Codigo).HasMaxLength(30).IsRequired();
        builder.HasIndex(materia => materia.Codigo).IsUnique();
        builder.Property(materia => materia.Nombre).HasMaxLength(200).IsRequired();
    }
}

internal sealed class PlanMateriaConfiguration : IEntityTypeConfiguration<PlanMateria>
{
    public void Configure(EntityTypeBuilder<PlanMateria> builder)
    {
        builder.ToTable("plan_materias");
        builder.HasKey(planMateria => planMateria.Id);
        builder.Property(planMateria => planMateria.Id).ValueGeneratedNever();
        builder.Property(planMateria => planMateria.Cuatrimestre).HasConversion<string>().HasMaxLength(10);

        builder.HasOne<Materia>()
            .WithMany()
            .HasForeignKey(planMateria => planMateria.MateriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una materia aparece una sola vez por plan; el índice también sirve para listar por año.
        builder.HasIndex(planMateria => new { planMateria.PlanEstudioId, planMateria.MateriaId }).IsUnique();
        builder.HasIndex(planMateria => new { planMateria.PlanEstudioId, planMateria.AnioEnPlan, planMateria.Orden });
    }
}
