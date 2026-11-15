namespace Astrea.Domain.Procedures;

/// <summary>
/// Catálogo inicial de trámites del SDD. Los ids son fijos porque se cargan con la migración
/// (todas las bases, incluidas las de prueba, tienen el mismo catálogo).
/// </summary>
public static class CatalogoTramites
{
    public static readonly Guid AlumnoRegularId = new("0193a5d0-0000-7000-8000-000000000001");
    public static readonly Guid ConstanciaExamenId = new("0193a5d0-0000-7000-8000-000000000002");
    public static readonly Guid AnaliticoParcialId = new("0193a5d0-0000-7000-8000-000000000003");
    public static readonly Guid JustificacionInasistenciaId = new("0193a5d0-0000-7000-8000-000000000004");

    private static readonly CampoTramite Carrera = new("carrera", "Carrera", TipoCampo.Carrera, true);

    public static IReadOnlyList<TramiteTipo> Tipos() =>
    [
        TramiteTipo.Crear(
            AlumnoRegularId,
            "ALUMNO_REGULAR",
            "Constancia de alumno regular",
            "Certifica que sos estudiante regular del instituto, para presentar en el trabajo, ANSES, obra social u otros organismos.",
            [Carrera, new("presentarAnte", "Para presentar ante", TipoCampo.Texto, true, 120, "Ej.: ANSES, empleador, obra social.")]),
        TramiteTipo.Crear(
            ConstanciaExamenId,
            "CONSTANCIA_EXAMEN",
            "Constancia de examen",
            "Certifica que te presentaste a rendir un examen final, para justificar la ausencia en el trabajo.",
            [
                Carrera,
                new("materia", "Materia", TipoCampo.Texto, true, 200),
                new("fechaExamen", "Fecha del examen", TipoCampo.Fecha, true),
                new("presentarAnte", "Para presentar ante", TipoCampo.Texto, false, 120),
            ]),
        TramiteTipo.Crear(
            AnaliticoParcialId,
            "ANALITICO_PARCIAL",
            "Certificado analítico parcial",
            "Detalle de las materias aprobadas hasta hoy, con nota y fecha.",
            [Carrera, new("motivo", "¿Para qué lo necesitás?", TipoCampo.Texto, true, 200, "Ej.: pase a otra institución, beca.")]),
        TramiteTipo.Crear(
            JustificacionInasistenciaId,
            "JUSTIFICACION_INASISTENCIA",
            "Justificación de inasistencia",
            "Pedí que se justifiquen tus faltas por un motivo de salud, laboral u otro previsto en el reglamento. Presentá el comprobante original en Bedelía.",
            [
                Carrera,
                new("desde", "Desde", TipoCampo.Fecha, true),
                new("hasta", "Hasta", TipoCampo.Fecha, true),
                new("motivo", "Motivo", TipoCampo.TextoLargo, true, 500),
            ]),
    ];
}
