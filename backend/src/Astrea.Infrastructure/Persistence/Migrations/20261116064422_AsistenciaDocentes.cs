using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsistenciaDocentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asignaciones_docentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asignada_por = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_asignacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asignaciones_docentes", x => x.id);
                    table.ForeignKey(
                        name: "fk_asignaciones_docentes_plan_materias_plan_materia_id",
                        column: x => x.plan_materia_id,
                        principalTable: "plan_materias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asignaciones_docentes_usuarios_docente_id",
                        column: x => x.docente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inscripciones_cursada",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estudiante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_lectivo = table.Column<int>(type: "integer", nullable: false),
                    fecha_inscripcion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscripciones_cursada", x => x.id);
                    table.ForeignKey(
                        name: "fk_inscripciones_cursada_plan_materias_plan_materia_id",
                        column: x => x.plan_materia_id,
                        principalTable: "plan_materias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inscripciones_cursada_usuarios_estudiante_id",
                        column: x => x.estudiante_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relevamientos_asistencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ciclo_lectivo = table.Column<int>(type: "integer", nullable: false),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cerrado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relevamientos_asistencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_relevamientos_asistencia_usuarios_creado_por",
                        column: x => x.creado_por,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "confirmaciones_asistencia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    relevamiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estudiante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    observacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_confirmaciones_asistencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_confirmaciones_asistencia_plan_materias_plan_materia_id",
                        column: x => x.plan_materia_id,
                        principalTable: "plan_materias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_confirmaciones_asistencia_relevamientos_asistencia_relevami",
                        column: x => x.relevamiento_id,
                        principalTable: "relevamientos_asistencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_confirmaciones_asistencia_usuarios_docente_id",
                        column: x => x.docente_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_confirmaciones_asistencia_usuarios_estudiante_id",
                        column: x => x.estudiante_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "codigo", "descripcion", "nombre" },
                values: new object[] { new Guid("0a6a8f1e-4c5b-4d1e-9b77-2f6c1b9d0e03"), "DOCENTE", "Profesor: confirma la asistencia de los estudiantes de las materias que tiene asignadas.", "Docente" });

            migrationBuilder.CreateIndex(
                name: "ix_asignaciones_docentes_docente_id_plan_materia_id",
                table: "asignaciones_docentes",
                columns: new[] { "docente_id", "plan_materia_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asignaciones_docentes_plan_materia_id",
                table: "asignaciones_docentes",
                column: "plan_materia_id");

            migrationBuilder.CreateIndex(
                name: "ix_confirmaciones_asistencia_docente_id",
                table: "confirmaciones_asistencia",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_confirmaciones_asistencia_estudiante_id",
                table: "confirmaciones_asistencia",
                column: "estudiante_id");

            migrationBuilder.CreateIndex(
                name: "ix_confirmaciones_asistencia_plan_materia_id",
                table: "confirmaciones_asistencia",
                column: "plan_materia_id");

            migrationBuilder.CreateIndex(
                name: "ix_confirmaciones_asistencia_relevamiento_id_plan_materia_id_e",
                table: "confirmaciones_asistencia",
                columns: new[] { "relevamiento_id", "plan_materia_id", "estudiante_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscripciones_cursada_estudiante_id_plan_materia_id_ciclo_l",
                table: "inscripciones_cursada",
                columns: new[] { "estudiante_id", "plan_materia_id", "ciclo_lectivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscripciones_cursada_plan_materia_id_ciclo_lectivo",
                table: "inscripciones_cursada",
                columns: new[] { "plan_materia_id", "ciclo_lectivo" });

            migrationBuilder.CreateIndex(
                name: "ix_relevamientos_asistencia_creado_en",
                table: "relevamientos_asistencia",
                column: "creado_en");

            migrationBuilder.CreateIndex(
                name: "ix_relevamientos_asistencia_creado_por",
                table: "relevamientos_asistencia",
                column: "creado_por");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignaciones_docentes");

            migrationBuilder.DropTable(
                name: "confirmaciones_asistencia");

            migrationBuilder.DropTable(
                name: "inscripciones_cursada");

            migrationBuilder.DropTable(
                name: "relevamientos_asistencia");

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0a6a8f1e-4c5b-4d1e-9b77-2f6c1b9d0e03"));
        }
    }
}
