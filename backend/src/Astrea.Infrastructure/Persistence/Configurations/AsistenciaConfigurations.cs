using Astrea.Domain.Academic;
using Astrea.Domain.Attendance;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class AsignacionDocenteConfiguration : IEntityTypeConfiguration<AsignacionDocente>
{
    public void Configure(EntityTypeBuilder<AsignacionDocente> builder)
    {
        builder.ToTable("asignaciones_docentes");
        builder.HasKey(asignacion => asignacion.Id);
        builder.Property(asignacion => asignacion.Id).ValueGeneratedNever();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(asignacion => asignacion.DocenteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlanMateria>().WithMany().HasForeignKey(asignacion => asignacion.PlanMateriaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(asignacion => new { asignacion.DocenteId, asignacion.PlanMateriaId }).IsUnique();
        builder.HasIndex(asignacion => asignacion.PlanMateriaId);
    }
}

internal sealed class InscripcionCursadaConfiguration : IEntityTypeConfiguration<InscripcionCursada>
{
    public void Configure(EntityTypeBuilder<InscripcionCursada> builder)
    {
        builder.ToTable("inscripciones_cursada");
        builder.HasKey(inscripcion => inscripcion.Id);
        builder.Property(inscripcion => inscripcion.Id).ValueGeneratedNever();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(inscripcion => inscripcion.EstudianteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlanMateria>().WithMany().HasForeignKey(inscripcion => inscripcion.PlanMateriaId).OnDelete(DeleteBehavior.Cascade);

        // Dos clics en "Inscribirme" no crean dos inscripciones.
        builder.HasIndex(inscripcion => new { inscripcion.EstudianteId, inscripcion.PlanMateriaId, inscripcion.CicloLectivo }).IsUnique();
        builder.HasIndex(inscripcion => new { inscripcion.PlanMateriaId, inscripcion.CicloLectivo });
    }
}

internal sealed class RelevamientoAsistenciaConfiguration : IEntityTypeConfiguration<RelevamientoAsistencia>
{
    public void Configure(EntityTypeBuilder<RelevamientoAsistencia> builder)
    {
        builder.ToTable("relevamientos_asistencia");
        builder.HasKey(relevamiento => relevamiento.Id);
        builder.Property(relevamiento => relevamiento.Id).ValueGeneratedNever();
        builder.Property(relevamiento => relevamiento.Titulo).HasMaxLength(RelevamientoAsistencia.LargoMaximoTitulo).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(relevamiento => relevamiento.CreadoPor).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(relevamiento => relevamiento.CreadoEn);
    }
}

internal sealed class ConfirmacionAsistenciaConfiguration : IEntityTypeConfiguration<ConfirmacionAsistencia>
{
    public void Configure(EntityTypeBuilder<ConfirmacionAsistencia> builder)
    {
        builder.ToTable("confirmaciones_asistencia");
        builder.HasKey(confirmacion => confirmacion.Id);
        builder.Property(confirmacion => confirmacion.Id).ValueGeneratedNever();
        builder.Property(confirmacion => confirmacion.Valor).HasConversion<string>().HasMaxLength(30);
        builder.Property(confirmacion => confirmacion.Observacion).HasMaxLength(ConfirmacionAsistencia.LargoMaximoObservacion);
        builder.HasOne<RelevamientoAsistencia>().WithMany().HasForeignKey(confirmacion => confirmacion.RelevamientoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlanMateria>().WithMany().HasForeignKey(confirmacion => confirmacion.PlanMateriaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(confirmacion => confirmacion.EstudianteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(confirmacion => confirmacion.DocenteId).OnDelete(DeleteBehavior.Restrict);

        // Dos profesores de la misma materia (o dos pestañas) no generan dos confirmaciones para el mismo estudiante.
        builder.HasIndex(confirmacion => new { confirmacion.RelevamientoId, confirmacion.PlanMateriaId, confirmacion.EstudianteId }).IsUnique();
        builder.HasIndex(confirmacion => confirmacion.EstudianteId);
    }
}
