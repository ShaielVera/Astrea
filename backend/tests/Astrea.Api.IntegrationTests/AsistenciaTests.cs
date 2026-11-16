using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

/// <summary>
/// Confirmación de asistencia de punta a punta: Secretaría asigna materias a un docente, los estudiantes declaran qué cursan,
/// la dirección abre un relevamiento, el profesor confirma y el informe se filtra por la lista de DNI de la beca.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AsistenciaTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly string _marca = $"Z{Guid.NewGuid():N}"[..9].ToUpperInvariant();

    private sealed record Sesion(HttpClient Cliente, Guid Id, string Email);

    private async Task<Sesion> SesionAsync(string? email = null)
    {
        email ??= $"persona-{Guid.NewGuid():N}@astrea.test";
        var cliente = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        var perfil = await LeerAsync<Perfil>(await cliente.PostAsync("/api/v1/auth/sesion", null));
        return new Sesion(cliente, perfil.Id, email);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        respuesta.IsSuccessStatusCode.Should().BeTrue(await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static DateOnly HoyEnElInstituto() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires")).DateTime);

    /// <summary>Carrera propia del test con dos materias: devuelve sus planMateriaId.</summary>
    private async Task<(Guid MateriaA, Guid MateriaB)> CrearMateriasAsync(HttpClient admin)
    {
        var carrera = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo = _marca, nombre = $"Carrera {_marca}", duracionAnios = 3 }));
        var plan = await LeerAsync<IdDto>(await admin.PostAsJsonAsync($"/api/v1/admin/carreras/{carrera.Id}/planes", new { codigoVersion = "Res. 1/26", nombre = "Plan 2026" }));
        var a = await LeerAsync<MateriaDto>(await admin.PostAsJsonAsync($"/api/v1/admin/planes/{plan.Id}/materias", new { nombre = $"Didáctica {_marca}", anio = 1, cuatrimestre = "Anual" }));
        var b = await LeerAsync<MateriaDto>(await admin.PostAsJsonAsync($"/api/v1/admin/planes/{plan.Id}/materias", new { nombre = $"Pedagogía {_marca}", anio = 1, cuatrimestre = "Anual" }));
        return (a.PlanMateriaId, b.PlanMateriaId);
    }

    private async Task CargarDniAsync(HttpClient admin, Sesion estudiante, string apellido, string dni) =>
        (await admin.PutAsJsonAsync($"/api/v1/admin/usuarios/{estudiante.Id}/datos-oficiales", new { nombre = "Ana", apellido, dni, legajo = (string?)null }))
            .EnsureSuccessStatusCode();

    private static Task<HttpResponseMessage> ConfirmarAsync(Sesion docente, Guid relevamientoId, Guid planMateriaId, params object[] confirmaciones) =>
        docente.Cliente.PostAsJsonAsync($"/api/v1/docente/relevamientos/{relevamientoId}/materias/{planMateriaId}/confirmaciones", new { confirmaciones });

    [Fact]
    public async Task Del_relevamiento_al_informe_filtrado_por_dni_sin_guardar_quien_cobra_la_beca()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var (materiaA, materiaB) = await CrearMateriasAsync(admin.Cliente);

        // Secretaría busca al profesor por email, le da el rol y le asigna una materia.
        var profe = await SesionAsync($"profe-{_marca.ToLowerInvariant()}@astrea.test");
        var candidatos = await LeerAsync<List<Candidato>>(await admin.Cliente.GetAsync($"/api/v1/admin/docentes/candidatos?texto=profe-{_marca.ToLowerInvariant()}"));
        candidatos.Should().ContainSingle().Which.EsDocente.Should().BeFalse();
        (await admin.Cliente.PostAsync($"/api/v1/admin/docentes/{profe.Id}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await LeerAsync<object>(await admin.Cliente.PostAsJsonAsync($"/api/v1/admin/docentes/{profe.Id}/materias", new { planMateriaId = materiaA }));
        (await admin.Cliente.PostAsJsonAsync($"/api/v1/admin/docentes/{profe.Id}/materias", new { planMateriaId = materiaA })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var docentes = await LeerAsync<List<Docente>>(await admin.Cliente.GetAsync("/api/v1/admin/docentes"));
        docentes.Should().ContainSingle(docente => docente.Id == profe.Id).Which.Materias.Should().ContainSingle(materia => materia.PlanMateriaId == materiaA);

        // Los estudiantes declaran qué cursan.
        var cumplidora = await SesionAsync();
        var ausente = await SesionAsync();
        foreach (var (estudiante, materia) in new[] { (cumplidora, materiaA), (cumplidora, materiaB), (ausente, materiaA) })
        {
            await LeerAsync<object>(await estudiante.Cliente.PostAsJsonAsync("/api/v1/me/cursada", new { planMateriaId = materia }));
        }

        (await ausente.Cliente.PostAsJsonAsync("/api/v1/me/cursada", new { planMateriaId = materiaA })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var dniCumplidora = $"3{Random.Shared.Next(1000000, 9999999)}";
        var dniAusente = $"4{Random.Shared.Next(1000000, 9999999)}";
        await CargarDniAsync(admin.Cliente, cumplidora, $"Cumplidora {_marca}", dniCumplidora);
        await CargarDniAsync(admin.Cliente, ausente, $"Ausente {_marca}", dniAusente);

        // La dirección abre el relevamiento.
        var relevamiento = await LeerAsync<Relevamiento>(await admin.Cliente.PostAsJsonAsync(
            "/api/v1/admin/asistencia/relevamientos", new { titulo = $"Receso {_marca}", fechaCierre = HoyEnElInstituto().AddDays(10) }));
        relevamiento.Abierto.Should().BeTrue();

        // El profesor ve solo su materia y sus inscriptos.
        (await LeerAsync<List<Relevamiento>>(await profe.Cliente.GetAsync("/api/v1/docente/relevamientos"))).Should().Contain(r => r.Id == relevamiento.Id);
        var panel = await LeerAsync<Panel>(await profe.Cliente.GetAsync($"/api/v1/docente/relevamientos/{relevamiento.Id}"));
        panel.Materias.Should().ContainSingle().Which.Estudiantes.Select(estudiante => estudiante.Id).Should().BeEquivalentTo([cumplidora.Id, ausente.Id]);

        (await ConfirmarAsync(profe, relevamiento.Id, materiaA,
                new { estudianteId = cumplidora.Id, valor = "AsisteRegularmente" },
                new { estudianteId = ausente.Id, valor = "NoAsiste", observacion = "No vino desde abril" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // No puede confirmar en una materia ajena ni sobre alguien que no está inscripto.
        (await ConfirmarAsync(profe, relevamiento.Id, materiaB, new { estudianteId = cumplidora.Id, valor = "NoAsiste" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ConfirmarAsync(profe, relevamiento.Id, materiaA, new { estudianteId = admin.Id, valor = "NoAsiste" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // El estudiante ve lo que confirmó su profesor y no puede borrarlo dándose de baja.
        var miAsistencia = await LeerAsync<MiAsistencia>(await ausente.Cliente.GetAsync("/api/v1/me/asistencia"));
        miAsistencia.Confirmaciones.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Valor = "NoAsiste", Observacion = "No vino desde abril" });
        (await ausente.Cliente.DeleteAsync($"/api/v1/me/cursada/{materiaA}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await cumplidora.Cliente.DeleteAsync($"/api/v1/me/cursada/{materiaB}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Avance para la dirección.
        var avance = await LeerAsync<Avance>(await admin.Cliente.GetAsync($"/api/v1/admin/asistencia/relevamientos/{relevamiento.Id}/avance"));
        avance.Materias.Should().ContainSingle(materia => materia.PlanMateriaId == materiaA).Which.Should().BeEquivalentTo(new { Inscriptos = 2, Confirmados = 2 });

        // Informe filtrado por la lista de la beca (por DNI, en el cuerpo del pedido).
        var dniDesconocido = "29999999";
        var informe = await LeerAsync<Informe>(await admin.Cliente.PostAsJsonAsync(
            $"/api/v1/admin/asistencia/relevamientos/{relevamiento.Id}/informe", new { dnis = $"{dniAusente}\n{dniDesconocido}" }));
        informe.Estudiantes.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = ausente.Id, Dni = dniAusente, NoAsiste = 1, Regular = 0 });
        informe.Estudiantes.Items[0].Materias.Should().ContainSingle().Which.Docente.Should().Be(profe.Email);
        informe.DnisSinDatos.Should().Equal(dniDesconocido);

        // Cerrado, ya no se puede cambiar nada.
        (await admin.Cliente.PostAsync($"/api/v1/admin/asistencia/relevamientos/{relevamiento.Id}/cierre", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConfirmarAsync(profe, relevamiento.Id, materiaA, new { estudianteId = ausente.Id, valor = "AsisteRegularmente" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cada_rol_ve_solo_lo_suyo()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var estudiante = await SesionAsync();
        var profe = await SesionAsync();
        (await admin.Cliente.PostAsync($"/api/v1/admin/docentes/{profe.Id}", null)).EnsureSuccessStatusCode();

        // Un estudiante no entra al área docente ni a los informes; un docente no entra a la administración.
        (await estudiante.Cliente.GetAsync("/api/v1/docente/relevamientos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await estudiante.Cliente.GetAsync("/api/v1/admin/asistencia/relevamientos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await profe.Cliente.GetAsync("/api/v1/admin/asistencia/relevamientos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await profe.Cliente.GetAsync("/api/v1/docente/relevamientos")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Al quitarle el rol, pierde el acceso en el pedido siguiente (los roles salen de la base, no del token).
        (await admin.Cliente.DeleteAsync($"/api/v1/admin/docentes/{profe.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await profe.Cliente.GetAsync("/api/v1/docente/relevamientos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Validaciones_de_secretaria()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var noDocente = await SesionAsync();
        var (materiaA, _) = await CrearMateriasAsync(admin.Cliente);

        (await admin.Cliente.GetAsync("/api/v1/admin/docentes/candidatos?texto=ab")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Cliente.PostAsJsonAsync($"/api/v1/admin/docentes/{noDocente.Id}/materias", new { planMateriaId = materiaA })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Cliente.PostAsync($"/api/v1/admin/docentes/{Guid.NewGuid()}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Cliente.PostAsJsonAsync("/api/v1/admin/asistencia/relevamientos", new { titulo = "Viejo", fechaCierre = HoyEnElInstituto().AddDays(-1) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Cliente.PostAsJsonAsync($"/api/v1/admin/asistencia/relevamientos/{Guid.NewGuid()}/informe", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record IdDto(Guid Id);

    private sealed record MateriaDto(Guid PlanMateriaId, Guid MateriaId);

    private sealed record Perfil(Guid Id);

    private sealed record Candidato(Guid Id, string Email, bool EsDocente);

    private sealed record AsignacionFila(Guid Id, Guid PlanMateriaId);

    private sealed record Docente(Guid Id, List<AsignacionFila> Materias);

    private sealed record Relevamiento(Guid Id, string Titulo, bool Abierto);

    private sealed record EstudiantePanel(Guid Id, string? Valor);

    private sealed record MateriaPanel(Guid PlanMateriaId, List<EstudiantePanel> Estudiantes);

    private sealed record Panel(List<MateriaPanel> Materias);

    private sealed record Confirmacion(string Valor, string? Observacion);

    private sealed record MiAsistencia(List<Confirmacion> Confirmaciones);

    private sealed record MateriaAvance(Guid PlanMateriaId, int Inscriptos, int Confirmados);

    private sealed record Avance(List<MateriaAvance> Materias);

    private sealed record MateriaInforme(string Materia, string? Valor, string? Docente);

    private sealed record EstudianteInforme(Guid Id, string? Dni, int Regular, int NoAsiste, List<MateriaInforme> Materias);

    private sealed record PaginaInforme(List<EstudianteInforme> Items);

    private sealed record Informe(PaginaInforme Estudiantes, List<string> DnisSinDatos);
}
