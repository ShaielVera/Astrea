namespace Astrea.Domain.Procedures;

/// <summary>
/// Catálogo inicial de trámites del SDD. Los ids son fijos porque se cargan con la migración
/// (todas las bases, incluidas las de prueba, tienen el mismo catálogo).
/// </summary>
public static class CatalogoTramites
{
    public static readonly Guid AlumnoRegularId = new("0193a5d0-0000-7000-8000-000000000001");
    public static readonly Guid ConstanciaExamenId = new("0193a5d0-0000-7000-8000-000000000002");

    public static readonly Guid PlantillaAlumnoRegularV1Id = new("0193a5d0-0000-7000-8000-000000000101");
    public static readonly Guid PlantillaConstanciaExamenV1Id = new("0193a5d0-0000-7000-8000-000000000102");

    private static readonly CampoTramite Carrera = new("carrera", "Carrera", TipoCampo.Carrera, true);

    /// <summary>
    /// Versión 1 de las plantillas de los dos trámites.
    /// </summary>
    public static IReadOnlyList<PlantillaDocumento> PlantillasIniciales()
    {
        var tipos = Tipos().ToDictionary(tipo => tipo.Id);
        var fecha = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        return
        [
            PlantillaDocumento.Crear(
                PlantillaAlumnoRegularV1Id,
                tipos[AlumnoRegularId],
                1,
                "Constancia de alumno regular",
                "Se deja constancia de que {{nombreCompleto}}, DNI {{dni}}, es estudiante regular de la carrera " +
                "{{carrera}} en este Instituto durante el ciclo lectivo en curso.\n\n" +
                "A pedido del interesado o la interesada, y para ser presentada ante {{presentarAnte}}, " +
                "se extiende la presente el {{fechaEmision}}.",
                fecha),
            PlantillaDocumento.Crear(
                PlantillaConstanciaExamenV1Id,
                tipos[ConstanciaExamenId],
                1,
                "Constancia de examen",
                "Se deja constancia de que {{nombreCompleto}}, DNI {{dni}}, estudiante de la carrera {{carrera}}, " +
                "se presentó a rendir el examen final de {{materia}} el día {{fechaExamen}} en este Instituto.\n\n" +
                "A pedido del interesado o la interesada, y para ser presentada ante {{presentarAnte|quien corresponda}}, " +
                "se extiende la presente el {{fechaEmision}}.",
                fecha),
        ];
    }

    /// <summary>Los trámites que se piden por Astrea: los dos que emiten una constancia en PDF.</summary>
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
                new("fechaExamen", "Fecha del examen", TipoCampo.Fecha, true, SinFechasFuturas: true),
                new("presentarAnte", "Para presentar ante", TipoCampo.Texto, false, 120),
            ]),
    ];
}
