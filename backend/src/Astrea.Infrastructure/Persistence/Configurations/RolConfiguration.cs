using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(rol => rol.Id);
        builder.Property(rol => rol.Id).ValueGeneratedNever();
        builder.Property(rol => rol.Codigo).HasMaxLength(50).IsRequired();
        builder.HasIndex(rol => rol.Codigo).IsUnique();
        builder.Property(rol => rol.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(rol => rol.Descripcion).HasMaxLength(500).IsRequired();

        builder.HasData(Rol.Catalogo.Select(rol => new
        {
            rol.Id,
            rol.Codigo,
            rol.Nombre,
            rol.Descripcion,
        }));
    }
}
