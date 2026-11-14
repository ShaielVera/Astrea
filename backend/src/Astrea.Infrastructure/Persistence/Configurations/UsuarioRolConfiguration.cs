using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioRolConfiguration : IEntityTypeConfiguration<UsuarioRol>
{
    public void Configure(EntityTypeBuilder<UsuarioRol> builder)
    {
        builder.ToTable("usuario_roles");
        builder.HasKey(asignacion => new { asignacion.UsuarioId, asignacion.RolId });

        builder.HasOne<Rol>()
            .WithMany()
            .HasForeignKey(asignacion => asignacion.RolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(asignacion => asignacion.AsignadoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
