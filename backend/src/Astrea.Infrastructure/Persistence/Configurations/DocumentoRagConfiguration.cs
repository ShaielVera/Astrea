using Astrea.Domain.Academic;
using Astrea.Domain.Knowledge;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class DocumentoRagConfiguration : IEntityTypeConfiguration<DocumentoRag>
{
    public void Configure(EntityTypeBuilder<DocumentoRag> builder)
    {
        builder.ToTable("documentos_rag");
        builder.HasKey(documento => documento.Id);
        builder.Property(documento => documento.Id).ValueGeneratedNever();
        builder.Property(documento => documento.Titulo).HasMaxLength(150).IsRequired();
        builder.Property(documento => documento.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(documento => documento.Fuente).HasMaxLength(200);
        builder.Property(documento => documento.StoragePath).HasMaxLength(300).IsRequired();
        builder.Property(documento => documento.HashSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(documento => documento.Estado).HasConversion<string>().HasMaxLength(15);
        builder.Property(documento => documento.ErrorProcesamiento).HasMaxLength(500);
        builder.Ignore(documento => documento.EsConsultable);

        // Concurrencia optimista (xmin): el procesador y la Secretaría pueden tocar el mismo documento.
        builder.Property<uint>("Concurrencia").IsRowVersion();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(documento => documento.CargadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(documento => documento.RetiradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Carrera>().WithMany().HasForeignKey(documento => documento.CarreraId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(documento => new { documento.SerieId, documento.Version }).IsUnique();
        builder.HasIndex(documento => new { documento.Estado, documento.FechaCarga });
        builder.HasIndex(documento => documento.HashSha256);
        builder.HasIndex(documento => new { documento.CarreraId, documento.Tipo });
    }
}

internal sealed class FragmentoRagConfiguration : IEntityTypeConfiguration<FragmentoRag>
{
    public void Configure(EntityTypeBuilder<FragmentoRag> builder)
    {
        builder.ToTable("fragmentos_rag");
        builder.HasKey(fragmento => fragmento.Id);
        builder.Property(fragmento => fragmento.Id).ValueGeneratedNever();
        builder.Property(fragmento => fragmento.Contenido).IsRequired();

        builder.Property(fragmento => fragmento.Embedding)
            .HasColumnType($"vector({FragmentoRag.DimensionesEmbedding})")
            .HasConversion(
                vector => new Vector(vector),
                vector => vector.ToArray(),
                new ValueComparer<float[]>((a, b) => a!.SequenceEqual(b!), vector => vector.Length, vector => vector.ToArray()));

        builder.Property(fragmento => fragmento.Metadata)
            .HasColumnName("metadata_json")
            .HasColumnType("jsonb")
            .HasConversion(Json.Conversor<MetadataFragmento>(() => throw new InvalidOperationException("Fragmento sin metadatos.")), Json.Comparador<MetadataFragmento>());

        builder.HasOne<DocumentoRag>().WithMany().HasForeignKey(fragmento => fragmento.DocumentoRagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(fragmento => new { fragmento.DocumentoRagId, fragmento.NumeroFragmento }).IsUnique();

        // Búsqueda aproximada por similitud coseno.
        builder.HasIndex(fragmento => fragmento.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
    }
}
