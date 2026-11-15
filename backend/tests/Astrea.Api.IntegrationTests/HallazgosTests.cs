using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using Astrea.Application.Features.LostAndFound;
using Astrea.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Astrea.Api.IntegrationTests;

/// <summary>Ciclo del objeto perdido: publicar (con foto), buscar, entregar o dar de baja (Slice 7).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class HallazgosTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private Task<HttpClient> AdminAsync() => SesionAsync(AstreaApiFactory.AdministradorInicial);

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>Publica un hallazgo con un título único para poder encontrarlo entre los de otros tests.</summary>
    private static async Task<(Mio Hallazgo, string Titulo)> PublicarAsync(HttpClient cliente, string? sector = null)
    {
        var titulo = $"Termo {Guid.NewGuid():N}"[..20];
        var response = await cliente.PostAsJsonAsync("/api/v1/hallazgos", new
        {
            tituloPublico = titulo,
            descripcionPrivada = "Tiene una calcomanía de Racing y una abolladura",
            categoria = "TermoOBotella",
            sectorEncontrado = sector ?? "Aula 3, planta baja",
            franjaHoraria = "Manana",
            puntoResguardo = "AulaInfraestructura",
            fechaHallazgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await LeerAsync<Mio>(response), titulo);
    }

    private static Task<HttpResponseMessage> SubirFotoAsync(HttpClient cliente, Guid id, byte[] contenido, string contentType)
    {
        var cuerpo = new ByteArrayContent(contenido);
        cuerpo.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return cliente.PutAsync($"/api/v1/hallazgos/{id}/foto", cuerpo);
    }

    private static Task<HttpResponseMessage> BuscarAsync(HttpClient cliente, string texto) =>
        cliente.GetAsync($"/api/v1/hallazgos?texto={Uri.EscapeDataString(texto)}");

    [Fact]
    public async Task Sin_foto_no_aparece_en_la_busqueda_y_con_foto_otro_estudiante_lo_encuentra_sin_ver_datos_privados()
    {
        var publicador = await EstudianteAsync();
        var buscador = await EstudianteAsync();
        var (hallazgo, titulo) = await PublicarAsync(publicador);

        (await LeerAsync<Pagina>(await BuscarAsync(buscador, titulo))).TotalItems.Should().Be(0);
        (await buscador.GetAsync($"/api/v1/hallazgos/{hallazgo.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await LeerAsync<Mio>(await SubirFotoAsync(publicador, hallazgo.Id, Png, "image/png"));
        var response = await BuscarAsync(buscador, titulo);

        var json = await response.Content.ReadAsStringAsync();
        var pagina = JsonSerializer.Deserialize<Pagina>(json, Json)!;
        pagina.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new { hallazgo.Id, TituloPublico = titulo, PuntoResguardo = "AulaInfraestructura", TieneFoto = true });
        json.Should().NotContain("calcomanía").And.NotContain("Racing").And.NotContain("alumno-").And.NotContainEquivalentOf("descripcionPrivada");
    }

    [Fact]
    public async Task La_busqueda_no_encuentra_por_la_descripcion_privada()
    {
        var publicador = await EstudianteAsync();
        var (hallazgo, _) = await PublicarAsync(publicador);
        await SubirFotoAsync(publicador, hallazgo.Id, Png, "image/png");

        var pagina = await LeerAsync<Pagina>(await BuscarAsync(await EstudianteAsync(), "abolladura"));

        pagina.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Quien_lo_publico_sube_la_foto_y_cualquiera_con_sesion_la_ve()
    {
        var publicador = await EstudianteAsync();
        var (hallazgo, _) = await PublicarAsync(publicador);

        var subida = await LeerAsync<Mio>(await SubirFotoAsync(publicador, hallazgo.Id, Png, "image/png"));
        var foto = await (await EstudianteAsync()).GetAsync($"/api/v1/hallazgos/{hallazgo.Id}/foto");

        subida.TieneFoto.Should().BeTrue();
        foto.StatusCode.Should().Be(HttpStatusCode.OK);
        foto.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await foto.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
        (await factory.CreateClient().GetAsync($"/api/v1/hallazgos/{hallazgo.Id}/foto")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("<html>no soy una imagen</html>", "image/png", "JPEG, PNG o WebP")]
    [InlineData(null, "image/jpeg", "no coincide")]
    public async Task Una_foto_que_no_es_imagen_o_con_tipo_falso_se_rechaza(string? contenido, string contentType, string mensaje)
    {
        var publicador = await EstudianteAsync();
        var (hallazgo, _) = await PublicarAsync(publicador);

        var response = await SubirFotoAsync(publicador, hallazgo.Id, contenido is null ? Png : Encoding.UTF8.GetBytes(contenido), contentType);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(mensaje);
    }

    [Fact]
    public async Task Una_foto_de_mas_de_5_MB_se_rechaza()
    {
        var publicador = await EstudianteAsync();
        var (hallazgo, _) = await PublicarAsync(publicador);
        byte[] enorme = [.. Png, .. new byte[(5 * 1024 * 1024) + 1]];

        var response = await SubirFotoAsync(publicador, hallazgo.Id, enorme, "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("5 MB");
    }

    [Fact]
    public async Task Otro_estudiante_no_puede_cambiar_la_foto_ajena()
    {
        var (hallazgo, _) = await PublicarAsync(await EstudianteAsync());

        var response = await SubirFotoAsync(await EstudianteAsync(), hallazgo.Id, Png, "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task La_secretaria_lo_entrega_con_verificacion_y_desaparece_del_listado()
    {
        var publicador = await EstudianteAsync();
        var (hallazgo, titulo) = await PublicarAsync(publicador);
        await SubirFotoAsync(publicador, hallazgo.Id, Png, "image/png");
        var admin = await AdminAsync();

        var sinVerificar = await admin.PostAsJsonAsync($"/api/v1/admin/hallazgos/{hallazgo.Id}/entrega", new { nombreRetirante = "Juan López", dniRetirante = "30.123.456", identidadVerificada = false });
        sinVerificar.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var entregado = await LeerAsync<Admin>(await admin.PostAsJsonAsync(
            $"/api/v1/admin/hallazgos/{hallazgo.Id}/entrega",
            new { nombreRetirante = "Juan López", dniRetirante = "30.123.456", identidadVerificada = true, observacionInterna = "Describió la calcomanía" }));

        entregado.Estado.Should().Be("Entregado");
        entregado.DescripcionPrivada.Should().Contain("calcomanía");
        entregado.Entrega!.DniRetirante.Should().Be("30123456");
        entregado.Entrega.VerificadoPor.Should().Be(AstreaApiFactory.AdministradorInicial);
        entregado.Entrega.IdentidadVerificada.Should().BeTrue();

        var otro = await EstudianteAsync();
        (await LeerAsync<Pagina>(await BuscarAsync(otro, titulo))).TotalItems.Should().Be(0);
        (await otro.GetAsync($"/api/v1/hallazgos/{hallazgo.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otro.GetAsync($"/api/v1/hallazgos/{hallazgo.Id}/foto")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var mios = await LeerAsync<PaginaMios>(await publicador.GetAsync("/api/v1/me/hallazgos"));
        mios.Items.Should().ContainSingle().Which.Estado.Should().Be("Entregado");
    }

    [Fact]
    public async Task La_secretaria_da_de_baja_una_publicacion_y_no_se_puede_entregar_despues()
    {
        var publicador = await EstudianteAsync();
        var (hallazgo, titulo) = await PublicarAsync(publicador);
        await SubirFotoAsync(publicador, hallazgo.Id, Png, "image/png");
        var admin = await AdminAsync();

        var baja = await LeerAsync<Admin>(await admin.PostAsJsonAsync($"/api/v1/admin/hallazgos/{hallazgo.Id}/baja", new { motivo = "Publicación duplicada" }));
        var entregar = await admin.PostAsJsonAsync($"/api/v1/admin/hallazgos/{hallazgo.Id}/entrega", new { nombreRetirante = "Juan", dniRetirante = "30123456", identidadVerificada = true });

        baja.Estado.Should().Be("DadoDeBaja");
        baja.DadoDeBajaPor.Should().Be(AstreaApiFactory.AdministradorInicial);
        entregar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LeerAsync<Pagina>(await BuscarAsync(admin, titulo))).TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task A_los_30_dias_de_entregado_se_borra_con_su_foto_y_los_datos_de_quien_lo_retiro()
    {
        var publicador = await EstudianteAsync();
        var (vencido, _) = await PublicarAsync(publicador);
        var (reciente, _) = await PublicarAsync(publicador);
        await SubirFotoAsync(publicador, vencido.Id, Png, "image/png");
        var admin = await AdminAsync();
        foreach (var id in new[] { vencido.Id, reciente.Id })
        {
            (await admin.PostAsJsonAsync($"/api/v1/admin/hallazgos/{id}/entrega", new { nombreRetirante = "Juan López", dniRetirante = "30123456", identidadVerificada = true }))
                .EnsureSuccessStatusCode();
        }

        string rutaFoto;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var objeto = await db.ObjetosPerdidos.SingleAsync(o => o.Id == vencido.Id);
            rutaFoto = Path.Combine(factory.CarpetaArchivos, objeto.FotoStoragePath!);
            await db.Database.ExecuteSqlAsync($"UPDATE objetos_perdidos SET fecha_cierre = now() - interval '31 days' WHERE id = {vencido.Id}");
        }

        File.Exists(rutaFoto).Should().BeTrue();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<DepuracionHallazgos>().DepurarAsync(CancellationToken.None)).Should().BeGreaterThanOrEqualTo(1);
        }

        (await admin.GetAsync($"/api/v1/admin/hallazgos/{vencido.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync($"/api/v1/admin/hallazgos/{reciente.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(rutaFoto).Should().BeFalse();
    }

    [Fact]
    public async Task Un_estudiante_no_accede_a_la_administracion_de_hallazgos()
    {
        var (hallazgo, _) = await PublicarAsync(await EstudianteAsync());
        var estudiante = await EstudianteAsync();

        (await estudiante.GetAsync("/api/v1/admin/hallazgos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await estudiante.PostAsJsonAsync($"/api/v1/admin/hallazgos/{hallazgo.Id}/entrega", new { nombreRetirante = "Yo", dniRetirante = "30123456", identidadVerificada = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_hallazgo_con_fecha_futura_no_se_publica()
    {
        var response = await (await EstudianteAsync()).PostAsJsonAsync("/api/v1/hallazgos", new
        {
            tituloPublico = "Llaves",
            descripcionPrivada = "Llavero rojo",
            categoria = "Llaves",
            sectorEncontrado = "Patio",
            franjaHoraria = "Tarde",
            puntoResguardo = "Secretaria",
            fechaHallazgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record Publico(Guid Id, string TituloPublico, string PuntoResguardo, bool TieneFoto);

    private sealed record Pagina(List<Publico> Items, int TotalItems);

    private sealed record Mio(Guid Id, string TituloPublico, string DescripcionPrivada, bool TieneFoto, string Estado);

    private sealed record PaginaMios(List<Mio> Items, int TotalItems);

    private sealed record Entrega(string NombreRetirante, string DniRetirante, bool IdentidadVerificada, string VerificadoPor);

    private sealed record Admin(Guid Id, string Estado, string DescripcionPrivada, Entrega? Entrega, string? DadoDeBajaPor);
}
