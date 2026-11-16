using Astrea.Infrastructure.Persistence.Repositories;
using Astrea.Infrastructure.Persistence.Seed;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

/// <summary>Búsqueda de materias por nombre (la usa el asistente): sin distinguir tildes, en planes vigentes y por carrera.</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class BusquedaDeMateriasTests(BaseDeDatosFixture baseDeDatos) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = baseDeDatos.CrearContexto();
        await CatalogoInstitucionalSeeder.SembrarAsync(db, new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Encuentra_la_materia_sin_tildes_y_pone_primero_la_coincidencia_exacta()
    {
        await using var db = baseDeDatos.CrearContexto();

        var encontradas = await new EfAcademicRepository(db).BuscarMateriasAsync("programacion i", null, 10, CancellationToken.None);

        encontradas[0].Should().BeEquivalentTo(new { Nombre = "Programación I", Carrera = "Tecnicatura Superior en Desarrollo de Software" });
        encontradas.Select(materia => materia.Nombre).Should().Contain("Programación II");
    }

    [Fact]
    public async Task Una_materia_de_varias_carreras_se_puede_filtrar_por_carrera()
    {
        await using var db = baseDeDatos.CrearContexto();
        var repositorio = new EfAcademicRepository(db);
        var inicial = await db.Carreras.SingleAsync(carrera => carrera.Codigo == "PEI");

        var todas = await repositorio.BuscarMateriasAsync("Pedagogía", null, 10, CancellationToken.None);
        var deInicial = await repositorio.BuscarMateriasAsync("Pedagogía", inicial.Id, 10, CancellationToken.None);

        todas.Where(materia => materia.Nombre == "Pedagogía").Select(materia => materia.Carrera).Distinct().Should().HaveCountGreaterThan(1);
        deInicial.Should().OnlyContain(materia => materia.CarreraId == inicial.Id);
        deInicial.Should().Contain(materia => materia.Nombre == "Pedagogía");
    }

    [Fact]
    public async Task Los_comodines_del_texto_no_se_interpretan()
    {
        await using var db = baseDeDatos.CrearContexto();

        (await new EfAcademicRepository(db).BuscarMateriasAsync("%", null, 10, CancellationToken.None)).Should().BeEmpty();
    }
}
