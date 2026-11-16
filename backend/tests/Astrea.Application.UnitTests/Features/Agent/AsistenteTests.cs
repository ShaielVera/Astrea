using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Exams;
using Astrea.Application.Features.Knowledge;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Agent;
using Astrea.Domain.Exams;
using Astrea.Domain.Knowledge;
using Astrea.Domain.Procedures;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Astrea.Application.UnitTests.Features.Agent.EntornoAsistente;

namespace Astrea.Application.UnitTests.Features.Agent;

public class AsistenteTests
{
    private readonly EntornoAsistente _entorno = new();

    private void ConEvidencia(double similitud = 0.8) =>
        _entorno.Documentos.BuscarSimilaresAsync(default!, default, default).ReturnsForAnyArgs([
            new FragmentoEncontrado(Guid.NewGuid(), 1, "La inscripción a mesas cierra 48 horas antes.", new MetadataFragmento("Reglamento", TipoDocumentoRag.Reglamento, 1, "CAPÍTULO I", null, null), 1, 1, similitud),
        ]);

    private Task<Result<IntercambioDto>> PreguntarAsync(string texto, Guid? conversacionId = null) =>
        _entorno.Asistente().PreguntarAsync(new PreguntaCommand(conversacionId, texto), CancellationToken.None);

    private Conversacion ConversacionPropia()
    {
        var conversacion = Conversacion.Iniciar(Guid.NewGuid(), _entorno.Estudiante.Id, "Primera", Ahora);
        _entorno.Conversaciones.ObtenerAsync(conversacion.Id, Arg.Any<CancellationToken>()).Returns(conversacion);
        return conversacion;
    }

