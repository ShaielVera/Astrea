using Astrea.Domain.Exams;
using Astrea.Infrastructure.Persistence.Seed;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

/// <summary>Las mesas publicadas por el instituto se cruzan con el catálogo real y se cargan publicadas, sin duplicarse.</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class MesasInstitucionalesSeederTests(BaseDeDatosFixture baseDeDatos) : IAsyncLifetime
{
    private const string Turno = "Noviembre-Diciembre 2026";

    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await BorrarMesasAsync();
        await using var db = baseDeDatos.CrearContexto();
        await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
    }

    // Otros tests borran el catálogo: las mesas no pueden quedar apuntando a sus materias.
    public Task DisposeAsync() => BorrarMesasAsync();

    private async Task BorrarMesasAsync()
    {
        await using var db = baseDeDatos.CrearContexto();
        await db.MesasExamen.Where(mesa => mesa.Turno == Turno).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Carga_todas_las_mesas_publicadas_cruzadas_con_su_materia_y_no_las_duplica()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            var resultado = await MesasInstitucionalesSeeder.SembrarAsync(db, Ahora);

            resultado.Advertencias.Should().BeEmpty();
            resultado.MesasCreadas.Should().Be(516, "258 materias publicadas con 2 llamados (una va a dos materias del plan y otra tiene un solo llamado confirmado)");
        }

        await using (var db = baseDeDatos.CrearContexto())
        {
            var otraVez = await MesasInstitucionalesSeeder.SembrarAsync(db, Ahora);
            otraVez.MesasCreadas.Should().Be(0);
            otraVez.MesasExistentes.Should().Be(516);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        var mesas = await consulta.MesasExamen.Where(mesa => mesa.Turno == Turno).ToListAsync();
        mesas.Should().OnlyContain(mesa => mesa.Estado == EstadoMesa.Publicada && mesa.Tribunal.Count >= 1);

        // Programación II (TSDS, 3.er año): 03/12 y 17/12 a las 13:30.
        var programacion = await (
                from mesa in consulta.MesasExamen
                join planMateria in consulta.PlanMaterias on mesa.PlanMateriaId equals planMateria.Id
                join materia in consulta.Materias on planMateria.MateriaId equals materia.Id
                where materia.Nombre == "Programación II" && mesa.Turno == Turno
                orderby mesa.Llamado
                select mesa)
            .ToListAsync();
        programacion.Select(mesa => (mesa.Llamado, mesa.Fecha, mesa.Hora)).Should().Equal(
            (1, new DateOnly(2026, 12, 3), new TimeOnly(13, 30)),
            (2, new DateOnly(2026, 12, 17), new TimeOnly(13, 30)));
        programacion[0].Tribunal.Should().Equal("Ramirez, RA", "Alesso, M", "Sandoval, P");
    }

    [Fact]
    public async Task No_carga_mesas_cuya_fecha_ya_paso()
    {
        await using var db = baseDeDatos.CrearContexto();

        var resultado = await MesasInstitucionalesSeeder.SembrarAsync(db, new DateTimeOffset(2026, 12, 20, 12, 0, 0, TimeSpan.Zero));

        resultado.MesasCreadas.Should().BeGreaterThan(0).And.BeLessThan(516);
        resultado.Advertencias.Should().OnlyContain(advertencia => advertencia.Contains("ya pasó", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CALVO M – PEDRAZZOLI,F-MIÑOZ,A", new[] { "Calvo, M", "Pedrazzoli, F", "Miñoz, A" })]
    [InlineData("GAIDO J.P –DOLDAN,I- RAMIREZ,R.A.", new[] { "Gaido, JP", "Doldan, I", "Ramirez, RA" })]
    [InlineData("CAINERO,G (TREGNAGHI,S)- DELLA ROSA,M-ZALAZAR,E", new[] { "Cainero, G (Tregnaghi, S)", "Della Rosa, M", "Zalazar, E" })]
    [InlineData("FERRERO MARCELA – MAGIOLO,W- CHAUDERON,L", new[] { "Ferrero Marcela", "Magiolo, W", "Chauderon, L" })]
    [InlineData("PEREYRA,S- VILLA, MF-LOVINO,MF-", new[] { "Pereyra, S", "Villa, MF", "Lovino, MF" })]
    [InlineData("SARA,J- SAGER –MARTINEZ,G", new[] { "Sara, J", "Sager", "Martinez, G" })]
    public void Normaliza_los_nombres_del_tribunal(string publicado, string[] esperado) =>
        MesasInstitucionalesSeeder.Tribunal(publicado).Should().Equal(esperado);

    [Theory]
    [InlineData("COMUNICACIÓN (PRIMER CUATRIMESTRE)", "COMUNICACION")]
    [InlineData("Ética y Responsabilidad Social", "ETICA Y RESPONSABILIDAD SOCIAL")]
    [InlineData("SUJETO,CULTURA Y SOCIEDAD II", "SUJETO CULTURA Y SOCIEDAD II")]
    public void Normaliza_los_nombres_de_materia_para_cruzarlos(string nombre, string esperado) =>
        MesasInstitucionalesSeeder.Normalizar(nombre).Should().Be(esperado);
}
