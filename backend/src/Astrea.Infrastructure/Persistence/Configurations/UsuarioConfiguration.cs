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
        builder.HasIndex(usuario => usuario.Dni);
        builder.Property(usuario => usuario.EmailContacto).HasMaxLength(320);
        builder.Property(usuario => usuario.Celular).HasMaxLength(15);
        builder.Property(usuario => usuario.Localidad).HasMaxLength(100);
        builder.PrimitiveCollection(usuario => usuario.CarrerasIds);

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

internal sealed class CodigoVerificacionConfiguration : IEntityTypeConfiguration<CodigoVerificacion>
{
    public void Configure(EntityTypeBuilder<CodigoVerificacion> builder)
    {
        builder.ToTable("codigos_verificacion");
        builder.HasKey(codigo => codigo.Id);
        builder.Property(codigo => codigo.Id).ValueGeneratedNever();
        builder.Property(codigo => codigo.Canal).HasConversion<string>().HasMaxLength(10);
        builder.Property(codigo => codigo.Destino).HasMaxLength(320).IsRequired();
        builder.Property(codigo => codigo.Hash).HasMaxLength(64).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(codigo => codigo.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(codigo => new { codigo.UsuarioId, codigo.Canal, codigo.CreadoEn });
        builder.HasIndex(codigo => codigo.VenceEn);
    }
}