    [Fact]
    public async Task Responde_con_fuentes_documentales_y_guarda_la_conversacion_nueva()
    {
        ConEvidencia();
        _entorno.ModeloPropone(Responder("Cierra 48 horas antes [F1].", ["F1"]));

        var resultado = await PreguntarAsync("¿Cuándo cierra la inscripción a mesas?");

        var intercambio = resultado.Valor;
        intercambio.Respuesta.Contenido.Should().Be("Cierra 48 horas antes [1].");
        intercambio.Respuesta.Fuentes.Should().ContainSingle().Which.Titulo.Should().Be("Reglamento");
        _entorno.Conversaciones.Received(1).Agregar(Arg.Is<Conversacion>(conversacion => conversacion.Id == intercambio.ConversacionId && conversacion.UsuarioId == _entorno.Estudiante.Id));
        await _entorno.UnitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        await _entorno.Modelo.Received(1).SiguientePasoAsync(
            Arg.Is<ConsultaAgente>(consulta => consulta.Instrucciones == PoliticaRag.Instrucciones
                && consulta.Herramientas.Any(herramienta => herramienta.Nombre == "buscar_mesas")
                && ((TurnoUsuario)consulta.Turnos[consulta.Turnos.Count - 1]).Texto.Contains("<fragmento id=\"F1\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Usa_una_herramienta_y_responde_citando_los_datos_actuales()
    {
        var mesa = new MesaExamenDto(Guid.NewGuid(), EstadoMesa.Publicada, "Diciembre", 1, new DateOnly(2026, 12, 10), new TimeOnly(9, 0), Ahora, null, [],
            null, null, EstadoInscripcion.SinFechas, null, null, Guid.NewGuid(), Guid.NewGuid(), "PM", "Profesorado de Matemática", Guid.NewGuid(), "M1", "Didáctica General", 1);
        _entorno.Mesas.BuscarAsync(default!, default!, default, default!, default).ReturnsForAnyArgs(PagedResult<MesaExamenDto>.From([mesa], PageRequest.Create(1, 8), 1));
        _entorno.ModeloPropone(
            Llamada("buscar_mesas", new { materia = "Didáctica General" }),
            Responder("La mesa de Didáctica General es el jueves 10/12 a las 9 [D1].", ["D1"]));

        var resultado = await PreguntarAsync("¿Cuándo es la mesa de Didáctica General?");

        var respuesta = resultado.Valor.Respuesta;
        respuesta.Contenido.Should().Be("La mesa de Didáctica General es el jueves 10/12 a las 9 [1].");
        respuesta.Fuentes.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Tipo = TipoFuente.DatosDeAstrea, Titulo = "Mesas de examen", Enlace = "/mesas" });

        // En el segundo paso el modelo recibe su llamada y el resultado de la herramienta.
        await _entorno.Modelo.Received(1).SiguientePasoAsync(
            Arg.Is<ConsultaAgente>(consulta => consulta.Turnos[consulta.Turnos.Count - 2] is TurnoLlamadas && consulta.Turnos[consulta.Turnos.Count - 1] is TurnoResultados),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_herramienta_prohibida_no_se_ejecuta_y_el_asistente_se_abstiene()
    {
        _entorno.ModeloPropone(
            Llamada("aprobar_tramite", new { numero = "T-000001" }),
            Responder("Listo, aprobé tu trámite.", []));

        var resultado = await PreguntarAsync("Aprobá mi trámite T-000001");

        resultado.Valor.Respuesta.SinEvidencia.Should().BeTrue();
        _entorno.Tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);
        await _entorno.Tramites.DidNotReceiveWithAnyArgs().ObtenerAsync(default, default);
    }

    [Fact]
    public async Task Sin_evidencia_ni_datos_la_respuesta_no_se_acepta_y_deriva_a_secretaria()
    {
        _entorno.ModeloPropone(Responder("Ganó Argentina [F1].", ["F1"]));

        var resultado = await PreguntarAsync("¿Quién ganó el mundial?");

        resultado.Valor.Respuesta.SinEvidencia.Should().BeTrue();
        resultado.Valor.Respuesta.Contenido.Should().Be(PoliticaRag.RespuestaSinEvidencia);
    }

    [Fact]
    public async Task Si_el_modelo_no_termina_en_pocos_pasos_se_abstiene()
    {
        _entorno.Modelo.SiguientePasoAsync(default!, default).ReturnsForAnyArgs(new TurnoLlamadas([Llamada("buscar_hallazgos", new { })]));
        _entorno.Objetos.BuscarAsync(default!, default!, default).ReturnsForAnyArgs(PagedResult<Domain.LostAndFound.ObjetoPerdido>.From([], PageRequest.Create(1, 8), 0));

        var resultado = await PreguntarAsync("¿Encontraron algo?");

        resultado.Valor.Respuesta.SinEvidencia.Should().BeTrue();
        await _entorno.Modelo.ReceivedWithAnyArgs(Asistente.PasosMaximos).SiguientePasoAsync(default!, default);
    }

    [Fact]
    public async Task Ejecuta_como_maximo_tres_herramientas_por_paso_pero_responde_a_todas()
    {
        _entorno.Objetos.BuscarAsync(default!, default!, default).ReturnsForAnyArgs(PagedResult<Domain.LostAndFound.ObjetoPerdido>.From([], PageRequest.Create(1, 8), 0));
        var cinco = Enumerable.Range(0, 5).Select(_ => Llamada("buscar_hallazgos", new { })).ToList();
        _entorno.Modelo.SiguientePasoAsync(default!, default).ReturnsForAnyArgs(new TurnoLlamadas(cinco), new TurnoLlamadas([Responder("No hay [D1].", ["D1"])]));

        await PreguntarAsync("¿Encontraron algo?");

        await _entorno.Objetos.ReceivedWithAnyArgs(Asistente.LlamadasPorPaso).BuscarAsync(default!, default!, default);
        await _entorno.Modelo.Received(1).SiguientePasoAsync(Arg.Is<ConsultaAgente>(consulta => consulta.Turnos[consulta.Turnos.Count - 1] is TurnoResultados && ((TurnoResultados)consulta.Turnos[consulta.Turnos.Count - 1]).Resultados.Count == 5), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Puede_pedir_un_dato_que_falta_con_una_aclaracion()
    {
        _entorno.ModeloPropone(Responder("¿De qué carrera sos?", [], esAclaracion: true));

        var resultado = await PreguntarAsync("Quiero una constancia de alumno regular");

        resultado.Valor.Respuesta.EsAclaracion.Should().BeTrue();
        resultado.Valor.Respuesta.Contenido.Should().Be("¿De qué carrera sos?");
    }

    [Fact]
    public async Task Propone_un_tramite_sin_crearlo_y_al_confirmar_se_crea_uno_solo()
    {
        _entorno.ModeloPropone(
            Llamada("proponer_tramite", new { tipo = "ALUMNO_REGULAR", carrera = "Matemática", datos = new[] { new { clave = "presentarAnte", valor = "ANSES" } } }),
            Responder("Te preparé la solicitud. Confirmala con el botón.", []));
        var conversacion = ConversacionPropia();

        var intercambio = (await PreguntarAsync("Quiero una constancia de alumno regular para ANSES", conversacion.Id)).Valor;

        var propuesta = intercambio.Respuesta.Propuesta!;
        propuesta.Estado.Should().Be(EstadoPropuesta.Pendiente);
        propuesta.TipoNombre.Should().Be("Constancia de alumno regular");
        propuesta.VenceEl.Should().Be(Ahora.AddHours(Conversacion.HorasDeVigenciaPropuesta));
        _entorno.Tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);

        // Confirmar: se crea el trámite a nombre del usuario de la sesión, con el id del mensaje como clave de idempotencia.
        Tramite? creado = null;
        _entorno.Tramites.When(repositorio => repositorio.Agregar(Arg.Any<Tramite>())).Do(llamada => creado = llamada.Arg<Tramite>());
        _entorno.Tramites.ObtenerPersonasAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, PersonaDto> { [_entorno.Estudiante.Id] = new(_entorno.Estudiante.Id, _entorno.Estudiante.Email, null, null, null) });
        _entorno.Academico.ObtenerCarreraAsync(default, default).ReturnsForAnyArgs(Domain.Academic.Carrera.Crear(Guid.NewGuid(), "PM", "Profesorado de Matemática", 4, Ahora));

        var confirmado = await _entorno.Asistente().ConfirmarPropuestaAsync(conversacion.Id, intercambio.Respuesta.Id, CancellationToken.None);

        confirmado.EsExitoso.Should().BeTrue(confirmado.Error?.Mensaje);
        creado!.UsuarioId.Should().Be(_entorno.Estudiante.Id);
        creado.ClaveIdempotencia.Should().Be(intercambio.Respuesta.Id);
        confirmado.Valor.Propuesta!.Should().BeEquivalentTo(new { Estado = EstadoPropuesta.Confirmada, TramiteId = creado.Id });
        confirmado.Valor.Propuesta.NumeroTramite.Should().StartWith("T-");

        // Confirmar otra vez no crea otro trámite.
        var otraVez = await _entorno.Asistente().ConfirmarPropuestaAsync(conversacion.Id, intercambio.Respuesta.Id, CancellationToken.None);
        otraVez.Valor.Propuesta!.TramiteId.Should().Be(creado.Id);
        _entorno.Tramites.ReceivedWithAnyArgs(1).Agregar(default(Tramite)!);
    }

    [Fact]
    public async Task Una_propuesta_cancelada_ya_no_se_puede_confirmar()
    {
        _entorno.ModeloPropone(
            Llamada("proponer_tramite", new { tipo = "ALUMNO_REGULAR", carrera = "Historia", datos = new[] { new { clave = "presentarAnte", valor = "ANSES" } } }),
            Responder("Te preparé la solicitud.", []));
        var conversacion = ConversacionPropia();
        var mensaje = (await PreguntarAsync("Quiero una constancia", conversacion.Id)).Valor.Respuesta;

        var cancelada = await _entorno.Asistente().CancelarPropuestaAsync(conversacion.Id, mensaje.Id, CancellationToken.None);
        var confirmar = await _entorno.Asistente().ConfirmarPropuestaAsync(conversacion.Id, mensaje.Id, CancellationToken.None);

        cancelada.Valor.Propuesta!.Estado.Should().Be(EstadoPropuesta.Cancelada);
        confirmar.Error!.Tipo.Should().Be(TipoError.Conflicto);
        _entorno.Tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);
    }

    [Fact]
    public async Task No_se_puede_confirmar_la_propuesta_de_otra_persona()
    {
        var ajena = Conversacion.Iniciar(Guid.NewGuid(), Guid.NewGuid(), "Pregunta ajena", Ahora);
        var (_, mensaje) = ajena.Registrar("x", "Te preparé la solicitud.", [], TipoRespuesta.Respuesta, Ahora,
            new PropuestaTramite(CatalogoTramites.AlumnoRegularId, "Constancia", [], []));
        _entorno.Conversaciones.ObtenerAsync(ajena.Id, Arg.Any<CancellationToken>()).Returns(ajena);

        var resultado = await _entorno.Asistente().ConfirmarPropuestaAsync(ajena.Id, mensaje.Id, CancellationToken.None);

        resultado.Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        _entorno.Tramites.DidNotReceiveWithAnyArgs().Agregar(default(Tramite)!);
    }

    [Fact]
    public async Task Oculta_el_dni_antes_de_guardar_y_de_enviarlo_a_la_ia()
    {
        ConEvidencia();
        _entorno.ModeloPropone(Responder("Cierra 48 horas antes [F1].", ["F1"]));

        var resultado = await PreguntarAsync("Soy DNI 35123456, ¿cuándo cierra?");

        resultado.Valor.Pregunta.Contenido.Should().Be("Soy DNI [DNI], ¿cuándo cierra?");
        resultado.Valor.Titulo.Should().NotContain("35123456");
        await _entorno.Embeddings.Received(1).GenerarAsync(Arg.Is<IReadOnlyList<string>>(textos => !textos.Single().Contains("35123456")), PropositoEmbedding.Consulta, Arg.Any<CancellationToken>());
        await _entorno.Modelo.Received(1).SiguientePasoAsync(Arg.Is<ConsultaAgente>(consulta => consulta.Turnos.OfType<TurnoUsuario>().All(turno => !turno.Texto.Contains("35123456"))), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_repregunta_usa_el_historial_y_busca_junto_con_la_pregunta_anterior()
    {
        ConEvidencia();
        _entorno.ModeloPropone(Responder("También [F1].", ["F1"]));
        var conversacion = Conversacion.Iniciar(Guid.NewGuid(), _entorno.Estudiante.Id, "¿Cuándo cierra la inscripción a mesas?", Ahora);
        conversacion.Registrar("¿Cuándo cierra la inscripción a mesas?", "Cierra 48 horas antes [1].", [new FuenteRespuesta(Guid.NewGuid(), "R", 1, null, null, 1, 1, "x")], TipoRespuesta.Respuesta, Ahora);
        _entorno.Conversaciones.ObtenerAsync(conversacion.Id, Arg.Any<CancellationToken>()).Returns(conversacion);

        var resultado = await PreguntarAsync("¿Y para diciembre?", conversacion.Id);

        resultado.EsExitoso.Should().BeTrue();
        conversacion.Mensajes.Should().HaveCount(4);
        _entorno.Conversaciones.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _entorno.Embeddings.Received(1).GenerarAsync(
            Arg.Is<IReadOnlyList<string>>(textos => textos.Single() == "¿Cuándo cierra la inscripción a mesas?\n¿Y para diciembre?"),
            PropositoEmbedding.Consulta,
            Arg.Any<CancellationToken>());
        await _entorno.Modelo.Received(1).SiguientePasoAsync(
            Arg.Is<ConsultaAgente>(consulta => consulta.Turnos.Count == 3 && consulta.Turnos[0] is TurnoUsuario && consulta.Turnos[1] is TurnoModelo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_deja_usar_ni_ver_la_conversacion_de_otra_persona()
    {
        var ajena = Conversacion.Iniciar(Guid.NewGuid(), Guid.NewGuid(), "Pregunta ajena", Ahora);
        _entorno.Conversaciones.ObtenerAsync(ajena.Id, Arg.Any<CancellationToken>()).Returns(ajena);

        (await PreguntarAsync("Hola", ajena.Id)).Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        (await _entorno.Asistente().ObtenerAsync(ajena.Id, CancellationToken.None)).Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        (await _entorno.Asistente().EliminarAsync(ajena.Id, CancellationToken.None)).Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        _entorno.Conversaciones.DidNotReceiveWithAnyArgs().Eliminar(default!);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Una_pregunta_vacia_no_se_procesa(string? texto)
    {
        (await PreguntarAsync(texto!)).Error.Should().Be(ErroresAsistente.PreguntaVacia);
        await _entorno.Embeddings.DidNotReceiveWithAnyArgs().GenerarAsync(default!, default, default);
    }

    [Fact]
    public async Task Una_pregunta_demasiado_larga_se_rechaza()
    {
        (await PreguntarAsync(new string('a', Conversacion.LargoMaximoPregunta + 1))).Error.Should().Be(ErroresAsistente.PreguntaDemasiadoLarga);
    }

    [Fact]
    public async Task Si_la_ia_no_responde_devuelve_no_disponible_y_no_guarda_nada()
    {
        _entorno.Modelo.SiguientePasoAsync(default!, default).ThrowsAsyncForAnyArgs(new ModeloNoDisponibleException("Gemini no respondió"));

        var resultado = await PreguntarAsync("¿Cuándo cierra?");

        resultado.Error!.Tipo.Should().Be(TipoError.NoDisponible);
        resultado.Error.Mensaje.Should().Contain("Secretaría").And.NotContain("Gemini");
        _entorno.Conversaciones.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _entorno.UnitOfWork.DidNotReceiveWithAnyArgs().GuardarCambiosAsync(default);
    }

    [Fact]
    public async Task Si_fallan_los_embeddings_tambien_devuelve_no_disponible()
    {
        _entorno.Embeddings.GenerarAsync(default!, default, default).ThrowsAsyncForAnyArgs(new EmbeddingNoDisponibleException("sin red"));

        (await PreguntarAsync("¿Cuándo cierra?")).Error.Should().Be(ErroresAsistente.NoDisponible);
    }

    [Fact]
    public async Task Una_conversacion_llena_pide_empezar_otra()
    {
        var llena = ConversacionPropia();
        for (var i = 0; i < Conversacion.PreguntasMaximas; i++)
        {
            llena.Registrar($"P{i}", "Sin datos", [], TipoRespuesta.SinEvidencia, Ahora);
        }

        (await PreguntarAsync("Otra", llena.Id)).Error.Should().Be(ErroresAsistente.ConversacionLlena);
    }

    [Theory]
    [InlineData(EstadoIndexacion.Indexado, true)]
    [InlineData(EstadoIndexacion.Reemplazado, true)]
    [InlineData(EstadoIndexacion.Retirado, false)]
    [InlineData(EstadoIndexacion.Pendiente, false)]
    public async Task Solo_se_pueden_descargar_las_fuentes_vigentes_o_reemplazadas(EstadoIndexacion estado, bool disponible)
    {
        var documento = DocumentoEn(estado);
        _entorno.Documentos.ObtenerAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        _entorno.Archivos.LeerAsync(documento.StoragePath, Arg.Any<CancellationToken>()).Returns("%PDF-1.7"u8.ToArray());

        var resultado = await _entorno.Asistente().DescargarFuenteAsync(documento.Id, CancellationToken.None);

        resultado.EsExitoso.Should().Be(disponible);
        if (disponible)
        {
            resultado.Valor.NombreArchivo.Should().Be("Reglamento Académico (v1).pdf");
        }
    }

    private static DocumentoRag DocumentoEn(EstadoIndexacion estado)
    {
        var documento = DocumentoRag.Cargar(Guid.NewGuid(), new DatosDocumentoRag("Reglamento Académico", TipoDocumentoRag.Reglamento, null, null), "documentos-rag/2026/x.pdf", new string('a', 64), Guid.NewGuid(), Ahora);
        if (estado == EstadoIndexacion.Pendiente)
        {
            return documento;
        }

        documento.IniciarProcesamiento();
        documento.MarcarIndexado(1, 1, Ahora);
        if (estado == EstadoIndexacion.Reemplazado)
        {
            documento.MarcarReemplazado();
        }
        else if (estado == EstadoIndexacion.Retirado)
        {
            documento.Retirar(Guid.NewGuid());
        }

        return documento;
    }
}
