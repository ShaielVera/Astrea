using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Exams;
using Astrea.Domain.LostAndFound;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.LostAndFound;

/// <summary>
/// Lo que hace cualquier usuario con sesión: buscar hallazgos publicados, publicar uno (con foto, obligatoria para aparecer en la búsqueda)
/// y seguir los propios. La búsqueda devuelve siempre el DTO público, nunca la entidad.
/// </summary>
public sealed class Hallazgos(
    UsuarioActual usuarioActual,
    IObjetoPerdidoRepository objetos,
    IFileStorage archivos,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private static readonly EstadoObjeto[] SoloPublicados = [EstadoObjeto.Publicado];

    public async Task<Result<PagedResult<HallazgoPublicoDto>>> BuscarAsync(
        string? texto,
        CategoriaObjeto? categoria,
        PuntoResguardo? punto,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto?.Length > ErroresHallazgos.LargoMaximoTexto)
        {
            return ErroresHallazgos.TextoDemasiadoLargo;
        }

        var pagina = PageRequest.Create(page, pageSize);
        var encontrados = await objetos.BuscarAsync(new FiltroHallazgos(SoloPublicados, texto, categoria, punto, SoloConFoto: true), pagina, cancellationToken);
        return PagedResult<HallazgoPublicoDto>.From(encontrados.Items.Select(HallazgoPublicoDto.Desde).ToList(), pagina, encontrados.TotalItems);
    }

    public async Task<Result<HallazgoPublicoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var objeto = await objetos.ObtenerAsync(id, cancellationToken);
        if (objeto is not { EsVisibleEnBusqueda: true })
        {
            return ErroresHallazgos.NoEncontrado;
        }

        return HallazgoPublicoDto.Desde(objeto);
    }

    public async Task<Result<MiHallazgoDto>> PublicarAsync(PublicarHallazgoCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var ahora = clock.UtcNow;
        var objeto = ObjetoPerdido.Publicar(
            Guid.CreateVersion7(),
            usuario.Valor.Id,
            new DatosHallazgo(comando.TituloPublico, comando.DescripcionPrivada, comando.Categoria, comando.SectorEncontrado, comando.FranjaHoraria, comando.PuntoResguardo, comando.FechaHallazgo),
            ZonaHorariaInstitucional.Hoy(ahora),
            ahora);

        objetos.Agregar(objeto);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return MiHallazgoDto.Desde(objeto);
    }

    /// <summary>
    /// Sube (o reemplaza) la foto. Solo quien lo publicó o la Secretaría, y mientras siga publicado.
    /// La foto se valida por su contenido real y se le quitan los metadatos; el nombre lo genera el servidor.
    /// </summary>
    public async Task<Result<MiHallazgoDto>> SubirFotoAsync(Guid id, byte[] contenido, string? contentType, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var objeto = await objetos.ObtenerAsync(id, cancellationToken);
        if (objeto is null)
        {
            return ErroresHallazgos.NoEncontrado;
        }

        if (!objeto.PuedeEditar(usuario.Valor.Id, usuario.Valor.TieneRol(Rol.Administrador)))
        {
            return ErroresHallazgos.SinPermiso;
        }

        var (limpia, tipo, extension) = FotoHallazgo.Validar(contenido, contentType);
        var ruta = $"hallazgos/{clock.UtcNow:yyyy}/{objeto.Id}-{Guid.NewGuid():N}.{extension}";
        var anterior = objeto.AsignarFoto(ruta, tipo);

        await archivos.GuardarAsync(ruta, limpia, cancellationToken);
        try
        {
            await unitOfWork.GuardarCambiosAsync(cancellationToken);
        }
        catch
        {
            await archivos.EliminarAsync(ruta, CancellationToken.None);
            throw;
        }

        if (anterior is not null)
        {
            await archivos.EliminarAsync(anterior, cancellationToken);
        }

        return MiHallazgoDto.Desde(objeto);
    }

    /// <summary>La foto de un hallazgo publicado la ve cualquier usuario con sesión; si ya se cerró, solo quien lo publicó y la Secretaría.</summary>
    public async Task<Result<ArchivoDto>> ObtenerFotoAsync(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var objeto = await objetos.ObtenerAsync(id, cancellationToken);
        if (objeto is null || (!objeto.EstaPublicado && !objeto.PuedeEditar(usuario.Valor.Id, usuario.Valor.TieneRol(Rol.Administrador))))
        {
            return ErroresHallazgos.NoEncontrado;
        }

        var contenido = objeto.FotoStoragePath is null ? null : await archivos.LeerAsync(objeto.FotoStoragePath, cancellationToken);
        if (contenido is null)
        {
            return ErroresHallazgos.SinFoto;
        }

        return new ArchivoDto(Path.GetFileName(objeto.FotoStoragePath)!, objeto.FotoContentType!, contenido);
    }

    public async Task<Result<PagedResult<MiHallazgoDto>>> ListarMiosAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var pagina = PageRequest.Create(page, pageSize);
        var mios = await objetos.BuscarAsync(new FiltroHallazgos(Enum.GetValues<EstadoObjeto>(), PublicadoPor: usuario.Valor.Id), pagina, cancellationToken);
        return PagedResult<MiHallazgoDto>.From(mios.Items.Select(MiHallazgoDto.Desde).ToList(), pagina, mios.TotalItems);
    }
}

