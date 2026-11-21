using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerfilLocalidadYCarreras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "datos_confirmados_en",
                table: "usuarios");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "carreras_ids",
                table: "usuarios",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<string>(
                name: "localidad",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carreras_ids",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "localidad",
                table: "usuarios");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "datos_confirmados_en",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
