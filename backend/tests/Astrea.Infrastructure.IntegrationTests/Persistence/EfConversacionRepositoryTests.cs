using Astrea.Application.Contracts.Pagination;
using Astrea.Domain.Agent;
using Astrea.Domain.Users;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

/// <summary>Historial del asistente en PostgreSQL: mensajes con sus fuentes (jsonb), listado por usuario y depuración.</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class EfConversacionRepositoryTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly FuenteRespuesta Fuente =
        new(Guid.NewGuid(), "Reglamento Académico", 2, "CAPÍTULO I — DE LAS MESAS", "Resolución 4043/09", 1, 2, "La inscripción cierra 48 horas antes.");

    private async Task<Usuario> CrearUsuarioAsync()
    {
        await using var db = baseDeDatos.CrearContexto();
        var usuario = Usuario.Registrar(Guid.NewGuid(), $"uid-{Guid.NewGuid():N}", $"alumno-{Guid.NewGuid():N}@astrea.test", Ahora);
        db.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private async Task<Conversacion> GuardarAsync(Usuario usuario, string pregunta, DateTimeOffset fecha)
    {
        await using var db = baseDeDatos.CrearContexto();
        var conversacion = Conversacion.Iniciar(Guid.CreateVersion7(), usuario.Id, pregunta, fecha);
        conversacion.Registrar(pregunta, "Cierra 48 horas antes [1].", [Fuente], TipoRespuesta.Respuesta, fecha);
        var repositorio = new EfConversacionRepository(db);
        repositorio.Agregar(conversacion);
        await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        return conversacion;
    }

    [Fact]
    public async Task Guarda_los_mensajes_con_sus_fuentes_y_permite_seguir_la_conversacion()
    {
        var usuario = await CrearUsuarioAsync();
        var conversacion = await GuardarAsync(usuario, "¿Cuándo cierra la inscripción?", Ahora);

        await using (var db = baseDeDatos.CrearContexto())
        {
            var repositorio = new EfConversacionRepository(db);
            var guardada = (await repositorio.ObtenerAsync(conversacion.Id, CancellationToken.None))!;
            guardada.Registrar("¿Y en diciembre?", "No encontré información confirmada.", [], TipoRespuesta.SinEvidencia, Ahora.AddMinutes(1));
            await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        }

        await using var lectura = baseDeDatos.CrearContexto();
        var final = (await new EfConversacionRepository(lectura).ObtenerAsync(conversacion.Id, CancellationToken.None))!;
        final.Mensajes.Should().HaveCount(4);
        final.FechaUltimoMensaje.Should().Be(Ahora.AddMinutes(1));
        final.Mensajes.Single(mensaje => mensaje.Rol == RolMensaje.Asistente && !mensaje.SinEvidencia).Fuentes.Should().Equal(Fuente);
        final.Mensajes.Single(mensaje => mensaje.SinEvidencia).Fuentes.Should().BeEmpty();
    }

    [Fact]
    public async Task Guarda_la_propuesta_de_tramite_y_su_confirmacion()
    {
        var usuario = await CrearUsuarioAsync();
        var conversacion = Conversacion.Iniciar(Guid.CreateVersion7(), usuario.Id, "Quiero una constancia", Ahora);
        var propuesta = new PropuestaTramite(Guid.NewGuid(), "Constancia de alumno regular", new Dictionary<string, string?> { ["presentarAnte"] = "ANSES" }, [new DatoPropuesta("Para presentar ante", "ANSES")]);
        var (_, mensaje) = conversacion.Registrar("Quiero una constancia", "Te preparé la solicitud.", [], TipoRespuesta.Respuesta, Ahora, propuesta);
        await using (var db = baseDeDatos.CrearContexto())
        {
            new EfConversacionRepository(db).Agregar(conversacion);
            await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        }

        var tramiteId = Guid.NewGuid();
        await using (var db = baseDeDatos.CrearContexto())
        {
            var guardada = (await new EfConversacionRepository(db).ObtenerAsync(conversacion.Id, CancellationToken.None))!;
            guardada.Mensajes.Single(m => m.Id == mensaje.Id).Propuesta.Should().BeEquivalentTo(propuesta);
            guardada.ConfirmarPropuesta(mensaje.Id, tramiteId, "T-000099", Ahora.AddMinutes(1));
            await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        }

        await using var lectura = baseDeDatos.CrearContexto();
        var final = (await new EfConversacionRepository(lectura).ObtenerAsync(conversacion.Id, CancellationToken.None))!;
        final.Mensajes.Single(m => m.Id == mensaje.Id).Propuesta.Should().BeEquivalentTo(new { Estado = EstadoPropuesta.Confirmada, TramiteId = tramiteId, NumeroTramite = "T-000099" });
    }

    [Fact]
    public async Task Lista_solo_las_del_usuario_la_mas_reciente_primero()
    {
        var usuario = await CrearUsuarioAsync();
        var otro = await CrearUsuarioAsync();
        await GuardarAsync(usuario, "Vieja", Ahora.AddDays(-2));
        await GuardarAsync(usuario, "Nueva", Ahora);
        await GuardarAsync(otro, "De otra persona", Ahora);

        await using var db = baseDeDatos.CrearContexto();
        var listado = await new EfConversacionRepository(db).ListarDeUsuarioAsync(usuario.Id, PageRequest.Create(1, 10), CancellationToken.None);

        listado.TotalItems.Should().Be(2);
        listado.Items.Select(conversacion => conversacion.Titulo).Should().Equal("Nueva", "Vieja");
    }

    [Fact]
    public async Task Elimina_las_conversaciones_inactivas_con_sus_mensajes()
    {
        var usuario = await CrearUsuarioAsync();
        var vieja = await GuardarAsync(usuario, "Vieja", Ahora.AddYears(-5));
        var reciente = await GuardarAsync(usuario, "Reciente", Ahora);

        await using (var db = baseDeDatos.CrearContexto())
        {
            var borradas = await new EfConversacionRepository(db).EliminarInactivasDesdeAsync(Ahora.AddYears(-5).AddDays(1), CancellationToken.None);
            borradas.Should().BeGreaterThanOrEqualTo(1);
        }

        await using var lectura = baseDeDatos.CrearContexto();
        var repositorio = new EfConversacionRepository(lectura);
        (await repositorio.ObtenerAsync(vieja.Id, CancellationToken.None)).Should().BeNull();
        (await repositorio.ObtenerAsync(reciente.Id, CancellationToken.None)).Should().NotBeNull();
        lectura.MensajesChat.Count(mensaje => mensaje.ConversacionId == vieja.Id).Should().Be(0);
    }
}
