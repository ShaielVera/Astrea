using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tramites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "tramite_numero_seq");

            migrationBuilder.CreateTable(
                name: "tramite_tipos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    requiere_adjunto = table.Column<bool>(type: "boolean", nullable: false),
                    campos = table.Column<string>(type: "jsonb", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tramite_tipos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tramites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('tramite_numero_seq')"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tramite_tipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datos = table.Column<string>(type: "jsonb", nullable: false),
                    clave_idempotencia = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    observacion_administrativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    administrador_asignado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_solicitud = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tramites", x => x.id);
                    table.ForeignKey(
                        name: "fk_tramites_tramite_tipos_tramite_tipo_id",
                        column: x => x.tramite_tipo_id,
                        principalTable: "tramite_tipos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tramites_usuarios_administrador_asignado_id",
                        column: x => x.administrador_asignado_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tramites_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tramite_eventos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tramite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    estado_nuevo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tramite_eventos", x => x.id);
                    table.ForeignKey(
                        name: "fk_tramite_eventos_tramites_tramite_id",
                        column: x => x.tramite_id,
                        principalTable: "tramites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tramite_eventos_usuarios_actor_usuario_id",
                        column: x => x.actor_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "tramite_tipos",
                columns: new[] { "id", "activo", "campos", "codigo", "descripcion", "nombre", "requiere_adjunto" },
                values: new object[,]
                {
                    { new Guid("0193a5d0-0000-7000-8000-000000000001"), true, "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"presentarAnte\",\"etiqueta\":\"Para presentar ante\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":\"Ej.: ANSES, empleador, obra social.\"}]", "ALUMNO_REGULAR", "Certifica que sos estudiante regular del instituto, para presentar en el trabajo, ANSES, obra social u otros organismos.", "Constancia de alumno regular", false },
                    { new Guid("0193a5d0-0000-7000-8000-000000000002"), true, "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"materia\",\"etiqueta\":\"Materia\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":200,\"ayuda\":null},{\"clave\":\"fechaExamen\",\"etiqueta\":\"Fecha del examen\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"presentarAnte\",\"etiqueta\":\"Para presentar ante\",\"tipo\":1,\"obligatorio\":false,\"largoMaximo\":120,\"ayuda\":null}]", "CONSTANCIA_EXAMEN", "Certifica que te presentaste a rendir un examen final, para justificar la ausencia en el trabajo.", "Constancia de examen", false },
                    { new Guid("0193a5d0-0000-7000-8000-000000000003"), true, "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"motivo\",\"etiqueta\":\"\\u00BFPara qu\\u00E9 lo necesit\\u00E1s?\",\"tipo\":1,\"obligatorio\":true,\"largoMaximo\":200,\"ayuda\":\"Ej.: pase a otra instituci\\u00F3n, beca.\"}]", "ANALITICO_PARCIAL", "Detalle de las materias aprobadas hasta hoy, con nota y fecha.", "Certificado analítico parcial", false },
                    { new Guid("0193a5d0-0000-7000-8000-000000000004"), true, "[{\"clave\":\"carrera\",\"etiqueta\":\"Carrera\",\"tipo\":4,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"desde\",\"etiqueta\":\"Desde\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"hasta\",\"etiqueta\":\"Hasta\",\"tipo\":3,\"obligatorio\":true,\"largoMaximo\":120,\"ayuda\":null},{\"clave\":\"motivo\",\"etiqueta\":\"Motivo\",\"tipo\":2,\"obligatorio\":true,\"largoMaximo\":500,\"ayuda\":null}]", "JUSTIFICACION_INASISTENCIA", "Pedí que se justifiquen tus faltas por un motivo de salud, laboral u otro previsto en el reglamento. Presentá el comprobante original en Bedelía.", "Justificación de inasistencia", false }
                });

            migrationBuilder.CreateIndex(
                name: "ix_tramite_eventos_actor_usuario_id",
                table: "tramite_eventos",
                column: "actor_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_tramite_eventos_tramite_id_fecha",
                table: "tramite_eventos",
                columns: new[] { "tramite_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_tramite_tipos_codigo",
                table: "tramite_tipos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tramites_administrador_asignado_id",
                table: "tramites",
                column: "administrador_asignado_id");

            migrationBuilder.CreateIndex(
                name: "ix_tramites_estado_fecha_solicitud",
                table: "tramites",
                columns: new[] { "estado", "fecha_solicitud" });

            migrationBuilder.CreateIndex(
                name: "ix_tramites_numero",
                table: "tramites",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tramites_tramite_tipo_id",
                table: "tramites",
                column: "tramite_tipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_tramites_usuario_id_clave_idempotencia",
                table: "tramites",
                columns: new[] { "usuario_id", "clave_idempotencia" },
                unique: true,
                filter: "clave_idempotencia IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tramites_usuario_id_fecha_solicitud",
                table: "tramites",
                columns: new[] { "usuario_id", "fecha_solicitud" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tramite_eventos");

            migrationBuilder.DropTable(
                name: "tramites");

            migrationBuilder.DropTable(
                name: "tramite_tipos");

            migrationBuilder.DropSequence(
                name: "tramite_numero_seq");
        }
    }
}
