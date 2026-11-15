using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.LostAndFound;
using Astrea.Domain.LostAndFound;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;

namespace Astrea.Application.UnitTests.Features.LostAndFound;

public class HallazgosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly ICurrentUser _sesion = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly IObjetoPerdidoRepository _objetos = Substitute.For<IObjetoPerdidoRepository>();
    private readonly IFileStorage _archivos = Substitute.For<IFileStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Usuario _publicador = Usuario.Registrar(Guid.NewGuid(), "uid-publicador", "ana@astrea.test", Ahora);
    private readonly Usuario _otro = Usuario.Registrar(Guid.NewGuid(), "uid-otro", "otro@astrea.test", Ahora);
    private readonly ObjetoPerdido _objeto;

    public HallazgosTests()
    {
        _clock.UtcNow.Returns(Ahora);
        _usuarios.ObtenerPorFirebaseUidAsync("uid-publicador", Arg.Any<CancellationToken>()).Returns(_publicador);
        _usuarios.ObtenerPorFirebaseUidAsync("uid-otro", Arg.Any<CancellationToken>()).Returns(_otro);
        _objeto = ObjetoPerdido.Publicar(
            Guid.NewGuid(),
            _publicador.Id,
            new DatosHallazgo("Termo negro", "Calcomanía de Racing", CategoriaObjeto.TermoOBotella, "Aula 3", FranjaHoraria.Manana, PuntoResguardo.Secretaria, new DateOnly(2026, 11, 15)),
            new DateOnly(2026, 11, 15),
            Ahora);
        _objetos.ObtenerAsync(_objeto.Id, Arg.Any<CancellationToken>()).Returns(_objeto);
    }

    private Hallazgos Servicio(string uid)
    {
        _sesion.FirebaseUid.Returns(uid);
        return new Hallazgos(new UsuarioActual(_sesion, _usuarios), _objetos, _archivos, _unitOfWork, _clock);
    }

    [Fact]
    public void El_dto_publico_no_tiene_ningun_dato_privado()
    {
        var propiedades = typeof(HallazgoPublicoDto).GetProperties().Select(propiedad => propiedad.Name).ToList();

        propiedades.Should().NotContain(["DescripcionPrivada", "PublicadoPorUsuarioId", "PublicadoPor", "Entrega", "FotoStoragePath", "MotivoBaja"]);
        HallazgoPublicoDto.Desde(_objeto).Should().BeEquivalentTo(new { TituloPublico = "Termo negro", PuntoResguardo = PuntoResguardo.Secretaria, TieneFoto = false });
    }

    [Fact]
    public async Task Quien_lo_publico_sube_la_foto_con_un_nombre_generado_por_el_servidor()
    {
        var resultado = await Servicio("uid-publicador").SubirFotoAsync(_objeto.Id, Png, "image/png", CancellationToken.None);

        resultado.Valor.TieneFoto.Should().BeTrue();
        _objeto.FotoStoragePath.Should().MatchRegex($"^hallazgos/2026/{_objeto.Id}-[0-9a-f]{{32}}\\.png$");
        await _archivos.Received(1).GuardarAsync(_objeto.FotoStoragePath!, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Otro_estudiante_no_puede_cambiar_la_foto()
    {
        var resultado = await Servicio("uid-otro").SubirFotoAsync(_objeto.Id, Png, "image/png", CancellationToken.None);

        resultado.Error.Should().Be(ErroresHallazgos.SinPermiso);
        await _archivos.DidNotReceiveWithAnyArgs().GuardarAsync(default!, default!, default);
    }

    [Fact]
    public async Task Reemplazar_la_foto_borra_la_anterior_del_almacenamiento()
    {
        await Servicio("uid-publicador").SubirFotoAsync(_objeto.Id, Png, "image/png", CancellationToken.None);
        var primera = _objeto.FotoStoragePath!;

        await Servicio("uid-publicador").SubirFotoAsync(_objeto.Id, Png, "image/png", CancellationToken.None);

        _objeto.FotoStoragePath.Should().NotBe(primera);
        await _archivos.Received(1).EliminarAsync(primera, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_foto_de_un_objeto_ya_entregado_solo_la_ve_quien_lo_publico()
    {
        _objeto.AsignarFoto("hallazgos/2026/x.png", "image/png");
        _archivos.LeerAsync("hallazgos/2026/x.png", Arg.Any<CancellationToken>()).Returns(Png);
        _objeto.Entregar(Guid.NewGuid(), Guid.NewGuid(), "Juan", "30123456", true, null, Ahora);

        (await Servicio("uid-otro").ObtenerFotoAsync(_objeto.Id, CancellationToken.None)).Error.Should().Be(ErroresHallazgos.NoEncontrado);
        (await Servicio("uid-publicador").ObtenerFotoAsync(_objeto.Id, CancellationToken.None)).Valor.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task Un_hallazgo_cerrado_no_aparece_en_el_detalle_publico()
    {
        _objeto.DarDeBaja(Guid.NewGuid(), "Duplicado", Ahora);

        var resultado = await Servicio("uid-otro").ObtenerAsync(_objeto.Id, CancellationToken.None);

        resultado.Error.Should().Be(ErroresHallazgos.NoEncontrado);
    }

    [Fact]
    public async Task La_busqueda_publica_pide_solo_los_que_tienen_foto()
    {
        _objetos.BuscarAsync(default!, default!, default).ReturnsForAnyArgs(PagedResult<ObjetoPerdido>.From([], PageRequest.Create(1, 10), 0));

        await Servicio("uid-otro").BuscarAsync(null, null, null, null, null, CancellationToken.None);

        await _objetos.Received(1).BuscarAsync(Arg.Is<FiltroHallazgos>(filtro => filtro.SoloConFoto), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_foto_no_aparece_en_el_detalle_publico()
    {
        (await Servicio("uid-otro").ObtenerAsync(_objeto.Id, CancellationToken.None)).Error.Should().Be(ErroresHallazgos.NoEncontrado);

        _objeto.AsignarFoto("hallazgos/2026/x.png", "image/png");

        (await Servicio("uid-otro").ObtenerAsync(_objeto.Id, CancellationToken.None)).EsExitoso.Should().BeTrue();
    }

    [Fact]
    public async Task La_depuracion_borra_los_vencidos_y_sus_fotos_despues_de_guardar()
    {
        _objeto.AsignarFoto("hallazgos/2026/x.png", "image/png");
        _objeto.Entregar(Guid.NewGuid(), Guid.NewGuid(), "Juan", "30123456", true, null, Ahora.AddDays(-31));
        _objetos.ListarCerradosAntesDeAsync(Ahora.AddDays(-ObjetoPerdido.DiasDeConservacion), DepuracionHallazgos.Tanda, Arg.Any<CancellationToken>())
            .Returns([_objeto]);

        var borrados = await new DepuracionHallazgos(_objetos, _archivos, _unitOfWork, _clock).DepurarAsync(CancellationToken.None);

        borrados.Should().Be(1);
        Received.InOrder(() =>
        {
            _objetos.Eliminar(_objeto);
            _unitOfWork.GuardarCambiosAsync(Arg.Any<CancellationToken>());
            _archivos.EliminarAsync("hallazgos/2026/x.png", Arg.Any<CancellationToken>());
        });
    }
}
