using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsuariosRolesYHorarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ultimo_ingreso_en",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "carrera_id",
                table: "documentos_rag",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_rag_carrera_id_tipo",
                table: "documentos_rag",
                columns: new[] { "carrera_id", "tipo" });

            migrationBuilder.AddForeignKey(
                name: "fk_documentos_rag_carreras_carrera_id",
                table: "documentos_rag",
                column: "carrera_id",
                principalTable: "carreras",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documentos_rag_carreras_carrera_id",
                table: "documentos_rag");

            migrationBuilder.DropIndex(
                name: "ix_documentos_rag_carrera_id_tipo",
                table: "documentos_rag");

            migrationBuilder.DropColumn(
                name: "ultimo_ingreso_en",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "carrera_id",
                table: "documentos_rag");
        }
    }
}
