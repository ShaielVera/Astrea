using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Domain.Procedures;

namespace Astrea.Application.Features.Procedures;

/// <summary>
/// Lo que hace el estudiante: ver el catálogo, pedir un trámite y seguir los suyos.
/// Un trámite ajeno se responde como inexistente (404), para no revelar qué números existen.
/// </summary>
public sealed class SolicitudesTramite(
    UsuarioActual usuarioActual,
    ITramiteRepository tramites,
    DetalleTramites detalles,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<IReadOnlyList<TramiteTipoDto>> ListarTiposAsync(CancellationToken cancellationToken) =>
        (await tramites.ListarTiposAsync(incluirInactivos: false, cancellationToken)).Select(TramiteTipoDto.Desde).ToList();

    public async Task<Result<TramiteDetalleDto>> SolicitarAsync(SolicitarTramiteCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        // Reintento de la misma solicitud (doble clic, red que se corta): se devuelve la que ya existe.
        if (comando.ClaveIdempotencia is { } clave
            && await tramites.ObtenerPorClaveIdempotenciaAsync(usuario.Valor.Id, clave, cancellationToken) is { } existente)
        {
            return await detalles.ArmarAsync(existente, usuario.Valor.Id, vistaAdministracion: false, cancellationToken);
        }

        var tipo = await tramites.ObtenerTipoAsync(comando.TipoId, cancellationToken);
        if (tipo is null)
        {
            return ErroresTramites.TipoNoEncontrado;
        }

        var tramite = Tramite.Solicitar(Guid.CreateVersion7(), usuario.Valor.Id, tipo, comando.Datos, comando.ClaveIdempotencia, clock.UtcNow);
        if (!await detalles.CarrerasValidasAsync(tipo, tramite.Datos, cancellationToken))
        {
            return ErroresTramites.CarreraInvalida;
        }

        tramites.Agregar(tramite);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await detalles.ArmarAsync(tramite, usuario.Valor.Id, vistaAdministracion: false, cancellationToken);
    }

    public async Task<Result<PagedResult<TramiteResumenDto>>> ListarMiosAsync(EstadoTramite[]? estados, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var filtro = new FiltroTramites(UsuarioId: usuario.Valor.Id, Estados: estados is { Length: > 0 } ? estados : null, MasRecientesPrimero: true);
        return await tramites.BuscarAsync(filtro, PageRequest.Create(page, pageSize), cancellationToken);
    }

    public async Task<Result<TramiteDetalleDto>> ObtenerMioAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null || !tramite.EsTitular(usuario.Valor.Id))
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        return await detalles.ArmarAsync(tramite, usuario.Valor.Id, vistaAdministracion: false, cancellationToken);
    }
}

/// <summary>
/// Bandeja de Bedelía/Secretaría (solo Administrador): revisar, tomar, aprobar o rechazar.
/// Cada transición registra al administrador que la hizo.
/// </summary>
public sealed class BandejaTramites(
    UsuarioActual usuarioActual,
    ITramiteRepository tramites,
    DetalleTramites detalles,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<PagedResult<TramiteResumenDto>>> BuscarAsync(
        EstadoTramite[]? estados,
        Guid? tipoId,
        string? texto,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto?.Length > ErroresTramites.LargoMaximoTexto)
        {
            return ErroresTramites.TextoDemasiadoLargo;
        }

        var filtro = new FiltroTramites(Estados: estados is { Length: > 0 } ? estados : null, TipoId: tipoId, Texto: texto);
        return await tramites.BuscarAsync(filtro, PageRequest.Create(page, pageSize), cancellationToken);
    }

    public async Task<IReadOnlyList<TramiteTipoDto>> ListarTiposAsync(CancellationToken cancellationToken) =>
        (await tramites.ListarTiposAsync(incluirInactivos: true, cancellationToken)).Select(TramiteTipoDto.Desde).ToList();

    public async Task<Result<TramiteDetalleDto>> ObtenerAsync(Guid tramiteId, CancellationToken cancellationToken)
    {
        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null)
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
    }

    public Task<Result<TramiteDetalleDto>> TomarAsync(Guid tramiteId, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(tramiteId, (tramite, adminId) => tramite.Tomar(adminId, clock.UtcNow), cancellationToken);

    public Task<Result<TramiteDetalleDto>> AprobarAsync(Guid tramiteId, AprobarTramiteCommand comando, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(tramiteId, (tramite, adminId) => tramite.Aprobar(adminId, comando.Comentario, clock.UtcNow), cancellationToken);

    public Task<Result<TramiteDetalleDto>> RechazarAsync(Guid tramiteId, RechazarTramiteCommand comando, CancellationToken cancellationToken) =>
        CambiarEstadoAsync(tramiteId, (tramite, adminId) => tramite.Rechazar(adminId, comando.Motivo, clock.UtcNow), cancellationToken);

    private async Task<Result<TramiteDetalleDto>> CambiarEstadoAsync(Guid tramiteId, Action<Tramite, Guid> cambio, CancellationToken cancellationToken)
    {
        var administrador = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!administrador.EsExitoso)
        {
            return administrador.Error!;
        }

        var tramite = await tramites.ObtenerAsync(tramiteId, cancellationToken);
        if (tramite is null)
        {
            return ErroresTramites.TramiteNoEncontrado;
        }

        cambio(tramite, administrador.Valor.Id);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return await detalles.ArmarAsync(tramite, usuarioActualId: null, vistaAdministracion: true, cancellationToken);
    }
}

