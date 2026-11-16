using Astrea.Domain.Agent;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Astrea.Infrastructure.Persistence.Configurations;

internal sealed class ConversacionConfiguration : IEntityTypeConfiguration<Conversacion>
{
    public void Configure(EntityTypeBuilder<Conversacion> builder)
    {
        builder.ToTable("conversaciones");
        builder.HasKey(conversacion => conversacion.Id);
        builder.Property(conversacion => conversacion.Id).ValueGeneratedNever();
        builder.Property(conversacion => conversacion.Titulo).HasMaxLength(Conversacion.LargoMaximoTitulo).IsRequired();
        builder.Ignore(conversacion => conversacion.CantidadDePreguntas);
        builder.Ignore(conversacion => conversacion.UltimaPregunta);

        // Dos pestañas preguntando en la misma conversación a la vez: la segunda recibe un conflicto.
        builder.Property<uint>("Concurrencia").IsRowVersion();

        // Si se borra el usuario, se borra su historial con el asistente.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(conversacion => conversacion.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(conversacion => conversacion.Mensajes)
            .WithOne()
            .HasForeignKey(mensaje => mensaje.ConversacionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(conversacion => conversacion.Mensajes)
            .HasField("_mensajes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Listado del usuario (la más reciente primero) y depuración de las inactivas.
        builder.HasIndex(conversacion => new { conversacion.UsuarioId, conversacion.FechaUltimoMensaje });
        builder.HasIndex(conversacion => conversacion.FechaUltimoMensaje);
    }
}

internal sealed class MensajeChatConfiguration : IEntityTypeConfiguration<MensajeChat>
{
    public void Configure(EntityTypeBuilder<MensajeChat> builder)
    {
        builder.ToTable("mensajes_chat");
        builder.HasKey(mensaje => mensaje.Id);
        builder.Property(mensaje => mensaje.Id).ValueGeneratedNever();
        builder.Property(mensaje => mensaje.Rol).HasConversion<string>().HasMaxLength(15);
        builder.Property(mensaje => mensaje.Contenido).HasMaxLength(Conversacion.LargoMaximoRespuesta).IsRequired();
        builder.Property(mensaje => mensaje.Fuentes)
            .HasColumnName("fuentes_json")
            .HasColumnType("jsonb")
            .HasConversion(Json.Conversor<List<FuenteRespuesta>>(() => []), Json.Comparador<List<FuenteRespuesta>>());
        builder.HasIndex(mensaje => new { mensaje.ConversacionId, mensaje.Fecha });
    }
}
