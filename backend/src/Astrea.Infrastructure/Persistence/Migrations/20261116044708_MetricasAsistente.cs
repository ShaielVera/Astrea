using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MetricasAsistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consultas_asistente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dia = table.Column<DateOnly>(type: "date", nullable: false),
                    tema = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    con_propuesta = table.Column<bool>(type: "boolean", nullable: false),
                    fuentes_documentos = table.Column<int>(type: "integer", nullable: false),
                    fuentes_datos = table.Column<int>(type: "integer", nullable: false),
                    herramientas = table.Column<List<string>>(type: "text[]", nullable: false),
                    duracion_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultas_asistente", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consultas_asistente_dia",
                table: "consultas_asistente",
                column: "dia");

            migrationBuilder.CreateIndex(
                name: "ix_consultas_asistente_fecha",
                table: "consultas_asistente",
                column: "fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultas_asistente");
        }
    }
}
