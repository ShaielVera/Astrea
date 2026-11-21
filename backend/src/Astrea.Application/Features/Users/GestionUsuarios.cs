using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Attendance;
using Astrea.Application.Features.Auth;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Users;

/// <summary>Una cuenta en "Usuarios y roles". <see cref="Materias"/> solo trae datos si es docente.</summary>
public sealed record CuentaDto(
    Guid Id,
    string Nombre,
    string Email,
    string? Dni,
    IReadOnlyList<string> Roles,
    DateTimeOffset? UltimoIngresoEn,
    DateTimeOffset CreadaEn,
    bool Activa,
    IReadOnlyList<AsignacionDto> Materias);

public static class ErroresUsuarios
{
    public const int LargoMaximoTexto = 100;

    public static readonly Error NoEncontrado = Error.NoEncontrado("usuario_no_encontrado", "El usuario no existe o está desactivado.");

    public static readonly Error RolInvalido = Error.Validacion("rol_invalido", "Solo se pueden dar o quitar los roles de administrador y docente.");

    public static readonly Error QuitarseAdministrador = Error.Conflicto(
        "quitarse_administrador",
        "No podés quitarte a vos mismo el rol de administrador: pedíselo a otro administrador.");

    public static readonly Error UltimoAdministrador = Error.Conflicto(
        "ultimo_administrador",
        "Es el único administrador: dale el rol a otra persona antes de quitárselo.");

    public static readonly Error TextoDemasiadoLargo =
        Error.Validacion("texto_demasiado_largo", $"La búsqueda no puede superar los {LargoMaximoTexto} caracteres.");
}

/// <summary>
/// "Usuarios y roles": Administración ve todas las cuentas (las que entraron más recientemente primero), las busca por
/// apellido, nombre o DNI y les da o quita los roles de administrador y docente. Las materias de cada docente se asignan
/// con <see cref="GestionDocentes"/>.
/// </summary>
public sealed class GestionUsuarios(
    UsuarioActual usuarioActual,
    IUserRepository usuarios,
    IAsistenciaRepository asistencia,
    GestionDocentes docentes,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<PagedResult<CuentaDto>>> ListarAsync(string? texto, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto?.Length > ErroresUsuarios.LargoMaximoTexto)
        {
            return ErroresUsuarios.TextoDemasiadoLargo;
        }

        var pagina = PageRequest.Create(page, pageSize);
        var cuentas = await usuarios.ListarCuentasAsync(texto, pagina, cancellationToken);

        var materias = cuentas.Items.Any(usuario => usuario.TieneRol(Rol.Docente))
            ? (await asistencia.ListarDocentesAsync(cancellationToken)).ToDictionary(docente => docente.Id, docente => docente.Materias)
            : [];

        var items = cuentas.Items
            .Select(usuario => new CuentaDto(
                usuario.Id,
                usuario.NombreParaMostrar,
                usuario.Email,
                usuario.Dni,
                usuario.CodigosDeRol(),
                usuario.UltimoIngresoEn,
                usuario.CreatedAt,
                usuario.Activo,
                materias.GetValueOrDefault(usuario.Id) ?? []))
            .ToList();
        return PagedResult<CuentaDto>.From(items, pagina, cuentas.TotalItems);
    }

    public async Task<Result<bool>> DarRolAsync(Guid usuarioId, string codigoRol, CancellationToken cancellationToken)
    {
        var rol = RolEditable(codigoRol);
        if (rol is null)
        {
            return ErroresUsuarios.RolInvalido;
        }

        if (rol == Rol.Docente)
        {
            return await docentes.HacerDocenteAsync(usuarioId, cancellationToken);
        }

        var admin = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!admin.EsExitoso)
        {
            return admin.Error!;
        }

        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is not { Activo: true })
        {
            return ErroresUsuarios.NoEncontrado;
        }

        usuario.AsignarRol(rol, admin.Valor.Id, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }

    public async Task<Result<bool>> QuitarRolAsync(Guid usuarioId, string codigoRol, CancellationToken cancellationToken)
    {
        var rol = RolEditable(codigoRol);
        if (rol is null)
        {
            return ErroresUsuarios.RolInvalido;
        }

        if (rol == Rol.Docente)
        {
            return await docentes.QuitarDocenteAsync(usuarioId, cancellationToken);
        }

        var admin = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!admin.EsExitoso)
        {
            return admin.Error!;
        }

        if (admin.Valor.Id == usuarioId)
        {
            return ErroresUsuarios.QuitarseAdministrador;
        }

        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ErroresUsuarios.NoEncontrado;
        }

        if (!usuario.TieneRol(Rol.Administrador))
        {
            return true;
        }

        var activos = await usuarios.ContarActivosPorRolAsync(cancellationToken);
        if (usuario.Activo && activos.GetValueOrDefault(Rol.CodigoAdministrador) <= 1)
        {
            return ErroresUsuarios.UltimoAdministrador;
        }

        usuario.QuitarRol(rol, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return true;
    }

    private static Rol? RolEditable(string codigo) =>
        codigo.Trim().ToUpperInvariant() switch
        {
            Rol.CodigoAdministrador => Rol.Administrador,
            Rol.CodigoDocente => Rol.Docente,
            _ => null,
        };
}
