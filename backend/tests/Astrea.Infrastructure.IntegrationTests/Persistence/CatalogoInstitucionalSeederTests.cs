using Astrea.Application.Contracts.Pagination;
using Astrea.Domain.Academic;
using Astrea.Infrastructure.Persistence.Repositories;
using Astrea.Infrastructure.Persistence.Seed;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class CatalogoInstitucionalSeederTests(BaseDeDatosFixture baseDeDatos) : IAsyncLifetime
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await using var db = baseDeDatos.CrearContexto();
        await db.PlanesEstudio.ExecuteDeleteAsync();
        await db.Materias.ExecuteDeleteAsync();
        await db.Carreras.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Carga_las_nueve_carreras_del_instituto_con_su_plan()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            (await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora)).Should().Be(9);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        var codigos = await consulta.Carreras.Select(carrera => carrera.Codigo).ToListAsync();
        codigos.Should().BeEquivalentTo("PEI", "PEA", "PEE", "PET", "TSDS", "TSE", "TSGER", "TSGO", "TSSITI");
        (await consulta.PlanesEstudio.CountAsync()).Should().Be(9);
    }

    [Fact]
    public async Task El_plan_de_TSDS_tiene_sus_25_materias_ordenadas_por_anio()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        var tsds = await consulta.Carreras.SingleAsync(carrera => carrera.Codigo == "TSDS");
        var plan = await consulta.PlanesEstudio.SingleAsync(p => p.CarreraId == tsds.Id);
        var materias = await new EfAcademicRepository(consulta)
            .ListarMateriasDePlanAsync(plan.Id, anio: null, PageRequest.Create(1, 50), incluirInactivas: false, CancellationToken.None);

        plan.CodigoVersion.Should().Be("Res. 2120/16");
        materias.TotalItems.Should().Be(25);
        materias.Items.Select(materia => materia.Anio).Should().BeInAscendingOrder();
        materias.Items[0].Should().Match<Application.Features.Academic.MateriaDePlanDto>(m =>
            m.Nombre == "Comunicación" && m.Cuatrimestre == Cuatrimestre.Primero && m.Codigo == "TSDS-001");
        materias.Items.Should().Contain(m => m.Nombre == "Práctica Profesionalizante I" && m.Anio == 2);
    }

    [Fact]
    public async Task Volver_a_sembrar_no_duplica_ni_pisa_lo_que_edito_Bedelia()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
            var tsds = await db.Carreras.SingleAsync(carrera => carrera.Codigo == "TSDS");
            tsds.Actualizar("Nombre editado por Bedelía", 3, true, Ahora);
            await db.SaveChangesAsync();
        }

        await using (var db = baseDeDatos.CrearContexto())
        {
            (await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora)).Should().Be(0);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        (await consulta.Carreras.CountAsync()).Should().Be(9);
        (await consulta.Carreras.SingleAsync(carrera => carrera.Codigo == "TSDS")).Nombre.Should().Be("Nombre editado por Bedelía");
    }

    [Fact]
    public async Task El_listado_de_carreras_pagina_y_oculta_las_inactivas()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
            var tsge = await db.Carreras.SingleAsync(carrera => carrera.Codigo == "TSGER");
            tsge.Actualizar(tsge.Nombre, tsge.DuracionAnios, activa: false, Ahora);
            await db.SaveChangesAsync();
        }

        await using var consulta = baseDeDatos.CrearContexto();
        var repositorio = new EfAcademicRepository(consulta);

        var primeraPagina = await repositorio.ListarCarrerasAsync(PageRequest.Create(1, 5), incluirInactivas: false, CancellationToken.None);
        var segundaPagina = await repositorio.ListarCarrerasAsync(PageRequest.Create(2, 5), incluirInactivas: false, CancellationToken.None);
        var todas = await repositorio.ListarCarrerasAsync(PageRequest.Create(1, 50), incluirInactivas: true, CancellationToken.None);

        primeraPagina.TotalItems.Should().Be(8);
        primeraPagina.Items.Should().HaveCount(5);
        primeraPagina.HasNext.Should().BeTrue();
        segundaPagina.Items.Should().HaveCount(3);
        primeraPagina.Items.Concat(segundaPagina.Items).Select(c => c.Nombre).Should().BeInAscendingOrder(StringComparer.Ordinal);
        todas.TotalItems.Should().Be(9);
    }
}
