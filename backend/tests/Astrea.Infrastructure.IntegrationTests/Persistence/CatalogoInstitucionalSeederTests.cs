using Astrea.Application.Contracts.Pagination;
using Astrea.Domain.Academic;
using Astrea.Infrastructure.Persistence;
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
            var resultado = await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
            resultado.CarrerasAgregadas.Should().Be(9);
            resultado.PlanesConCorrelatividades.Should().Be(9);
            resultado.Advertencias.Should().BeEmpty();
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
    public async Task Volver_a_sembrar_no_duplica_ni_pisa_lo_que_edito_Secretaria()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
            var tsds = await db.Carreras.SingleAsync(carrera => carrera.Codigo == "TSDS");
            tsds.Actualizar("Nombre editado por Secretaría", 3, true, Ahora);
            await db.SaveChangesAsync();
        }

        await using (var db = baseDeDatos.CrearContexto())
        {
            var resultado = await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
            resultado.CarrerasAgregadas.Should().Be(0);
            resultado.PlanesConCorrelatividades.Should().Be(0);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        (await consulta.Carreras.CountAsync()).Should().Be(9);
        (await consulta.Carreras.SingleAsync(carrera => carrera.Codigo == "TSDS")).Nombre.Should().Be("Nombre editado por Secretaría");
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

    [Fact]
    public async Task Carga_las_correlatividades_oficiales_diferenciando_cursar_y_rendir()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        var repositorio = new EfAcademicRepository(consulta);
        var (planId, materias) = await MateriasDeAsync(consulta, "PEA");

        var administracionIII = await repositorio.ObtenerCorrelatividadesAsync(planId, materias["Administración III"], incluirInactivas: false, CancellationToken.None);

        administracionIII.Should().NotBeNull();
        administracionIII!.ParaCursar.Where(r => r.Condicion == CondicionRequisito.Regularizada).Select(r => r.Nombre)
            .Should().BeEquivalentTo("Administración II", "Sistema de Información Contable II");
        administracionIII.ParaCursar.Where(r => r.Condicion == CondicionRequisito.Aprobada).Select(r => r.Nombre)
            .Should().BeEquivalentTo("Administración General", "Administración I", "Sistema de Información Contable I");
        administracionIII.ParaRendir.Select(r => (r.Nombre, r.Condicion)).Should().BeEquivalentTo(new[]
        {
            ("Sistema de Información Contable II", CondicionRequisito.Regularizada),
            ("Estadística Aplicada", CondicionRequisito.Regularizada),
            ("Administración II", CondicionRequisito.Aprobada),
        });
        administracionIII.ParaCursar.Select(r => r.Anio).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Carga_alternativas_y_requisitos_en_texto()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        var (planId, materias) = await MateriasDeAsync(consulta, "PET");

        var practicaIII = await new EfAcademicRepository(consulta)
            .ObtenerCorrelatividadesAsync(planId, materias["Práctica Docente III: La Clase"], incluirInactivas: false, CancellationToken.None);

        var alternativas = practicaIII!.ParaCursar.Where(r => r.GrupoAlternativa is not null).ToList();
        alternativas.Select(r => r.Nombre).Should().BeEquivalentTo("Procesos Productivos", "Diseño y Producción Tecnológica II");
        alternativas.Select(r => r.GrupoAlternativa).Distinct().Should().ContainSingle();
        practicaIII.ParaCursar.Should().ContainSingle(r => r.MateriaId == null)
            .Which.Descripcion.Should().Contain("primer año");
        practicaIII.ParaRendir.Should().BeEmpty();
    }

    [Fact]
    public async Task No_vuelve_a_cargar_correlatividades_de_un_plan_que_ya_tiene()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
        }

        int cantidad;
        await using (var db = baseDeDatos.CrearContexto())
        {
            cantidad = await db.Correlatividades.CountAsync();
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
        }

        await using var consulta = baseDeDatos.CrearContexto();
        cantidad.Should().BeGreaterThan(400);
        (await consulta.Correlatividades.CountAsync()).Should().Be(cantidad);
    }

    [Fact]
    public async Task Si_Secretaria_renombro_una_materia_avisa_y_no_carga_ese_plan()
    {
        await using (var db = baseDeDatos.CrearContexto())
        {
            await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);
            await db.Correlatividades.ExecuteDeleteAsync();
            var programacion = await db.Materias.SingleAsync(materia => materia.Nombre == "Programación I");
            programacion.Actualizar("Programación Inicial", activa: true);
            await db.SaveChangesAsync();
        }

        await using (var db = baseDeDatos.CrearContexto())
        {
            var resultado = await CatalogoInstitucionalSeeder.SembrarAsync(db, Ahora);

            resultado.PlanesConCorrelatividades.Should().Be(8);
            resultado.Advertencias.Should().ContainSingle().Which.Should().Contain("TSDS").And.Contain("Programación I");
        }
    }

    private static async Task<(Guid PlanId, Dictionary<string, Guid> Materias)> MateriasDeAsync(AppDbContext db, string codigoCarrera)
    {
        var carrera = await db.Carreras.SingleAsync(carrera => carrera.Codigo == codigoCarrera);
        var plan = await db.PlanesEstudio.SingleAsync(plan => plan.CarreraId == carrera.Id);
        var materias = await (
            from planMateria in db.PlanMaterias
            join materia in db.Materias on planMateria.MateriaId equals materia.Id
            where planMateria.PlanEstudioId == plan.Id
            select new { materia.Nombre, materia.Id })
            .ToListAsync();

        return (plan.Id, materias.GroupBy(m => m.Nombre).ToDictionary(g => g.Key, g => g.First().Id));
    }
}
