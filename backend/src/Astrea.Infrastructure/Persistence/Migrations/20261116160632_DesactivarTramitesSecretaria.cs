using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DesactivarTramitesSecretaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000003"),
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000004"),
                column: "activo",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000003"),
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000004"),
                column: "activo",
                value: true);
        }
    }
}
