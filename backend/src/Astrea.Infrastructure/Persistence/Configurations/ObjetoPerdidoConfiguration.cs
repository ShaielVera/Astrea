using Astrea.Domain.LostAndFound;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class ObjetoPerdidoConfiguration : IEntityTypeConfiguration<ObjetoPerdido>
{
    public void Configure(EntityTypeBuilder<ObjetoPerdido> builder)
    {
        builder.ToTable("objetos_perdidos");
        builder.HasKey(objeto => objeto.Id);
        builder.Property(objeto => objeto.Id).ValueGeneratedNever();
        builder.Property(objeto => objeto.TituloPublico).HasMaxLength(80).IsRequired();
        builder.Property(objeto => objeto.DescripcionPrivada).HasMaxLength(500).IsRequired();
        builder.Property(objeto => objeto.SectorEncontrado).HasMaxLength(80).IsRequired();
        builder.Property(objeto => objeto.Categoria).HasConversion<string>().HasMaxLength(20);
        builder.Property(objeto => objeto.FranjaHoraria).HasConversion<string>().HasMaxLength(10);
        builder.Property(objeto => objeto.PuntoResguardo).HasConversion<string>().HasMaxLength(25);
        builder.Property(objeto => objeto.Estado).HasConversion<string>().HasMaxLength(15);
        builder.Property(objeto => objeto.FotoStoragePath).HasMaxLength(300);
        builder.Property(objeto => objeto.FotoContentType).HasMaxLength(20);
        builder.Property(objeto => objeto.MotivoBaja).HasMaxLength(300);
        builder.Ignore(objeto => objeto.EstaPublicado);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(objeto => objeto.PublicadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(objeto => objeto.DadoDeBajaPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(objeto => objeto.Entrega)
            .WithOne()
            .HasForeignKey<EntregaObjeto>(entrega => entrega.ObjetoPerdidoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Búsqueda pública: publicados, los encontrados más recientemente primero.
        builder.HasIndex(objeto => new { objeto.Estado, objeto.FechaHallazgo });
        builder.HasIndex(objeto => new { objeto.PublicadoPorUsuarioId, objeto.FechaPublicacion });
    }
}

internal sealed class EntregaObjetoConfiguration : IEntityTypeConfiguration<EntregaObjeto>
{
    public void Configure(EntityTypeBuilder<EntregaObjeto> builder)
    {
        builder.ToTable("entregas_objeto");
        builder.HasKey(entrega => entrega.Id);
        builder.Property(entrega => entrega.Id).ValueGeneratedNever();
        builder.Property(entrega => entrega.NombreRetirante).HasMaxLength(120).IsRequired();
        builder.Property(entrega => entrega.DniRetirante).HasMaxLength(9).IsRequired();
        builder.Property(entrega => entrega.ObservacionInterna).HasMaxLength(500);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(entrega => entrega.VerificadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Un objeto se entrega una sola vez.
        builder.HasIndex(entrega => entrega.ObjetoPerdidoId).IsUnique();
    }
}
