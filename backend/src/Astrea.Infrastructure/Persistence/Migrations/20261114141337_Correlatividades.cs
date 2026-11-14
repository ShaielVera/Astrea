using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Correlatividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "correlatividades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_estudio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_materia_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    plan_materia_requisito_id = table.Column<Guid>(type: "uuid", nullable: true),
                    condicion = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    grupo_alternativa = table.Column<int>(type: "integer", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correlatividades", x => x.id);
                    table.ForeignKey(
                        name: "fk_correlatividades_plan_materias_plan_materia_destino_id",
                        column: x => x.plan_materia_destino_id,
                        principalTable: "plan_materias",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_correlatividades_plan_materias_plan_materia_requisito_id",
                        column: x => x.plan_materia_requisito_id,
                        principalTable: "plan_materias",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_correlatividades_planes_estudio_plan_estudio_id",
                        column: x => x.plan_estudio_id,
                        principalTable: "planes_estudio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_correlatividades_plan_estudio_id",
                table: "correlatividades",
                column: "plan_estudio_id");

            migrationBuilder.CreateIndex(
                name: "ix_correlatividades_plan_materia_destino_id_tipo",
                table: "correlatividades",
                columns: new[] { "plan_materia_destino_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "ix_correlatividades_plan_materia_destino_id_tipo_plan_materia_",
                table: "correlatividades",
                columns: new[] { "plan_materia_destino_id", "tipo", "plan_materia_requisito_id" },
                unique: true,
                filter: "plan_materia_requisito_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_correlatividades_plan_materia_requisito_id",
                table: "correlatividades",
                column: "plan_materia_requisito_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "correlatividades");
        }
    }
}
