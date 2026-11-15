using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Academic;
using Astrea.Domain.Common;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Astrea.Application.UnitTests.Features.Procedures;

/// <summary>Aprobación con emisión del PDF oficial: datos oficiales, idempotencia y fallas con reintento.</summary>
public class EmisionDocumentosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = "%PDF-1.7 prueba"u8.ToArray();

    private readonly ICurrentUser _sesion = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly ITramiteRepository _tramites = Substitute.For<ITramiteRepository>();
    private readonly IAcademicRepository _academico = Substitute.For<IAcademicRepository>();
    private readonly IPdfGenerator _pdf = Substitute.For<IPdfGenerator>();
    private readonly IFileStorage _archivos = Substitute.For<IFileStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Usuario _estudiante = Usuario.Registrar(Guid.NewGuid(), "uid-estudiante", "ana@astrea.test", Ahora);
    private readonly Usuario _secretaria = Usuario.Registrar(Guid.NewGuid(), "uid-secretaria", "secretaria@astrea.test", Ahora);
    private readonly Carrera _carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Tecnicatura Superior en Desarrollo de Software", 3, Ahora);
    private readonly TramiteTipo _tipo = CatalogoTramites.Tipos().Single(tipo => tipo.Id == CatalogoTramites.AlumnoRegularId);
    private readonly PlantillaDocumento _plantilla = CatalogoTramites.PlantillasIniciales().Single(plantilla => plantilla.Id == CatalogoTramites.PlantillaAlumnoRegularV1Id);
    private readonly Tramite _tramite;
    private DocumentoEmitido? _emitido;

    public EmisionDocumentosTests()
    {
        _clock.UtcNow.Returns(Ahora);
        _sesion.FirebaseUid.Returns("uid-secretaria");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-secretaria", Arg.Any<CancellationToken>()).Returns(_secretaria);
        _usuarios.ObtenerPorIdAsync(_estudiante.Id, Arg.Any<CancellationToken>()).Returns(_estudiante);
        _academico.ObtenerCarreraAsync(_carrera.Id, Arg.Any<CancellationToken>()).Returns(_carrera);

        _tramite = Tramite.Solicitar(
            Guid.NewGuid(),
            _estudiante.Id,
            _tipo,
            new Dictionary<string, string?> { ["carrera"] = _carrera.Id.ToString(), ["presentarAnte"] = "ANSES" },
            null,
            Ahora);
        _tramites.ObtenerAsync(_tramite.Id, Arg.Any<CancellationToken>()).Returns(_tramite);
        _tramites.ObtenerTipoAsync(_tipo.Id, Arg.Any<CancellationToken>()).Returns(_tipo);
        _tramites.ObtenerPlantillaVigenteAsync(_tipo.Id, Arg.Any<CancellationToken>()).Returns(_plantilla);
        _tramites.ObtenerPersonasAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IEnumerable<Guid>>().ToDictionary(id => id, id => new PersonaDto(id, $"{id}@astrea.test", null, null, null)));

        // El repositorio "recuerda" el documento agregado, como haría la base.
        _tramites.When(repositorio => repositorio.Agregar(Arg.Any<DocumentoEmitido>())).Do(call => _emitido = call.Arg<DocumentoEmitido>());
        _tramites.ObtenerDocumentoDeTramiteAsync(_tramite.Id, Arg.Any<CancellationToken>()).Returns(_ => _emitido);
        _pdf.Generar(Arg.Any<ContenidoDocumento>()).Returns(Pdf);
    }

    private BandejaTramites Bandeja()
    {
        var detalles = new DetalleTramites(_tramites, _academico, new ConfiguracionDocumentos { UrlValidacion = "https://astrea.test/validar/{codigo}" });
        var emision = new EmisionDocumentos(_tramites, _usuarios, detalles, _pdf, _archivos, new ConfiguracionDocumentos { UrlValidacion = "https://astrea.test/validar/{codigo}" }, _clock);
        return new BandejaTramites(new UsuarioActual(_sesion, _usuarios), _tramites, detalles, emision, _archivos, _unitOfWork, _clock);
    }

    private void CargarDatosOficiales() => _estudiante.ActualizarDatosOficiales("Ana", "Pérez", "30.123.456", "L-123", Ahora);

    [Fact]
    public async Task Sin_datos_oficiales_no_se_aprueba_ni_se_genera_el_pdf()
    {
        var resultado = await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        resultado.Error.Should().Be(ErroresTramites.DatosOficialesIncompletos);
        _tramite.Estado.Should().Be(EstadoTramite.Pendiente);
        _pdf.DidNotReceiveWithAnyArgs().Generar(default!);
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Al_aprobar_emite_el_pdf_con_los_datos_oficiales_la_plantilla_y_el_qr()
    {
        CargarDatosOficiales();

        var resultado = await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand("Ya podés descargarla"), CancellationToken.None);

        _pdf.Received(1).Generar(Arg.Is<ContenidoDocumento>(contenido =>
            contenido.Titulo == "Constancia de alumno regular"
            && contenido.Cuerpo.Contains("Ana Pérez, DNI 30.123.456")
            && contenido.Cuerpo.Contains("Tecnicatura Superior en Desarrollo de Software")
            && contenido.Cuerpo.Contains("ANSES")
            && contenido.Cuerpo.Contains("15 de noviembre de 2026")
            && contenido.UrlValidacion == $"https://astrea.test/validar/{contenido.CodigoValidacion}"
            && contenido.VersionPlantilla == 1));
        await _archivos.Received(1).GuardarAsync(Arg.Is<string>(ruta => ruta.StartsWith("documentos/2026/") && ruta.EndsWith(".pdf")), Pdf, Arg.Any<CancellationToken>());

        _emitido!.HashSha256.Should().Be(DocumentoEmitido.CalcularHash(Pdf));
        _emitido.DniUltimosDigitos.Should().Be("456");
        resultado.Valor.Estado.Should().Be(EstadoTramite.Listo);
        resultado.Valor.Documento!.CodigoValidacion.Should().Be(_emitido.CodigoValidacion);
        resultado.Valor.EmiteDocumento.Should().BeTrue();
    }

    [Fact]
    public async Task Aprobar_dos_veces_no_emite_otro_documento()
    {
        CargarDatosOficiales();
        await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        var segunda = await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        segunda.EsExitoso.Should().BeTrue();
        _pdf.Received(1).Generar(Arg.Any<ContenidoDocumento>());
        _tramites.Received(1).Agregar(Arg.Any<DocumentoEmitido>());
        _tramite.Eventos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Si_no_se_puede_guardar_el_pdf_el_tramite_no_queda_listo_y_se_puede_reintentar()
    {
        CargarDatosOficiales();
        _archivos.GuardarAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("Disco lleno"));

        var aprobar = () => Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        await aprobar.Should().ThrowAsync<IOException>();
        _tramite.Estado.Should().Be(EstadoTramite.Pendiente);
        _emitido.Should().BeNull();
        await _unitOfWork.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());

        // Reintento cuando el almacenamiento vuelve a funcionar.
        _archivos.GuardarAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var reintento = await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        reintento.Valor.Estado.Should().Be(EstadoTramite.Listo);
        reintento.Valor.Documento.Should().NotBeNull();
    }

    [Fact]
    public async Task Si_falla_el_guardado_en_la_base_se_borra_el_pdf_para_no_dejar_huerfanos()
    {
        CargarDatosOficiales();
        _unitOfWork.GuardarCambiosAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Se cortó la conexión"));

        var aprobar = () => Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        await aprobar.Should().ThrowAsync<InvalidOperationException>();
        await _archivos.Received(1).EliminarAsync(_emitido!.StoragePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_tipo_sin_plantilla_se_aprueba_sin_pdf_y_sin_pedir_datos_oficiales()
    {
        _tramites.ObtenerPlantillaVigenteAsync(_tipo.Id, Arg.Any<CancellationToken>()).Returns((PlantillaDocumento?)null);

        var resultado = await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand("Retiralo en Secretaría"), CancellationToken.None);

        resultado.Valor.Estado.Should().Be(EstadoTramite.Listo);
        resultado.Valor.EmiteDocumento.Should().BeFalse();
        resultado.Valor.Documento.Should().BeNull();
        _pdf.DidNotReceiveWithAnyArgs().Generar(default!);
    }

    [Fact]
    public async Task Un_tramite_rechazado_no_se_aprueba_ni_emite()
    {
        CargarDatosOficiales();
        _tramite.Rechazar(_secretaria.Id, "Datos incorrectos", Ahora);

        var aprobar = () => Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);

        await aprobar.Should().ThrowAsync<DomainException>().WithMessage("*finalizado*");
        _pdf.DidNotReceiveWithAnyArgs().Generar(default!);
    }

    [Fact]
    public async Task La_validacion_publica_muestra_solo_datos_minimos()
    {
        CargarDatosOficiales();
        await Bandeja().AprobarAsync(_tramite.Id, new AprobarTramiteCommand(null), CancellationToken.None);
        _tramites.ObtenerDocumentoPorCodigoAsync(_emitido!.CodigoValidacion, Arg.Any<CancellationToken>()).Returns(_emitido);

        // Tipeado a mano: minúsculas, sin guiones y con una "o" en lugar de un cero.
        var tipeado = _emitido.CodigoValidacion.Replace("-", string.Empty).Replace('0', 'o').ToLowerInvariant();
        var resultado = await new ValidacionDocumentos(_tramites).ValidarAsync(tipeado, CancellationToken.None);

        resultado.Valor.Should().BeEquivalentTo(new
        {
            _emitido.CodigoValidacion,
            TipoDocumento = "Constancia de alumno regular",
            Estado = EstadoDocumento.Vigente,
            DniParcial = "**.***.456",
        });
    }

    [Fact]
    public async Task Un_codigo_inexistente_o_mal_formado_no_valida()
    {
        var resultado = await new ValidacionDocumentos(_tramites).ValidarAsync("no-es-un-codigo", CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.NoEncontrado);
    }
}
