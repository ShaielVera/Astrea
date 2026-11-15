namespace Astrea.Application.Abstractions.Identity;

/// <summary>
/// Configuración de identidad. <see cref="AdministradoresIniciales"/> son los emails que reciben el rol
/// Administrador al iniciar sesión, siempre que Firebase haya verificado el email.
/// Sirve para dar de alta al primer personal de Secretaría sin un panel previo.
/// </summary>
public sealed class ConfiguracionIdentidad
{
    public const string Seccion = "Identidad";

    public IReadOnlyCollection<string> AdministradoresIniciales { get; init; } = [];

    public bool EsAdministradorInicial(string email) =>
        AdministradoresIniciales.Any(admin => string.Equals(admin.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
