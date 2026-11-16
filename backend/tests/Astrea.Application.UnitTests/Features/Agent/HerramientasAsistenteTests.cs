using System.Text.Json;
using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Academic;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Exams;
using Astrea.Application.Features.LostAndFound;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Academic;
using Astrea.Domain.Exams;
using Astrea.Domain.LostAndFound;
using Astrea.Domain.Procedures;
using AwesomeAssertions;
using NSubstitute;
using static Astrea.Application.UnitTests.Features.Agent.EntornoAsistente;

namespace Astrea.Application.UnitTests.Features.Agent;

public class HerramientasAsistenteTests
{
    private readonly EntornoAsistente _entorno = new();

    private ContextoHerramientas Contexto() => new(_entorno.Estudiante);

    private async Task<JsonElement> EjecutarAsync(string nombre, object argumentos, ContextoHerramientas? contexto = null) =>
        JsonSerializer.SerializeToElement(await _entorno.Herramientas().EjecutarAsync(Llamada(nombre, argumentos), contexto ?? Contexto(), CancellationToken.None));

    private static string? Error(JsonElement resultado) => resultado.TryGetProperty("error", out var error) ? error.GetString() : null;

    [Fact]
    public async Task Las_definiciones_tienen_solo_herramientas_de_lectura_proponer_y_responder()
    {
        var definiciones = await _entorno.Herramientas().DefinicionesAsync(CancellationToken.None);

        definiciones.Select(definicion => definicion.Nombre).Should().BeEquivalentTo(
            "buscar_correlatividades", "buscar_mesas", "buscar_hallazgos", "consultar_mis_tramites", "proponer_tramite", "responder");
        var proponer = definiciones.Single(definicion => definicion.Nombre == "proponer_tramite");
        proponer.Parametros.Single(parametro => parametro.Nombre == "tipo").Valores.Should().Contain("ALUMNO_REGULAR");
        proponer.Descripcion.Should().Contain("NO crea el trámite");
        definiciones.Single(definicion => definicion.Nombre == "buscar_hallazgos").Parametros.Single(p => p.Nombre == "categoria").Valores.Should().Contain("Electronica");
    }