/// <summary>Arma el detalle de un trámite para el estudiante o para la administración, y valida las carreras elegidas.</summary>
public sealed class DetalleTramites(ITramiteRepository tramites, IAcademicRepository academico)
{
    /// <summary>Los campos de tipo carrera deben apuntar a una carrera existente y activa.</summary>
    public async Task<bool> CarrerasValidasAsync(TramiteTipo tipo, IReadOnlyDictionary<string, string> datos, CancellationToken cancellationToken)
    {
        foreach (var campo in tipo.Campos.Where(campo => campo.Tipo == TipoCampo.Carrera))
        {
            if (datos.TryGetValue(campo.Clave, out var valor)
                && await academico.ObtenerCarreraAsync(Guid.Parse(valor), cancellationToken) is not { Activa: true })
            {
                return false;
            }
        }

        return true;
    }

    public async Task<TramiteDetalleDto> ArmarAsync(Tramite tramite, Guid? usuarioActualId, bool vistaAdministracion, CancellationToken cancellationToken)
    {
        var tipo = (await tramites.ObtenerTipoAsync(tramite.TramiteTipoId, cancellationToken))!;
        var involucrados = tramite.Eventos.Select(evento => evento.ActorUsuarioId)
            .Append(tramite.UsuarioId)
            .Concat(tramite.AdministradorAsignadoId is { } asignado ? [asignado] : []);
        var personas = await tramites.ObtenerPersonasAsync(involucrados.Distinct(), cancellationToken);

        var datos = new List<DatoTramiteDto>();
        foreach (var campo in tipo.Campos)
        {
            if (tramite.Datos.TryGetValue(campo.Clave, out var valor))
            {
                datos.Add(new DatoTramiteDto(campo.Clave, campo.Etiqueta, await ValorParaMostrarAsync(campo, valor, cancellationToken)));
            }
        }

        // El estudiante ve "Vos" o "Bedelía"; la administración ve quién hizo cada cambio.
        string Actor(Guid actorId) => vistaAdministracion
            ? personas.GetValueOrDefault(actorId)?.NombreParaMostrar ?? "Usuario eliminado"
            : actorId == usuarioActualId ? "Vos" : "Bedelía";

        var historial = tramite.Eventos
            .OrderBy(evento => evento.Fecha)
            .ThenBy(evento => evento.Id)
            .Select(evento => new EventoTramiteDto(evento.EstadoAnterior, evento.EstadoNuevo, evento.Comentario, Actor(evento.ActorUsuarioId), evento.Fecha))
            .ToList();

        return new TramiteDetalleDto(
            tramite.Id,
            NumeroTramite.Formatear(tramite.Numero),
            tipo.Id,
            tipo.Nombre,
            tramite.Estado,
            tramite.FechaSolicitud,
            tramite.UpdatedAt,
            tramite.ObservacionAdministrativa,
            personas[tramite.UsuarioId],
            vistaAdministracion && tramite.AdministradorAsignadoId is { } adminId ? personas.GetValueOrDefault(adminId)?.NombreParaMostrar : null,
            datos,
            historial);
    }

    private async Task<string> ValorParaMostrarAsync(CampoTramite campo, string valor, CancellationToken cancellationToken) =>
        campo.Tipo switch
        {
            TipoCampo.Carrera => (await academico.ObtenerCarreraAsync(Guid.Parse(valor), cancellationToken))?.Nombre ?? "Carrera eliminada",
            TipoCampo.Fecha => DateOnly.ParseExact(valor, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                .ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
            _ => valor,
        };
}
