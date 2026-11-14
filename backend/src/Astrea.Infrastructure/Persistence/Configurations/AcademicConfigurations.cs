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
