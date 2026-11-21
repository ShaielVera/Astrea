using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TramitesSoloConConstanciaYMetricas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El analítico parcial y la justificación de inasistencia nunca se activaron. Se borran solo si nadie los
            // pidió ni tienen plantilla: si los hubiera, quedan para no perder el historial.
            migrationBuilder.Sql(
                """
                DELETE FROM tramite_tipos t
                WHERE t.id IN ('0193a5d0-0000-7000-8000-000000000003', '0193a5d0-0000-7000-8000-000000000004')
                  AND NOT EXISTS (SELECT 1 FROM tramites r WHERE r.tramite_tipo_id = t.id)
                  AND NOT EXISTS (SELECT 1 FROM plantillas_documento p WHERE p.tramite_tipo_id = t.id);
                """);

            migrationBuilder.AddColumn<bool>(
                name: "responde_aclaracion",
                table: "consultas_asistente",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000001"),
                column: "campos",
                value: "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null,\"sinFechasFuturas\":false},{\"clave\":\"presentarAnte\",\"etiqueta\":\"Para presentar ante\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":\"Ej.: ANSES, empleador, obra social.\",\"sinFechasFuturas\":false}]");

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000002"),
                column: "campos",
                value: "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null,\"sinFechasFuturas\":false},{\"clave\":\"materia\",\"etiqueta\":\"Materia\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":200,\"ayuda\":null,\"sinFechasFuturas\":false},{\"clave\":\"fechaExamen\",\"etiqueta\":\"Fecha del examen\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null,\"sinFechasFuturas\":true},{\"clave\":\"presentarAnte\",\"etiqueta\":\"Para presentar ante\",\"tipo\":1,\"obligatorio\":false,\"largoMaximo\":120,\"ayuda\":null,\"sinFechasFuturas\":false}]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "responde_aclaracion",
                table: "consultas_asistente");

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000001"),
                column: "campos",
                value: "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"presentarAnte\",\"etiqueta\":\"Para presentar ante\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":\"Ej.: ANSES, empleador, obra social.\"}]");

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000002"),
                column: "campos",
                value: "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"materia\",\"etiqueta\":\"Materia\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":200,\"ayuda\":null},{\"clave\":\"fechaExamen\",\"etiqueta\":\"Fecha del examen\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"presentarAnte\",\"etiqueta\":\"Para presentar ante\",\"tipo\":1,\"obligatorio\":false,\"largoMaximo\":120,\"ayuda\":null}]");

            migrationBuilder.InsertData(
                table: "tramite_tipos",
                columns: new[] { "id", "activo", "campos", "codigo", "descripcion", "nombre", "requiere_adjunto" },
                values: new object[,]
                {
                    { new Guid("0193a5d0-0000-7000-8000-000000000003"), false, "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"motivo\",\"etiqueta\":\"\\u00BFPara qu\\u00E9 lo necesit\\u00E1s?\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":200,\"ayuda\":\"Ej.: pase a otra instituci\\u00F3n, beca.\"}]", "ANALITICO_PARCIAL", "Detalle de las materias aprobadas hasta hoy, con nota y fecha.", "Certificado analítico parcial", false },
                    { new Guid("0193a5d0-0000-7000-8000-000000000004"), false, "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"desde\",\"etiqueta\":\"Desde\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"hasta\",\"etiqueta\":\"Hasta\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"motivo\",\"etiqueta\":\"Motivo\",\"tipo\":2,\"obligatorio\":true,\"largoMaximo\":500,\"ayuda\":null}]", "JUSTIFICACION_INASISTENCIA", "Pedí que se justifiquen tus faltas por un motivo de salud, laboral u otro previsto en el reglamento. Presentá el comprobante original en Secretaría.", "Justificación de inasistencia", false }
                });
        }
    }
}
