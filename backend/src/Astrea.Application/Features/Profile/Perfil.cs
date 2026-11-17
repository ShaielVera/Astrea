using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Notifications;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Domain.Users;

namespace Astrea.Application.Features.Profile;

/// <summary>Un dato de contacto y su validación (vence al año).</summary>
public sealed record ContactoDto(string? Valor, bool Verificado, DateTimeOffset? VerificadoEl, DateTimeOffset? VenceEl, bool Vencido);

public sealed record MiPerfilDto(
    string EmailCuenta,
    string? Nombre,
    string? Apellido,
    string? Dni,
    string? Legajo,
    bool DatosConfirmados,
    ContactoDto EmailContacto,
    ContactoDto Celular,
    bool Completo);

public sealed record DatosPersonalesCommand(string Nombre, string Apellido, string Dni);

public sealed record ContactoCommand(string? EmailContacto, string? Celular);

public sealed record CodigoEnviadoDto(string Destino, DateTimeOffset VenceEl, bool Simulado);

public sealed record ValidarCodigoCommand(string Codigo);

public static class ErroresPerfil
{
    public static readonly Error DniEnUso =
        Error.Conflicto("dni_en_uso", "Ese DNI ya figura en otra cuenta de Astrea. Si es tuyo, consultalo en Secretaría.");

    public static readonly Error SinDato =
        Error.Validacion("contacto_vacio", "Primero guardá el dato que querés validar.");

    public static Error EsperarParaReenviar(int segundos) =>
        Error.Conflicto("esperar_para_reenviar", $"Ya te mandamos un código hace un momento. Esperá {segundos} segundos para pedir otro.");

    public static readonly Error SinCodigo =
        Error.Validacion("sin_codigo", "Pedí un código primero.");

    public static Error CodigoIncorrecto(int restantes) =>
        Error.Validacion("codigo_incorrecto", $"El código no es correcto. Te quedan {restantes} {(restantes == 1 ? "intento" : "intentos")}.");

    public static readonly Error CodigoVencido =
        Error.Validacion("codigo_vencido", "El código venció (dura 10 minutos). Pedí uno nuevo.");

    public static readonly Error CodigoSinIntentos =
        Error.Validacion("codigo_sin_intentos", "Se agotaron los intentos para ese código. Pedí uno nuevo.");

    public static Error CanalNoConfigurado(CanalContacto canal) => Error.NoDisponible(
        "canal_no_configurado",
        canal == CanalContacto.Email
            ? "Todavía no está configurado el envío de emails de Astrea. Avisale a Secretaría."
            : "Todavía no está configurado el envío de SMS. Por ahora, el celular queda sin validar.");
}