/// <summary>Moderación y cierre de hallazgos (solo Administrador). Entregas y bajas quedan registradas con su autor y fecha.</summary>
public sealed class AdministracionHallazgos(
    UsuarioActual usuarioActual,
    IObjetoPerdidoRepository objetos,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<PagedResult<HallazgoAdminDto>>> BuscarAsync(EstadoObjeto[]? estados, string? texto, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto?.Length > ErroresHallazgos.LargoMaximoTexto)
        {
            return ErroresHallazgos.TextoDemasiadoLargo;
        }

        var pagina = PageRequest.Create(page, pageSize);
        var filtro = new FiltroHallazgos(estados is { Length: > 0 } ? estados : Enum.GetValues<EstadoObjeto>(), texto);
        var encontrados = await objetos.BuscarAsync(filtro, pagina, cancellationToken);
        var items = await ADtosAsync(encontrados.Items, cancellationToken);
        return PagedResult<HallazgoAdminDto>.From(items, pagina, encontrados.TotalItems);
    }

    public async Task<Result<HallazgoAdminDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var objeto = await objetos.ObtenerAsync(id, cancellationToken);
        if (objeto is null)
        {
            return ErroresHallazgos.NoEncontrado;
        }

        return (await ADtosAsync([objeto], cancellationToken))[0];
    }

    public Task<Result<HallazgoAdminDto>> EntregarAsync(Guid id, EntregarHallazgoCommand comando, CancellationToken cancellationToken) =>
        CerrarAsync(
            id,
            (objeto, adminId) => objeto.Entregar(Guid.CreateVersion7(), adminId, comando.NombreRetirante, comando.DniRetirante, comando.IdentidadVerificada, comando.ObservacionInterna, clock.UtcNow),
            cancellationToken);

    public Task<Result<HallazgoAdminDto>> DarDeBajaAsync(Guid id, DarDeBajaHallazgoCommand comando, CancellationToken cancellationToken) =>
        CerrarAsync(id, (objeto, adminId) => objeto.DarDeBaja(adminId, comando.Motivo, clock.UtcNow), cancellationToken);

    private async Task<Result<HallazgoAdminDto>> CerrarAsync(Guid id, Action<ObjetoPerdido, Guid> cierre, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        var objeto = await objetos.ObtenerAsync(id, cancellationToken);
        if (objeto is null)
        {
            return ErroresHallazgos.NoEncontrado;
        }

        cierre(objeto, administrador.Valor.Id);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return (await ADtosAsync([objeto], cancellationToken))[0];
    }

    private async Task<List<HallazgoAdminDto>> ADtosAsync(IReadOnlyList<ObjetoPerdido> lista, CancellationToken cancellationToken)
    {
        var ids = lista.Select(objeto => objeto.PublicadoPorUsuarioId)
            .Concat(lista.Select(objeto => objeto.Entrega?.VerificadoPorUsuarioId).OfType<Guid>())
            .Concat(lista.Select(objeto => objeto.DadoDeBajaPorUsuarioId).OfType<Guid>())
            .Distinct();
        var personas = await objetos.ObtenerPersonasAsync(ids, cancellationToken);

        string Nombre(Guid id) => personas.GetValueOrDefault(id) is { } persona
            ? persona.Apellido is not null || persona.Nombre is not null ? $"{persona.Apellido}, {persona.Nombre}".Trim(' ', ',') : persona.Email
            : "Usuario eliminado";

        return lista.Select(objeto => new HallazgoAdminDto(
                objeto.Id,
                objeto.TituloPublico,
                objeto.DescripcionPrivada,
                objeto.Categoria,
                objeto.SectorEncontrado,
                objeto.FranjaHoraria,
                objeto.PuntoResguardo,
                objeto.FechaHallazgo,
                objeto.FechaPublicacion,
                objeto.FotoStoragePath is not null,
                objeto.Estado,
                objeto.FechaCierre,
                personas.GetValueOrDefault(objeto.PublicadoPorUsuarioId) ?? new PersonaHallazgoDto(objeto.PublicadoPorUsuarioId, "Usuario eliminado", null, null),
                objeto.Entrega is { } entrega
                    ? new EntregaDto(entrega.NombreRetirante, entrega.DniRetirante, entrega.IdentidadVerificada, Nombre(entrega.VerificadoPorUsuarioId), entrega.ObservacionInterna, entrega.FechaEntrega)
                    : null,
                objeto.MotivoBaja,
                objeto.DadoDeBajaPorUsuarioId is { } baja ? Nombre(baja) : null))
            .ToList();
    }
}

/// <summary>
/// Borra los hallazgos cerrados (entregados o dados de baja) hace <see cref="ObjetoPerdido.DiasDeConservacion"/> días o más,
/// junto con su foto y el registro de entrega (nombre y DNI de quien lo retiró): pasado ese plazo ya no sirve conservarlos.
/// </summary>
public sealed class DepuracionHallazgos(
    IObjetoPerdidoRepository objetos,
    IFileStorage archivos,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Cuántos se borran por tanda, para no cargar demasiados en memoria.</summary>
    public const int Tanda = 100;

    /// <summary>Devuelve cuántos hallazgos borró.</summary>
    public async Task<int> DepurarAsync(CancellationToken cancellationToken)
    {
        var ahora = clock.UtcNow;
        var total = 0;
        while (true)
        {
            var vencidos = await objetos.ListarCerradosAntesDeAsync(ahora.AddDays(-ObjetoPerdido.DiasDeConservacion), Tanda, cancellationToken);
            if (vencidos.Count == 0)
            {
                return total;
            }

            var fotos = vencidos.Select(objeto => objeto.FotoStoragePath).OfType<string>().ToList();
            foreach (var objeto in vencidos)
            {
                objetos.Eliminar(objeto);
            }

            await unitOfWork.GuardarCambiosAsync(cancellationToken);

            // Las fotos se borran después de confirmar el borrado en la base: si algo falla antes, no quedan registros sin foto.
            foreach (var foto in fotos)
            {
                await archivos.EliminarAsync(foto, cancellationToken);
            }

            total += vencidos.Count;
            if (vencidos.Count < Tanda)
            {
                return total;
            }
        }
    }
}