    [Theory]
    [InlineData("aprobar_tramite")]
    [InlineData("rechazar_tramite")]
    [InlineData("entregar_hallazgo")]
    [InlineData("responder")]
    [InlineData("SELECT * FROM usuarios")]
    public async Task Rechaza_las_herramientas_que_no_existen_o_no_estan_permitidas(string nombre)
    {
        var resultado = await EjecutarAsync(nombre, new { numero = "T-000001" });

        Error(resultado).Should().Contain("no existe o no está permitida");
        _entorno.Tramites.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Un_usuario_sin_el_rol_de_estudiante_no_puede_usarlas()
    {
        var resultado = await EjecutarAsync(HerramientasAsistente.BuscarMesas, new { }, new ContextoHerramientas(SinRoles()));

        Error(resultado).Should().Contain("no tiene permiso");
        await _entorno.Mesas.DidNotReceiveWithAnyArgs().BuscarAsync(default!, default!, default, default!, default);
    }

    [Theory]
    [InlineData("""[]""", "tienen que ser un objeto")]
    [InlineData("""{"materia": 5}""", "tiene que ser texto")]
    [InlineData("""{"desde": "15/11/2026"}""", "AAAA-MM-DD")]
    public async Task Valida_los_argumentos_antes_de_ejecutar(string argumentos, string error)
    {
        var resultado = JsonSerializer.SerializeToElement(await _entorno.Herramientas().EjecutarAsync(
            new LlamadaHerramienta(HerramientasAsistente.BuscarMesas, JsonDocument.Parse(argumentos).RootElement), Contexto(), CancellationToken.None));

        Error(resultado).Should().Contain(error);
        await _entorno.Mesas.DidNotReceiveWithAnyArgs().BuscarAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Rechaza_textos_demasiado_largos_y_categorias_inexistentes()
    {
        Error(await EjecutarAsync(HerramientasAsistente.BuscarHallazgos, new { texto = new string('a', 101) })).Should().Contain("supera los 100");
        Error(await EjecutarAsync(HerramientasAsistente.BuscarHallazgos, new { categoria = "Joyas" })).Should().Contain("no es un valor válido");
        Error(await EjecutarAsync(HerramientasAsistente.BuscarHallazgos, new { categoria = "7" })).Should().Contain("no es un valor válido");
    }

    [Fact]
    public async Task Buscar_mesas_filtra_por_materia_y_carrera_y_registra_el_dato_citable()
    {
        var mesa = new MesaExamenDto(Guid.NewGuid(), EstadoMesa.Publicada, "Diciembre", 1, new DateOnly(2026, 12, 10), new TimeOnly(9, 0), Ahora, "Aula 4", ["Pérez", "Gómez"],
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 8), EstadoInscripcion.Abierta, null, null, Guid.NewGuid(), Matematica.Id, "PM", Matematica.Nombre, Guid.NewGuid(), "M1", "Didáctica General", 1);
        _entorno.Mesas.BuscarAsync(default!, default!, default, default!, default).ReturnsForAnyArgs(PagedResult<MesaExamenDto>.From([mesa], PageRequest.Create(1, 8), 1));
        var contexto = Contexto();

        var resultado = await EjecutarAsync(HerramientasAsistente.BuscarMesas, new { materia = "Didáctica", carrera = "matematica" }, contexto);

        resultado.GetProperty("id").GetString().Should().Be("D1");
        resultado.GetProperty("mesas")[0].GetProperty("fecha").GetString().Should().Be("jueves 10/12/2026");
        resultado.GetProperty("mesas")[0].GetProperty("inscripcion").GetString().Should().Be("abierta hasta el 8/12");
        await _entorno.Mesas.Received(1).BuscarAsync(
            Arg.Is<FiltroMesas>(filtro => filtro.Texto == "Didáctica" && filtro.CarreraId == Matematica.Id),
            Arg.Is<EstadoMesa[]>(estados => !estados.Contains(EstadoMesa.Borrador)),
            Arg.Any<DateOnly>(),
            Arg.Any<PageRequest>(),
            Arg.Any<CancellationToken>());
        contexto.Datos.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = "D1", Titulo = "Mesas de examen", Enlace = "/mesas" });
        contexto.Datos[0].Resumen.Should().Contain("Didáctica General: jueves 10/12/2026 09:00");
    }

    [Fact]
    public async Task Una_carrera_inexistente_o_ambigua_vuelve_como_error_para_preguntar()
    {
        Error(await EjecutarAsync(HerramientasAsistente.BuscarMesas, new { carrera = "Medicina" })).Should().Contain("No existe la carrera").And.Contain("Profesorado de Historia");
        Error(await EjecutarAsync(HerramientasAsistente.BuscarMesas, new { carrera = "Profesorado" })).Should().Contain("coincide con varias carreras");
    }

    [Fact]
    public async Task Buscar_hallazgos_devuelve_solo_datos_publicos()
    {
        var objeto = ObjetoPerdido.Publicar(Guid.NewGuid(), Guid.NewGuid(),
            new DatosHallazgo("Cartuchera azul", "Tiene un llavero de Boca y un lápiz roto", CategoriaObjeto.Utiles, "Aula 3", FranjaHoraria.Tarde, PuntoResguardo.Secretaria, new DateOnly(2026, 11, 14)),
            new DateOnly(2026, 11, 15), Ahora);
        _entorno.Objetos.BuscarAsync(default!, default!, default).ReturnsForAnyArgs(PagedResult<ObjetoPerdido>.From([objeto], PageRequest.Create(1, 8), 1));

        var resultado = await EjecutarAsync(HerramientasAsistente.BuscarHallazgos, new { texto = "cartuchera" });

        resultado.GetRawText().Should().Contain("Cartuchera azul").And.NotContain("llavero").And.NotContain("Boca");
        resultado.GetProperty("aviso").GetString().Should().Contain("demostrar");
        await _entorno.Objetos.Received(1).BuscarAsync(Arg.Is<FiltroHallazgos>(filtro => filtro.SoloConFoto && filtro.Estados!.SequenceEqual(new[] { EstadoObjeto.Publicado })), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mis_tramites_consulta_siempre_los_del_usuario_de_la_sesion()
    {
        _entorno.Tramites.BuscarAsync(default!, default!, default).ReturnsForAnyArgs(PagedResult<TramiteResumenDto>.From(
            [new TramiteResumenDto(Guid.NewGuid(), "T-000012", Guid.NewGuid(), "Constancia de alumno regular", EstadoTramite.EnProceso, Ahora, Ahora, "e@astrea.test", null, null)],
            PageRequest.Create(1, 10),
            1));

        var resultado = await EjecutarAsync(HerramientasAsistente.ConsultarMisTramites, new { usuarioId = Guid.NewGuid(), numero = "t-000012" });

        resultado.GetProperty("tramites")[0].GetProperty("estado").GetString().Should().Be("En proceso");
        await _entorno.Tramites.Received(1).BuscarAsync(Arg.Is<FiltroTramites>(filtro => filtro.UsuarioId == _entorno.Estudiante.Id), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Correlatividades_busca_la_materia_por_nombre_y_distingue_regular_de_aprobada()
    {
        var materia = new MateriaEncontradaDto(Guid.NewGuid(), Guid.NewGuid(), "M2", "Didáctica Específica", 2, Matematica.Id, Matematica.Nombre, "Plan 2024");
        _entorno.Academico.BuscarMateriasAsync(default!, default, default, default).ReturnsForAnyArgs([materia]);
        _entorno.Academico.ObtenerCorrelatividadesAsync(materia.PlanId, materia.MateriaId, false, Arg.Any<CancellationToken>()).Returns(new CorrelatividadesDeMateriaDto(
            materia.PlanId, Guid.NewGuid(), materia.MateriaId, "M2", "Didáctica Específica", 2,
            [new RequisitoDto(Guid.NewGuid(), CondicionRequisito.Regularizada, null, Guid.NewGuid(), "M1", "Didáctica General", 1, null)],
            [new RequisitoDto(Guid.NewGuid(), CondicionRequisito.Aprobada, null, Guid.NewGuid(), "M1", "Didáctica General", 1, null)]));

        var resultado = await EjecutarAsync(HerramientasAsistente.BuscarCorrelatividades, new { materia = "didactica especifica" });

        resultado.GetProperty("paraCursar")[0].GetString().Should().Be("Didáctica General regularizada");
        resultado.GetProperty("paraRendir")[0].GetString().Should().Be("Didáctica General aprobada");
        resultado.GetProperty("nota").GetString().Should().Contain("condiciones distintas");
    }

    [Fact]
    public async Task Si_varias_materias_coinciden_pide_aclarar_cual()
    {
        _entorno.Academico.BuscarMateriasAsync(default!, default, default, default).ReturnsForAnyArgs([
            new MateriaEncontradaDto(Guid.NewGuid(), Guid.NewGuid(), "M1", "Didáctica General", 1, Matematica.Id, Matematica.Nombre, "Plan 2024"),
            new MateriaEncontradaDto(Guid.NewGuid(), Guid.NewGuid(), "H1", "Didáctica General", 1, Historia.Id, Historia.Nombre, "Plan 2023"),
        ]);

        var resultado = await EjecutarAsync(HerramientasAsistente.BuscarCorrelatividades, new { materia = "Didáctica General" });

        resultado.GetProperty("opciones").EnumerateArray().Select(opcion => opcion.GetString()).Should()
            .Equal("Didáctica General (Profesorado de Matemática, 1° año)", "Didáctica General (Profesorado de Historia, 1° año)");
        await _entorno.Academico.DidNotReceiveWithAnyArgs().ObtenerCorrelatividadesAsync(default, default, default, default);
    }

    [Fact]
    public async Task Proponer_tramite_valida_con_las_reglas_reales_pero_no_crea_nada()
    {
        var contexto = Contexto();

        var resultado = await EjecutarAsync(
            HerramientasAsistente.ProponerTramite,
            new { tipo = "ALUMNO_REGULAR", carrera = "Profesorado de Matemática", datos = new[] { new { clave = "presentarAnte", valor = "ANSES" } } },
            contexto);

        resultado.GetProperty("aviso").GetString().Should().Contain("TODAVÍA NO SE CREÓ");
        contexto.Propuesta.Should().NotBeNull();
        contexto.Propuesta!.TipoId.Should().Be(CatalogoTramites.AlumnoRegularId);
        contexto.Propuesta.Resumen.Select(dato => $"{dato.Etiqueta}: {dato.Valor}").Should().Equal("Carrera: Profesorado de Matemática", "Para presentar ante: ANSES");
        contexto.Propuesta.Datos.Should().Contain("presentarAnte", "ANSES");
        _entorno.Tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);
        await _entorno.UnitOfWork.DidNotReceiveWithAnyArgs().GuardarCambiosAsync(default);
    }

    [Fact]
    public async Task Proponer_tramite_con_datos_faltantes_o_tipo_inexistente_vuelve_como_error()
    {
        var contexto = Contexto();

        var faltaDato = await EjecutarAsync(HerramientasAsistente.ProponerTramite, new { tipo = "ALUMNO_REGULAR", carrera = "Historia" }, contexto);
        var tipoInexistente = await EjecutarAsync(HerramientasAsistente.ProponerTramite, new { tipo = "TITULO_FINAL", carrera = "Historia" }, contexto);

        Error(faltaDato).Should().Contain("Faltan datos").And.Contain("Preguntáselos");
        Error(tipoInexistente).Should().Contain("No existe el tipo");
        contexto.Propuesta.Should().BeNull();
    }
}
