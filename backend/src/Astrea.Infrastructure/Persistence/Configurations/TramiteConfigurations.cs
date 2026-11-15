using System.Text.Json;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class TramiteTipoConfiguration : IEntityTypeConfiguration<TramiteTipo>
{
    public void Configure(EntityTypeBuilder<TramiteTipo> builder)
    {
        builder.ToTable("tramite_tipos");
        builder.HasKey(tipo => tipo.Id);
        builder.Property(tipo => tipo.Id).ValueGeneratedNever();
        builder.Property(tipo => tipo.Codigo).HasMaxLength(40).IsRequired();
        builder.HasIndex(tipo => tipo.Codigo).IsUnique();
        builder.Property(tipo => tipo.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(tipo => tipo.Descripcion).HasMaxLength(500).IsRequired();

        // El formulario de cada tipo (su "esquema") se guarda como jsonb.
        builder.Property(tipo => tipo.Campos)
            .HasColumnType("jsonb")
            .HasConversion(Json.Conversor<IReadOnlyList<CampoTramite>>(() => []), Json.Comparador<IReadOnlyList<CampoTramite>>());

        // Catálogo inicial del SDD: viaja con la migración, así todas las bases lo tienen.
        builder.HasData(CatalogoTramites.Tipos());
    }
}

internal sealed class TramiteConfiguration : IEntityTypeConfiguration<Tramite>
{
    public const string SecuenciaNumero = "tramite_numero_seq";

    public void Configure(EntityTypeBuilder<Tramite> builder)
    {
        builder.ToTable("tramites");
        builder.HasKey(tramite => tramite.Id);
        builder.Property(tramite => tramite.Id).ValueGeneratedNever();
        builder.Property(tramite => tramite.Estado).HasConversion<string>().HasMaxLength(15);
        builder.Property(tramite => tramite.ObservacionAdministrativa).HasMaxLength(500);
        builder.Ignore(tramite => tramite.EstaFinalizado);

        // Número correlativo para ventanilla, asignado por PostgreSQL (sin huecos por concurrencia entre pedidos).
        builder.Property(tramite => tramite.Numero)
            .HasDefaultValueSql($"nextval('{SecuenciaNumero}')")
            .ValueGeneratedOnAdd();
        builder.HasIndex(tramite => tramite.Numero).IsUnique();

        builder.Property(tramite => tramite.Datos)
            .HasColumnType("jsonb")
            .HasConversion(Json.Conversor<IReadOnlyDictionary<string, string>>(() => new Dictionary<string, string>()), Json.Comparador<IReadOnlyDictionary<string, string>>());

        // Concurrencia optimista con la columna de sistema xmin: si dos administradores cambian
        // el mismo trámite a la vez, el segundo recibe un conflicto en lugar de pisar al primero.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(tramite => tramite.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(tramite => tramite.AdministradorAsignadoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TramiteTipo>().WithMany().HasForeignKey(tramite => tramite.TramiteTipoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(tramite => tramite.Eventos)
            .WithOne()
            .HasForeignKey(evento => evento.TramiteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(tramite => tramite.Eventos)
            .HasField("_eventos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // "Mis trámites" (por titular) y la bandeja (por estado, los más antiguos primero).
        builder.HasIndex(tramite => new { tramite.UsuarioId, tramite.FechaSolicitud });
        builder.HasIndex(tramite => new { tramite.Estado, tramite.FechaSolicitud });

        // Respaldo en la base de la idempotencia: una misma clave crea un solo trámite por titular.
        builder.HasIndex(tramite => new { tramite.UsuarioId, tramite.ClaveIdempotencia })
            .IsUnique()
            .HasFilter("clave_idempotencia IS NOT NULL");
    }
}

internal sealed class TramiteEventoConfiguration : IEntityTypeConfiguration<TramiteEvento>
{
    public void Configure(EntityTypeBuilder<TramiteEvento> builder)
    {
        builder.ToTable("tramite_eventos");
        builder.HasKey(evento => evento.Id);
        builder.Property(evento => evento.Id).ValueGeneratedNever();
        builder.Property(evento => evento.EstadoAnterior).HasConversion<string>().HasMaxLength(15);
        builder.Property(evento => evento.EstadoNuevo).HasConversion<string>().HasMaxLength(15);
        builder.Property(evento => evento.Comentario).HasMaxLength(500);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(evento => evento.ActorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(evento => new { evento.TramiteId, evento.Fecha });
    }
}

internal sealed class PlantillaDocumentoConfiguration : IEntityTypeConfiguration<PlantillaDocumento>
{
    public void Configure(EntityTypeBuilder<PlantillaDocumento> builder)
    {
        builder.ToTable("plantillas_documento");
        builder.HasKey(plantilla => plantilla.Id);
        builder.Property(plantilla => plantilla.Id).ValueGeneratedNever();
        builder.Property(plantilla => plantilla.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(plantilla => plantilla.Cuerpo).HasMaxLength(4000).IsRequired();
        builder.HasOne<TramiteTipo>().WithMany().HasForeignKey(plantilla => plantilla.TramiteTipoId).OnDelete(DeleteBehavior.Restrict);

        // Una sola versión con cada número por tipo; la vigente es la de número más alto.
        builder.HasIndex(plantilla => new { plantilla.TramiteTipoId, plantilla.Version }).IsUnique();

        builder.HasData(CatalogoTramites.PlantillasIniciales());
    }
}

internal sealed class DocumentoEmitidoConfiguration : IEntityTypeConfiguration<DocumentoEmitido>
{
    public void Configure(EntityTypeBuilder<DocumentoEmitido> builder)
    {
        builder.ToTable("documentos_emitidos");
        builder.HasKey(documento => documento.Id);
        builder.Property(documento => documento.Id).ValueGeneratedNever();
        builder.Property(documento => documento.TipoDocumento).HasMaxLength(120).IsRequired();
        builder.Property(documento => documento.CodigoValidacion).HasMaxLength(14).IsRequired();
        builder.Property(documento => documento.StoragePath).HasMaxLength(300).IsRequired();
        builder.Property(documento => documento.HashSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(documento => documento.DniUltimosDigitos).HasMaxLength(3);
        builder.Property(documento => documento.Estado).HasConversion<string>().HasMaxLength(10);
        builder.Property(documento => documento.MotivoAnulacion).HasMaxLength(300);

        builder.HasOne<Tramite>().WithMany().HasForeignKey(documento => documento.TramiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(documento => documento.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(documento => documento.AnuladoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlantillaDocumento>().WithMany().HasForeignKey(documento => documento.PlantillaId).OnDelete(DeleteBehavior.Restrict);

        // El código del QR identifica al documento en la validación pública.
        builder.HasIndex(documento => documento.CodigoValidacion).IsUnique();

        // Respaldo de la idempotencia: un trámite tiene a lo sumo un documento vigente.
        builder.HasIndex(documento => documento.TramiteId)
            .IsUnique()
            .HasFilter("estado = 'Vigente'");
    }
}

/// <summary>Conversión a JSON para columnas jsonb con tipos de solo lectura del dominio.</summary>
internal static class Json
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static ValueConverter<T, string> Conversor<T>(Func<T> vacio)
        where T : class =>
        new(valor => JsonSerializer.Serialize(valor, Opciones), json => JsonSerializer.Deserialize<T>(json, Opciones) ?? vacio());

    public static ValueComparer<T> Comparador<T>()
        where T : class =>
        new(
            (a, b) => JsonSerializer.Serialize(a, Opciones) == JsonSerializer.Serialize(b, Opciones),
            valor => JsonSerializer.Serialize(valor, Opciones).GetHashCode(StringComparison.Ordinal),
            valor => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(valor, Opciones), Opciones)!);
}
