using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentosEmitidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plantillas_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tramite_tipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cuerpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    creada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantillas_documento", x => x.id);
                    table.ForeignKey(
                        name: "fk_plantillas_documento_tramite_tipos_tramite_tipo_id",
                        column: x => x.tramite_tipo_id,
                        principalTable: "tramite_tipos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos_emitidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tramite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_plantilla = table.Column<int>(type: "integer", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    codigo_validacion = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    dni_ultimos_digitos = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    fecha_emision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    motivo_anulacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    anulado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anulado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_emitidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_emitidos_plantillas_documento_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "plantillas_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_emitidos_tramites_tramite_id",
                        column: x => x.tramite_id,
                        principalTable: "tramites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_emitidos_usuarios_anulado_por_usuario_id",
                        column: x => x.anulado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_emitidos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "plantillas_documento",
                columns: new[] { "id", "creada_at", "cuerpo", "titulo", "tramite_tipo_id", "version" },
                values: new object[,]
                {
                    { new Guid("0193a5d0-0000-7000-8000-000000000101"), new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Se deja constancia de que {{nombreCompleto}}, DNI {{dni}}, es estudiante regular de la carrera {{carrera}} en este Instituto durante el ciclo lectivo en curso.\n\nA pedido del interesado o la interesada, y para ser presentada ante {{presentarAnte}}, se extiende la presente el {{fechaEmision}}.", "Constancia de alumno regular", new Guid("0193a5d0-0000-7000-8000-000000000001"), 1 },
                    { new Guid("0193a5d0-0000-7000-8000-000000000102"), new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Se deja constancia de que {{nombreCompleto}}, DNI {{dni}}, estudiante de la carrera {{carrera}}, se presentó a rendir el examen final de {{materia}} el día {{fechaExamen}} en este Instituto.\n\nA pedido del interesado o la interesada, y para ser presentada ante {{presentarAnte|quien corresponda}}, se extiende la presente el {{fechaEmision}}.", "Constancia de examen", new Guid("0193a5d0-0000-7000-8000-000000000002"), 1 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_emitidos_anulado_por_usuario_id",
                table: "documentos_emitidos",
                column: "anulado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_emitidos_codigo_validacion",
                table: "documentos_emitidos",
                column: "codigo_validacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_emitidos_plantilla_id",
                table: "documentos_emitidos",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_emitidos_tramite_id",
                table: "documentos_emitidos",
                column: "tramite_id",
                unique: true,
                filter: "estado = 'Vigente'");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_emitidos_usuario_id",
                table: "documentos_emitidos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_plantillas_documento_tramite_tipo_id_version",
                table: "plantillas_documento",
                columns: new[] { "tramite_tipo_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos_emitidos");

            migrationBuilder.DropTable(
                name: "plantillas_documento");
        }
    }
}
