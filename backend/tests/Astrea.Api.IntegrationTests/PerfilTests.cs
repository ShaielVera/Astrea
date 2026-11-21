using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public sealed class PerfilTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Un DNI distinto en cada test (la base se comparte entre tests).</summary>
    private static string DniNuevo() => Random.Shared.Next(20_000_000, 49_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task El_estudiante_completa_sus_datos_y_valida_el_email_con_un_codigo()
    {
        var estudiante = await EstudianteAsync();
        var emailContacto = $"contacto-{Guid.NewGuid():N}@gmail.com";

        var datos = await estudiante.PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Brenda", apellido = "Vera", dni = DniNuevo(), localidad = "nare", carrerasIds = new[] { await CarreraAsync() } });
        var contacto = await estudiante.PutAsJsonAsync("/api/v1/me/perfil/contacto", new { emailContacto, celular = "0342 15 412-3456" });
        var envio = await estudiante.PostAsync("/api/v1/me/perfil/verificaciones/email", null);
        var reenvioInmediato = await estudiante.PostAsync("/api/v1/me/perfil/verificaciones/email", null);
        var incorrecto = await estudiante.PostAsJsonAsync("/api/v1/me/perfil/verificaciones/email/confirmacion", new { codigo = "000000" == factory.Codigos.Ultimo(emailContacto) ? "111111" : "000000" });
        var correcto = await estudiante.PostAsJsonAsync("/api/v1/me/perfil/verificaciones/email/confirmacion", new { codigo = factory.Codigos.Ultimo(emailContacto) });

        datos.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LeerAsync<Perfil>(datos)).Localidad.Should().Be("Naré");
        (await LeerAsync<Perfil>(contacto)).Celular.Valor.Should().Be("3424123456");
        envio.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LeerAsync<CodigoEnviado>(envio)).Destino.Should().StartWith("co•••@gmail.com");
        reenvioInmediato.StatusCode.Should().Be(HttpStatusCode.Conflict);
        incorrecto.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await incorrecto.Content.ReadAsStringAsync()).Should().Contain("Te quedan 4 intentos");
        correcto.StatusCode.Should().Be(HttpStatusCode.OK, await correcto.Content.ReadAsStringAsync());
        var perfil = await LeerAsync<Perfil>(correcto);
        perfil.EmailContacto.Verificado.Should().BeTrue();
        perfil.EmailContacto.VenceEl.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(365), TimeSpan.FromMinutes(5));
        perfil.Celular.Verificado.Should().BeFalse();
        perfil.Completo.Should().BeTrue();

        // Si cambia el email, hay que volver a validarlo.
        var otro = await estudiante.PutAsJsonAsync("/api/v1/me/perfil/contacto", new { emailContacto = $"otro-{Guid.NewGuid():N}@gmail.com", celular = "3424123456" });
        (await LeerAsync<Perfil>(otro)).EmailContacto.Verificado.Should().BeFalse();
    }

    [Fact]
    public async Task El_estudiante_corrige_sus_datos_aunque_Secretaria_los_haya_cargado()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var estudiante = await EstudianteAsync();
        var dni = DniNuevo();
        await estudiante.PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Brenda", apellido = "Vera", dni });
        var id = (await estudiante.GetFromJsonAsync<Yo>("/api/v1/me", Json))!.Id;

        var deSecretaria = await admin.PutAsJsonAsync($"/api/v1/admin/usuarios/{id}/datos-oficiales", new { nombre = "Brenda", apellido = "Vera", dni, legajo = "L-1" });
        var corregido = await estudiante.PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Brenda Shaiel", apellido = "Vera", dni, localidad = "Sanjusto" });
        var conLocalidad = await estudiante.PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Brenda Shaiel", apellido = "Vera", dni, localidad = "San Justo", carrerasIds = new[] { Guid.NewGuid() } });
        var bien = await estudiante.PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Brenda Shaiel", apellido = "Vera", dni, localidad = "San Justo" });

        deSecretaria.StatusCode.Should().Be(HttpStatusCode.OK);
        corregido.StatusCode.Should().Be(HttpStatusCode.BadRequest, "la localidad se elige de la lista");
        conLocalidad.StatusCode.Should().Be(HttpStatusCode.BadRequest, "la carrera no existe");
        bien.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LeerAsync<Perfil>(bien)).Nombre.Should().Be("Brenda Shaiel");
    }

    [Fact]
    public async Task Las_localidades_se_listan_para_elegir()
    {
        var localidades = await (await EstudianteAsync()).GetFromJsonAsync<List<LocalidadDto>>("/api/v1/localidades", Json);

        localidades!.Select(l => (l.Nombre, l.Departamento)).Should().Contain(("San Justo", "San Justo")).And.Contain(("Los Saladillos", "San Justo")).And.NotContain(l => l.Nombre == "Reconquista");
        localidades!.Single(l => l.Nombre == "Esther").OtrosNombres.Should().Equal("Colonia Esther");
    }

    [Fact]
    public async Task Dos_cuentas_no_pueden_tener_el_mismo_DNI_y_los_datos_se_validan()
    {
        var dni = DniNuevo();
        await (await EstudianteAsync()).PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Ana", apellido = "Gómez", dni });
        var otra = await EstudianteAsync();

        var repetido = await otra.PutAsJsonAsync("/api/v1/me/perfil/datos", new { nombre = "Ana", apellido = "Gómez", dni = $"{dni[..2]}.{dni[2..5]}.{dni[5..]}" });
        var celularInvalido = await otra.PutAsJsonAsync("/api/v1/me/perfil/contacto", new { emailContacto = (string?)null, celular = "4123456" });
        var sinCelular = await otra.PostAsync("/api/v1/me/perfil/verificaciones/celular", null);
        var canalInexistente = await otra.PostAsync("/api/v1/me/perfil/verificaciones/fax", null);

        repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
        celularInvalido.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await celularInvalido.Content.ReadAsStringAsync()).Should().Contain("código de área");
        sinCelular.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        canalInexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sin_sesion_no_hay_perfil()
    {
        (await factory.CreateClient().GetAsync("/api/v1/me/perfil")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record Contacto(string? Valor, bool Verificado, DateTimeOffset? VenceEl, bool Vencido);

    private sealed record Perfil(string? Nombre, string? Apellido, string? Dni, string? Localidad, List<Guid> CarrerasIds, Contacto EmailContacto, Contacto Celular, bool Completo);

    private sealed record LocalidadDto(string Nombre, string Departamento, List<string> OtrosNombres);

    private sealed record CarreraCreada(Guid Id);

    /// <summary>Crea una carrera activa (como administrador) para elegirla en el perfil.</summary>
    private async Task<Guid> CarreraAsync()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var codigo = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var respuesta = await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = $"Carrera {codigo}", duracionAnios = 3 });
        respuesta.EnsureSuccessStatusCode();
        return (await LeerAsync<CarreraCreada>(respuesta)).Id;
    }

    private sealed record CodigoEnviado(string Destino, DateTimeOffset VenceEl, bool Simulado);

    private sealed record Yo(Guid Id);
}