/// <summary>
/// "Mi perfil": el estudiante completa sus datos (Secretaría los confirma después) y valida su email y celular de contacto
/// con un código. La validación dura un año: después se pide renovarla por si cambió de número o de email.
/// </summary>
public sealed class Perfil(UsuarioActual usuarioActual, IUserRepository usuarios, IEnviadorCodigos enviador, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<MiPerfilDto>> ObtenerAsync(CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        return usuario.EsExitoso ? ADto(usuario.Valor, clock.UtcNow) : usuario.Error!;
    }

    public async Task<Result<MiPerfilDto>> GuardarDatosAsync(DatosPersonalesCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var dni = Usuario.NormalizarDni(comando.Dni);
        if (await usuarios.ExisteOtroConDniAsync(dni, usuario.Valor.Id, cancellationToken))
        {
            return ErroresPerfil.DniEnUso;
        }

        usuario.Valor.CompletarDatosPersonales(comando.Nombre, comando.Apellido, dni, clock.UtcNow);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(usuario.Valor, clock.UtcNow);
    }

    public async Task<Result<MiPerfilDto>> GuardarContactoAsync(ContactoCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var ahora = clock.UtcNow;
        usuario.Valor.CambiarEmailContacto(comando.EmailContacto, ahora);
        usuario.Valor.CambiarCelular(comando.Celular, ahora);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return ADto(usuario.Valor, ahora);
    }

    /// <summary>Manda un código nuevo al dato guardado. Entre dos envíos del mismo canal hay que esperar un minuto.</summary>
    public async Task<Result<CodigoEnviadoDto>> EnviarCodigoAsync(CanalContacto canal, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var destino = usuario.Valor.ContactoDe(canal);
        if (destino is null)
        {
            return ErroresPerfil.SinDato;
        }

        var ahora = clock.UtcNow;
        if (await usuarios.UltimoCodigoAsync(usuario.Valor.Id, canal, cancellationToken) is { } anterior
            && ahora - anterior.CreadoEn < CodigoVerificacion.EsperaEntreEnvios)
        {
            var faltan = (int)Math.Ceiling((CodigoVerificacion.EsperaEntreEnvios - (ahora - anterior.CreadoEn)).TotalSeconds);
            return ErroresPerfil.EsperarParaReenviar(faltan);
        }

        var (codigo, enClaro) = CodigoVerificacion.Generar(usuario.Valor.Id, canal, destino, ahora);
        var entrega = await enviador.EnviarAsync(canal, destino, enClaro, cancellationToken);
        if (entrega == EntregaCodigo.NoConfigurado)
        {
            return ErroresPerfil.CanalNoConfigurado(canal);
        }

        usuarios.Agregar(codigo);
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return new CodigoEnviadoDto(Enmascarar(canal, destino), codigo.VenceEn, entrega == EntregaCodigo.Simulado);
    }

    public async Task<Result<MiPerfilDto>> ValidarCodigoAsync(CanalContacto canal, ValidarCodigoCommand comando, CancellationToken cancellationToken)
    {
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EsExitoso)
        {
            return usuario.Error!;
        }

        var codigo = await usuarios.UltimoCodigoAsync(usuario.Valor.Id, canal, cancellationToken);
        if (codigo is null || codigo.Destino != usuario.Valor.ContactoDe(canal))
        {
            return ErroresPerfil.SinCodigo;
        }

        var ahora = clock.UtcNow;
        var resultado = codigo.Validar(comando.Codigo, ahora);
        if (resultado == ResultadoCodigo.Correcto)
        {
            usuario.Valor.MarcarContactoVerificado(canal, codigo.Destino, ahora);
        }

        // Se guarda también un intento fallido: los intentos se cuentan aunque el código no sea correcto.
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return resultado switch
        {
            ResultadoCodigo.Correcto => ADto(usuario.Valor, ahora),
            ResultadoCodigo.Vencido => ErroresPerfil.CodigoVencido,
            ResultadoCodigo.SinIntentos => ErroresPerfil.CodigoSinIntentos,
            _ => ErroresPerfil.CodigoIncorrecto(codigo.IntentosRestantes),
        };
    }

    public static MiPerfilDto ADto(Usuario usuario, DateTimeOffset ahora)
    {
        var email = Contacto(usuario, CanalContacto.Email, ahora);
        var celular = Contacto(usuario, CanalContacto.Celular, ahora);
        return new MiPerfilDto(
            usuario.Email,
            usuario.Nombre,
            usuario.Apellido,
            usuario.Dni,
            usuario.Legajo,
            usuario.DatosConfirmados,
            email,
            celular,
            usuario.TieneDatosOficiales && email.Verificado && !email.Vencido && celular.Valor is not null);
    }

    private static ContactoDto Contacto(Usuario usuario, CanalContacto canal, DateTimeOffset ahora)
    {
        var verificado = usuario.ContactoVerificadoEn(canal);
        return new ContactoDto(
            usuario.ContactoDe(canal),
            verificado is not null,
            verificado,
            verificado + Usuario.VigenciaContacto,
            verificado is not null && !usuario.ContactoVigente(canal, ahora));
    }

    /// <summary>"ju•••@gmail.com" / "•••• ••3456": confirma a dónde se mandó sin repetir el dato entero.</summary>
    private static string Enmascarar(CanalContacto canal, string destino)
    {
        if (canal == CanalContacto.Celular)
        {
            return $"•••• ••{destino[^4..]}";
        }

        var arroba = destino.IndexOf('@', StringComparison.Ordinal);
        return $"{destino[..Math.Min(2, arroba)]}•••{destino[arroba..]}";
    }
}
