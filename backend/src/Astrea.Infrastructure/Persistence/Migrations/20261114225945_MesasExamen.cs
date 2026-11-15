using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MesasExamen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "mesas_examen",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    llamado = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    hora = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    aula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tribunal = table.Column<string[]>(type: "text[]", nullable: false),
                    inscripcion_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    inscripcion_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publicada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mesas_examen", x => x.id);
                    table.ForeignKey(
                        name: "fk_mesas_examen_plan_materias_plan_materia_id",
                        column: x => x.plan_materia_id,
                        principalTable: "plan_materias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mesas_examen_estado_fecha_hora",
                table: "mesas_examen",
                columns: new[] { "estado", "fecha", "hora" });

            migrationBuilder.CreateIndex(
                name: "ix_mesas_examen_plan_materia_id_fecha",
                table: "mesas_examen",
                columns: new[] { "plan_materia_id", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mesas_examen");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
