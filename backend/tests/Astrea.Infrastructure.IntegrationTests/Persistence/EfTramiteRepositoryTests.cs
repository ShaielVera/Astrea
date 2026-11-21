using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Academic;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class EfTramiteRepositoryTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private async Task<(Usuario Estudiante, Usuario Admin, Tramite Tramite)> CrearTramiteAsync()
    {
        await using var db = baseDeDatos.CrearContexto();
        var estudiante = Usuario.Registrar(Guid.NewGuid(), $"uid-{Guid.NewGuid():N}", $"ana-{Guid.NewGuid():N}@astrea.test", Ahora);
        var admin = Usuario.Registrar(Guid.NewGuid(), $"uid-{Guid.NewGuid():N}", $"secretaria-{Guid.NewGuid():N}@astrea.test", Ahora);
        var carrera = Carrera.Crear(Guid.NewGuid(), $"C{Guid.NewGuid():N}"[..10].ToUpperInvariant(), "Carrera", 3, Ahora);
        var tipo = await db.TramiteTipos.SingleAsync(candidato => candidato.Id == CatalogoTramites.AlumnoRegularId);
        var tramite = Tramite.Solicitar(
            Guid.NewGuid(),
            estudiante.Id,
            tipo,
            new Dictionary<string, string?> { ["carrera"] = carrera.Id.ToString(), ["presentarAnte"] = "ANSES" },
            null,
            Ahora);

        db.AddRange(estudiante, admin, carrera);
        new EfTramiteRepository(db).Agregar(tramite);
        await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        return (estudiante, admin, tramite);
    }

    [Fact]
    public async Task El_catalogo_inicial_viaja_con_la_migracion()
    {
        await using var db = baseDeDatos.CrearContexto();

        var repositorio = new EfTramiteRepository(db);
        var tipos = await repositorio.ListarTiposAsync(incluirInactivos: true, CancellationToken.None);

        tipos.Select(tipo => tipo.Codigo).Should().BeEquivalentTo("ALUMNO_REGULAR", "CONSTANCIA_EXAMEN");
        tipos.Single(tipo => tipo.Codigo == "CONSTANCIA_EXAMEN").Campos
            .Should().Contain(campo => campo.Clave == "fechaExamen" && campo.Tipo == TipoCampo.Fecha && campo.SinFechasFuturas);
    }

    [Fact]
    public async Task Guarda_el_tramite_con_numero_datos_jsonb_e_historial()
    {
        var (estudiante, _, tramite) = await CrearTramiteAsync();

        await using var db = baseDeDatos.CrearContexto();
        var guardado = await new EfTramiteRepository(db).ObtenerAsync(tramite.Id, CancellationToken.None);

        guardado!.Numero.Should().BePositive();
        guardado.UsuarioId.Should().Be(estudiante.Id);
        guardado.Datos.Should().ContainKey("presentarAnte").WhoseValue.Should().Be("ANSES");
        guardado.Eventos.Should().ContainSingle().Which.EstadoNuevo.Should().Be(EstadoTramite.Pendiente);
    }

    [Fact]
    public async Task Dos_cambios_simultaneos_sobre_el_mismo_tramite_son_un_conflicto_de_concurrencia()
    {
        var (_, admin, tramite) = await CrearTramiteAsync();

        // Dos administradores abren el mismo trámite antes de que el otro guarde.
        await using var db1 = baseDeDatos.CrearContexto();
        await using var db2 = baseDeDatos.CrearContexto();
        var copia1 = await new EfTramiteRepository(db1).ObtenerAsync(tramite.Id, CancellationToken.None);
        var copia2 = await new EfTramiteRepository(db2).ObtenerAsync(tramite.Id, CancellationToken.None);

        copia1!.Aprobar(admin.Id, "Listo", Ahora);
        await new EfUnitOfWork(db1).GuardarCambiosAsync(CancellationToken.None);

        copia2!.Rechazar(admin.Id, "Rechazado", Ahora);
        var guardarSegundo = () => new EfUnitOfWork(db2).GuardarCambiosAsync(CancellationToken.None);

        await guardarSegundo.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        await using var verificacion = baseDeDatos.CrearContexto();
        (await verificacion.Tramites.SingleAsync(candidato => candidato.Id == tramite.Id)).Estado.Should().Be(EstadoTramite.Listo);
        (await verificacion.TramiteEventos.CountAsync(evento => evento.TramiteId == tramite.Id)).Should().Be(2);
    }

    [Fact]
    public async Task La_bandeja_busca_por_numero_y_filtra_por_estado()
    {
        var (_, _, tramite) = await CrearTramiteAsync();
        await using var db = baseDeDatos.CrearContexto();
        var repositorio = new EfTramiteRepository(db);
        var numero = (await repositorio.ObtenerAsync(tramite.Id, CancellationToken.None))!.Numero;

        var pendientes = await repositorio.BuscarAsync(new FiltroTramites(Estados: [EstadoTramite.Pendiente], Texto: NumeroTramite.Formatear(numero)), PageRequest.Create(1, 10), CancellationToken.None);
        var listos = await repositorio.BuscarAsync(new FiltroTramites(Estados: [EstadoTramite.Listo], Texto: numero.ToString()), PageRequest.Create(1, 10), CancellationToken.None);

        pendientes.Items.Should().ContainSingle().Which.Id.Should().Be(tramite.Id);
        listos.TotalItems.Should().Be(0);
    }
}
