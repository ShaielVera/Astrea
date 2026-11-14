using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoAcademico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carreras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duracion_anios = table.Column<int>(type: "integer", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carreras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "materias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planes_estudio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrera_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duracion_anios = table.Column<int>(type: "integer", nullable: false),
                    vigencia_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    vigencia_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planes_estudio", x => x.id);
                    table.ForeignKey(
                        name: "fk_planes_estudio_carreras_carrera_id",
                        column: x => x.carrera_id,
                        principalTable: "carreras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_materias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_estudio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio_en_plan = table.Column<int>(type: "integer", nullable: false),
                    cuatrimestre = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    carga_horaria_semanal = table.Column<int>(type: "integer", nullable: true),
                    obligatoria = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_materias", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_materias_materias_materia_id",
                        column: x => x.materia_id,
                        principalTable: "materias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_materias_planes_estudio_plan_estudio_id",
                        column: x => x.plan_estudio_id,
                        principalTable: "planes_estudio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_carreras_codigo",
                table: "carreras",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carreras_nombre",
                table: "carreras",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "ix_materias_codigo",
                table: "materias",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_materias_materia_id",
                table: "plan_materias",
                column: "materia_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_materias_plan_estudio_id_anio_en_plan_orden",
                table: "plan_materias",
                columns: new[] { "plan_estudio_id", "anio_en_plan", "orden" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_materias_plan_estudio_id_materia_id",
                table: "plan_materias",
                columns: new[] { "plan_estudio_id", "materia_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planes_estudio_carrera_id",
                table: "planes_estudio",
                column: "carrera_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_materias");

            migrationBuilder.DropTable(
                name: "materias");

            migrationBuilder.DropTable(
                name: "planes_estudio");

            migrationBuilder.DropTable(
                name: "carreras");
        }
    }
}
