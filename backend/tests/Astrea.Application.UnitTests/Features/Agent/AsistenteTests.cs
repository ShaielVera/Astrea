using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Knowledge;
using Astrea.Domain.Agent;
using Astrea.Domain.Knowledge;
using Astrea.Domain.Users;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Astrea.Application.UnitTests.Features.Agent;

public class AsistenteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);

    private readonly IConversacionRepository _conversaciones = Substitute.For<IConversacionRepository>();
    private readonly IDocumentoRagRepository _documentos = Substitute.For<IDocumentoRagRepository>();
    private readonly IEmbeddingService _embeddings = Substitute.For<IEmbeddingService>();
    private readonly IModeloLenguaje _modelo = Substitute.For<IModeloLenguaje>();
    private readonly IFileStorage _archivos = Substitute.For<IFileStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _sesion = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly Usuario _estudiante = Usuario.Registrar(Guid.NewGuid(), "uid-estudiante", "estudiante@astrea.test", Ahora);

    public AsistenteTests()
    {
        _clock.UtcNow.Returns(Ahora);
        _sesion.FirebaseUid.Returns("uid-estudiante");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-estudiante", Arg.Any<CancellationToken>()).Returns(_estudiante);
        _embeddings.GenerarAsync(default!, default, default).ReturnsForAnyArgs([new float[FragmentoRag.DimensionesEmbedding]]);
        _documentos.BuscarSimilaresAsync(default!, default, default).ReturnsForAnyArgs([]);
        _modelo.ResponderAsync(default!, default).ReturnsForAnyArgs(new RespuestaModelo("Cierra 48 horas antes [F1].", ["F1"], false));
    }

    private Asistente Asistente() => new(
        new UsuarioActual(_sesion, _usuarios),
        _conversaciones,
        _documentos,
        _embeddings,
        _modelo,
        _archivos,
        _unitOfWork,
        _clock,
        new ConfiguracionIa { Asistente = new ConfiguracionAsistente { SimilitudMinima = 0.5 } },
        NullLogger<Asistente>.Instance);

    private void ConEvidencia(double similitud = 0.8) =>
        _documentos.BuscarSimilaresAsync(default!, default, default).ReturnsForAnyArgs([
            new FragmentoEncontrado(Guid.NewGuid(), 1, "La inscripción a mesas cierra 48 horas antes.", new MetadataFragmento("Reglamento", TipoDocumentoRag.Reglamento, 1, "CAPÍTULO I", null, null), 1, 1, similitud),
        ]);

    [Fact]
    public async Task Responde_con_fuentes_y_guarda_la_conversacion_nueva()
    {
        ConEvidencia();

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, "¿Cuándo cierra la inscripción a mesas?"), CancellationToken.None);

        var intercambio = resultado.Valor;
        intercambio.Respuesta.Contenido.Should().Be("Cierra 48 horas antes [1].");
        intercambio.Respuesta.SinEvidencia.Should().BeFalse();
        intercambio.Respuesta.Fuentes.Should().ContainSingle().Which.Titulo.Should().Be("Reglamento");
        intercambio.Titulo.Should().Be("¿Cuándo cierra la inscripción a mesas?");
        _conversaciones.Received(1).Agregar(Arg.Is<Conversacion>(conversacion => conversacion.Id == intercambio.ConversacionId && conversacion.UsuarioId == _estudiante.Id));
        await _unitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        await _modelo.Received(1).ResponderAsync(
            Arg.Is<ConsultaModelo>(consulta => consulta.Instrucciones == PoliticaRag.Instrucciones && consulta.Evidencias.Count == 1 && consulta.Mensaje.Contains("<pregunta>")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_evidencia_se_abstiene_sin_consultar_al_modelo()
    {
        ConEvidencia(similitud: 0.2);

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, "¿Quién ganó el mundial?"), CancellationToken.None);

        resultado.Valor.Respuesta.SinEvidencia.Should().BeTrue();
        resultado.Valor.Respuesta.Contenido.Should().Be(PoliticaRag.RespuestaSinEvidencia);
        resultado.Valor.Respuesta.Fuentes.Should().BeEmpty();
        await _modelo.DidNotReceiveWithAnyArgs().ResponderAsync(default!, default);
    }

    [Fact]
    public async Task Si_el_modelo_cita_una_fuente_inexistente_se_abstiene()
    {
        ConEvidencia();
        _modelo.ResponderAsync(default!, default).ReturnsForAnyArgs(new RespuestaModelo("Todo aprobado [F5].", ["F5"], false));

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, "¿Aprobé?"), CancellationToken.None);

        resultado.Valor.Respuesta.SinEvidencia.Should().BeTrue();
    }

    [Fact]
    public async Task Oculta_el_dni_antes_de_guardar_y_de_enviarlo_a_la_ia()
    {
        ConEvidencia();

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, "Soy DNI 35123456, ¿cuándo cierra?"), CancellationToken.None);

        resultado.Valor.Pregunta.Contenido.Should().Be("Soy DNI [DNI], ¿cuándo cierra?");
        resultado.Valor.Titulo.Should().NotContain("35123456");
        await _embeddings.Received(1).GenerarAsync(Arg.Is<IReadOnlyList<string>>(textos => !textos.Single().Contains("35123456")), PropositoEmbedding.Consulta, Arg.Any<CancellationToken>());
        await _modelo.Received(1).ResponderAsync(Arg.Is<ConsultaModelo>(consulta => !consulta.Mensaje.Contains("35123456")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_repregunta_usa_el_historial_y_busca_junto_con_la_pregunta_anterior()
    {
        ConEvidencia();
        var conversacion = Conversacion.Iniciar(Guid.NewGuid(), _estudiante.Id, "¿Cuándo cierra la inscripción a mesas?", Ahora);
        conversacion.Registrar("¿Cuándo cierra la inscripción a mesas?", "Cierra 48 horas antes [1].", [new FuenteRespuesta(Guid.NewGuid(), "R", 1, null, null, 1, 1, "x")], false, Ahora);
        _conversaciones.ObtenerAsync(conversacion.Id, Arg.Any<CancellationToken>()).Returns(conversacion);

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(conversacion.Id, "¿Y para diciembre?"), CancellationToken.None);

        resultado.EsExitoso.Should().BeTrue();
        conversacion.Mensajes.Should().HaveCount(4);
        _conversaciones.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _embeddings.Received(1).GenerarAsync(
            Arg.Is<IReadOnlyList<string>>(textos => textos.Single() == "¿Cuándo cierra la inscripción a mesas?\n¿Y para diciembre?"),
            PropositoEmbedding.Consulta,
            Arg.Any<CancellationToken>());
        await _modelo.Received(1).ResponderAsync(
            Arg.Is<ConsultaModelo>(consulta => consulta.Historial.Count == 2 && consulta.Historial[0].EsDelUsuario && !consulta.Historial[1].EsDelUsuario),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_deja_usar_ni_ver_la_conversacion_de_otra_persona()
    {
        var ajena = Conversacion.Iniciar(Guid.NewGuid(), Guid.NewGuid(), "Pregunta ajena", Ahora);
        _conversaciones.ObtenerAsync(ajena.Id, Arg.Any<CancellationToken>()).Returns(ajena);

        var preguntar = await Asistente().PreguntarAsync(new PreguntaCommand(ajena.Id, "Hola"), CancellationToken.None);
        var ver = await Asistente().ObtenerAsync(ajena.Id, CancellationToken.None);
        var borrar = await Asistente().EliminarAsync(ajena.Id, CancellationToken.None);

        preguntar.Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        ver.Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        borrar.Error.Should().Be(ErroresAsistente.ConversacionNoEncontrada);
        _conversaciones.DidNotReceiveWithAnyArgs().Eliminar(default!);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Una_pregunta_vacia_no_se_procesa(string? texto)
    {
        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, texto!), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAsistente.PreguntaVacia);
        await _embeddings.DidNotReceiveWithAnyArgs().GenerarAsync(default!, default, default);
    }

    [Fact]
    public async Task Una_pregunta_demasiado_larga_se_rechaza()
    {
        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, new string('a', Conversacion.LargoMaximoPregunta + 1)), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAsistente.PreguntaDemasiadoLarga);
    }

    [Fact]
    public async Task Si_la_ia_no_responde_devuelve_no_disponible_y_no_guarda_nada()
    {
        ConEvidencia();
        _modelo.ResponderAsync(default!, default).ThrowsAsyncForAnyArgs(new ModeloNoDisponibleException("Gemini no respondió"));

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, "¿Cuándo cierra?"), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoDisponible);
        resultado.Error.Mensaje.Should().Contain("Secretaría").And.NotContain("Gemini");
        _conversaciones.DidNotReceiveWithAnyArgs().Agregar(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().GuardarCambiosAsync(default);
    }

    [Fact]
    public async Task Si_fallan_los_embeddings_tambien_devuelve_no_disponible()
    {
        _embeddings.GenerarAsync(default!, default, default).ThrowsAsyncForAnyArgs(new EmbeddingNoDisponibleException("sin red"));

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(null, "¿Cuándo cierra?"), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAsistente.NoDisponible);
    }

    [Fact]
    public async Task Una_conversacion_llena_pide_empezar_otra()
    {
        var llena = Conversacion.Iniciar(Guid.NewGuid(), _estudiante.Id, "Primera", Ahora);
        for (var i = 0; i < Conversacion.PreguntasMaximas; i++)
        {
            llena.Registrar($"P{i}", "Sin datos", [], true, Ahora);
        }

        _conversaciones.ObtenerAsync(llena.Id, Arg.Any<CancellationToken>()).Returns(llena);

        var resultado = await Asistente().PreguntarAsync(new PreguntaCommand(llena.Id, "Otra"), CancellationToken.None);

        resultado.Error.Should().Be(ErroresAsistente.ConversacionLlena);
    }

    [Theory]
    [InlineData(EstadoIndexacion.Indexado, true)]
    [InlineData(EstadoIndexacion.Reemplazado, true)]
    [InlineData(EstadoIndexacion.Retirado, false)]
    [InlineData(EstadoIndexacion.Pendiente, false)]
    public async Task Solo_se_pueden_descargar_las_fuentes_vigentes_o_reemplazadas(EstadoIndexacion estado, bool disponible)
    {
        var documento = DocumentoEn(estado);
        _documentos.ObtenerAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        _archivos.LeerAsync(documento.StoragePath, Arg.Any<CancellationToken>()).Returns("%PDF-1.7"u8.ToArray());

        var resultado = await Asistente().DescargarFuenteAsync(documento.Id, CancellationToken.None);

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
