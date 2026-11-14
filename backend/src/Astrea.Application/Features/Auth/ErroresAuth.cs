using Astrea.Application.Contracts.Results;

namespace Astrea.Application.Features.Auth;

public static class ErroresAuth
{
    public static readonly Error SinIdentidad =
        Error.NoAutenticado("auth.sin_identidad", "No hay una sesión de Firebase válida.");

    public static readonly Error SinEmail =
        Error.Validacion("auth.sin_email", "La cuenta de Firebase no tiene un email asociado.");

    public static readonly Error NoRegistrado =
        Error.NoEncontrado("auth.no_registrado", "El usuario todavía no inició sesión en Astrea.");

    public static readonly Error CuentaDesactivada =
        Error.Prohibido("auth.cuenta_desactivada", "La cuenta está desactivada. Consultá en Secretaría.");
}
