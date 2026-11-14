using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(usuario => usuario.Id);
        builder.Property(usuario => usuario.Id).ValueGeneratedNever();

        builder.Property(usuario => usuario.FirebaseUid).HasMaxLength(128).IsRequired();
        builder.HasIndex(usuario => usuario.FirebaseUid).IsUnique();

        // No es único: una cuenta de Firebase borrada y recreada tiene otro UID con el mismo email.
        builder.Property(usuario => usuario.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(usuario => usuario.Email);

        builder.Property(usuario => usuario.Nombre).HasMaxLength(100);
        builder.Property(usuario => usuario.Apellido).HasMaxLength(100);
        builder.Property(usuario => usuario.Dni).HasMaxLength(20);
        builder.Property(usuario => usuario.Legajo).HasMaxLength(30);

        builder.HasMany(usuario => usuario.Roles)
            .WithOne()
            .HasForeignKey(asignacion => asignacion.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(usuario => usuario.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
