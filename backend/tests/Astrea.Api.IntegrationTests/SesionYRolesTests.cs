using System.Net;
using System.Net.Http.Json;
using Astrea.Api.IntegrationTests.Infrastructure;
using Astrea.Domain.Users;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Api.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public sealed class SesionYRolesTests(AstreaApiFactory factory)
{
    private static string UidUnico() => $"uid-{Guid.NewGuid():N}";

    private async Task<(HttpClient Client, Perfil Perfil)> IniciarSesionAsync(string email, bool emailVerificado = true)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear(UidUnico(), email, emailVerificado));
        var response = await client.PostAsync("/api/v1/auth/sesion", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (client, (await response.Content.ReadFromJsonAsync<Perfil>())!);
    }

    [Fact]
    public async Task Varios_inicios_de_sesion_simultaneos_de_un_usuario_nuevo_crean_uno_solo()
    {
        // Ej. el administrador abre varias pestañas a la vez la primera vez que entra.
        var uid = UidUnico();
        var token = TokensDePrueba.Crear(uid, AstreaApiFactory.AdministradorInicial);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => factory.CrearClienteCon(token).PostAsync("/api/v1/auth/sesion", content: null)));

        respuestas.Select(respuesta => respuesta.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        await factory.EjecutarEnBaseAsync(async db =>
        {
            (await db.Usuarios.CountAsync(usuario => usuario.FirebaseUid == uid)).Should().Be(1);
            var usuario = await db.Usuarios.SingleAsync(candidato => candidato.FirebaseUid == uid);
            usuario.Roles.Should().HaveCount(2);
        });
    }

    [Fact]
    public async Task Antes_de_iniciar_sesion_en_Astrea_el_perfil_devuelve_404()
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear(UidUnico(), "nueva@astrea.test"));

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Iniciar_sesion_da_de_alta_al_estudiante_y_el_perfil_lo_devuelve()
    {
        var (client, perfil) = await IniciarSesionAsync("Alumna.Nueva@Astrea.test");

        perfil.Email.Should().Be("alumna.nueva@astrea.test");
        perfil.Roles.Should().Equal(Rol.CodigoEstudiante);

        var me = await client.GetFromJsonAsync<Perfil>("/api/v1/me");
        me.Should().BeEquivalentTo(perfil);
    }

    [Fact]
    public async Task Iniciar_sesion_varias_veces_no_duplica_al_usuario()
    {
        var uid = UidUnico();
        var client = factory.CrearClienteCon(TokensDePrueba.Crear(uid, "repetida@astrea.test"));

        var primero = await (await client.PostAsync("/api/v1/auth/sesion", null)).Content.ReadFromJsonAsync<Perfil>();
        var segundo = await (await client.PostAsync("/api/v1/auth/sesion", null)).Content.ReadFromJsonAsync<Perfil>();

        segundo!.Id.Should().Be(primero!.Id);
        await factory.EjecutarEnBaseAsync(async db =>
            (await db.Usuarios.CountAsync(usuario => usuario.FirebaseUid == uid)).Should().Be(1));
    }

    [Fact]
    public async Task Un_estudiante_no_puede_entrar_al_area_de_administracion()
    {
        var (client, _) = await IniciarSesionAsync("estudiante@astrea.test");

        var response = await client.GetAsync("/api/v1/admin/usuarios/resumen");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_administrador_inicial_con_email_verificado_entra_al_area_de_administracion()
    {
        var (client, perfil) = await IniciarSesionAsync(AstreaApiFactory.AdministradorInicial);

        perfil.Roles.Should().BeEquivalentTo(Rol.CodigoAdministrador, Rol.CodigoEstudiante);
        var response = await client.GetAsync("/api/v1/admin/usuarios/resumen");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resumen = await response.Content.ReadFromJsonAsync<Resumen>();
        resumen!.Administradores.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Con_el_email_del_administrador_sin_verificar_no_se_obtiene_el_rol()
    {
        var (client, perfil) = await IniciarSesionAsync(AstreaApiFactory.AdministradorInicial, emailVerificado: false);

        perfil.Roles.Should().Equal(Rol.CodigoEstudiante);
        (await client.GetAsync("/api/v1/admin/usuarios/resumen")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sin_sesion_el_area_de_administracion_devuelve_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/admin/usuarios/resumen");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_usuario_desactivado_pierde_el_acceso_aunque_su_token_sea_valido()
    {
        var (client, perfil) = await IniciarSesionAsync(AstreaApiFactory.AdministradorInicial);
        await factory.EjecutarEnBaseAsync(async db =>
        {
            var usuario = await db.Usuarios.SingleAsync(u => u.Id == perfil.Id);
            usuario.Desactivar(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/api/v1/auth/sesion", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/admin/usuarios/resumen")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record Perfil(Guid Id, string Email, string? Nombre, string? Apellido, IReadOnlyList<string> Roles);

    private sealed record Resumen(int Estudiantes, int Administradores);
}
