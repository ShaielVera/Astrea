using System.Reflection;
using System.Text.Json;
using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Academic;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Exams;
using Astrea.Application.Features.LostAndFound;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.Agent;

/// <summary>Las dependencias del asistente: repositorios simulados y casos de uso reales.</summary>
internal sealed class EntornoAsistente
{
    public static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);

    public static readonly CarreraDto Matematica = new(Guid.NewGuid(), "PM", "Profesorado de Matemática", 4, true);
    public static readonly CarreraDto Historia = new(Guid.NewGuid(), "PH", "Profesorado de Historia", 4, true);

    public EntornoAsistente()
    {
        Clock.UtcNow.Returns(Ahora);
        Sesion.FirebaseUid.Returns("uid-estudiante");
        Usuarios.ObtenerPorFirebaseUidAsync("uid-estudiante", Arg.Any<CancellationToken>()).Returns(Estudiante);
        Academico.ListarCarrerasAsync(default!, default, default).ReturnsForAnyArgs(PagedResult<CarreraDto>.From([Matematica, Historia], PageRequest.Create(1, 50), 2));
        Academico.BuscarMateriasAsync(default!, default, default, default).ReturnsForAnyArgs([]);
        Tramites.ListarTiposAsync(default, default).ReturnsForAnyArgs(CatalogoTramites.Tipos());
        Tramites.ObtenerTipoAsync(default, default).ReturnsForAnyArgs(llamada => CatalogoTramites.Tipos().SingleOrDefault(tipo => tipo.Id == llamada.Arg<Guid>()));
        Embeddings.GenerarAsync(default!, default, default).ReturnsForAnyArgs([new float[Domain.Knowledge.FragmentoRag.DimensionesEmbedding]]);
        Documentos.BuscarSimilaresAsync(default!, default, default).ReturnsForAnyArgs([]);
    }

    public IAcademicRepository Academico { get; } = Substitute.For<IAcademicRepository>();

    public IExamRepository Mesas { get; } = Substitute.For<IExamRepository>();

    public IObjetoPerdidoRepository Objetos { get; } = Substitute.For<IObjetoPerdidoRepository>();

    public ITramiteRepository Tramites { get; } = Substitute.For<ITramiteRepository>();

    public IConversacionRepository Conversaciones { get; } = Substitute.For<IConversacionRepository>();

    public IDocumentoRagRepository Documentos { get; } = Substitute.For<IDocumentoRagRepository>();

    public IEmbeddingService Embeddings { get; } = Substitute.For<IEmbeddingService>();

    public IModeloLenguaje Modelo { get; } = Substitute.For<IModeloLenguaje>();

    public IFileStorage Archivos { get; } = Substitute.For<IFileStorage>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IClock Clock { get; } = Substitute.For<IClock>();

    public ICurrentUser Sesion { get; } = Substitute.For<ICurrentUser>();

    public IUserRepository Usuarios { get; } = Substitute.For<IUserRepository>();

    public Usuario Estudiante { get; } = Usuario.Registrar(Guid.NewGuid(), "uid-estudiante", "estudiante@astrea.test", Ahora);

    public UsuarioActual UsuarioActual => new(Sesion, Usuarios);

    public SolicitudesTramite Solicitudes =>
        new(UsuarioActual, Tramites, new DetalleTramites(Tramites, Academico, new ConfiguracionDocumentos()), Archivos, UnitOfWork, Clock);

    public HerramientasAsistente Herramientas() => new(
        Academico,
        new Correlatividades(Academico, UnitOfWork),
        new ConsultasMesas(Mesas, Clock),
        new Hallazgos(UsuarioActual, Objetos, Archivos, UnitOfWork, Clock),
        Solicitudes,
        Tramites,
        Clock);

    public Asistente Asistente(double similitudMinima = 0.5) => new(
        UsuarioActual,
        Conversaciones,
        Documentos,
        Embeddings,
        Modelo,
        Herramientas(),
        Solicitudes,
        Archivos,
        UnitOfWork,
        Clock,
        new ConfiguracionIa { Asistente = new ConfiguracionAsistente { SimilitudMinima = similitudMinima } },
        NullLogger<Asistente>.Instance);

    /// <summary>El modelo propone, en orden, una llamada por paso.</summary>
    public void ModeloPropone(params LlamadaHerramienta[] pasos)
    {
        var turnos = pasos.Select(paso => new TurnoLlamadas([paso])).ToArray();
        Modelo.SiguientePasoAsync(default!, default).ReturnsForAnyArgs(turnos[0], turnos[1..]);
    }

    public static LlamadaHerramienta Llamada(string nombre, object argumentos) => new(nombre, JsonSerializer.SerializeToElement(argumentos));

    public static LlamadaHerramienta Responder(string respuesta, string[] fuentes, bool sinEvidencia = false, bool esAclaracion = false) =>
        Llamada(HerramientasAsistente.Responder, new { respuesta, fuentes, sinEvidencia, esAclaracion });

    /// <summary>Un usuario sin el rol de estudiante (los usuarios nacen con ese rol; el dominio no permite quitarlo).</summary>
    public static Usuario SinRoles()
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid-sin-roles", "sin-roles@astrea.test", Ahora);
        var roles = typeof(Usuario).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(campo => campo.Name == "_roles");
        ((System.Collections.IList)roles.GetValue(usuario)!).Clear();
        return usuario;
    }
}
