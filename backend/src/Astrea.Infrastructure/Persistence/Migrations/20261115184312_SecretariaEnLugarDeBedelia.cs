using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecretariaEnLugarDeBedelia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0a6a8f1e-4c5b-4d1e-9b77-2f6c1b9d0e02"),
                column: "descripcion",
                value: "Personal de Secretaría: administra planes, mesas, trámites, hallazgos y documentos.");

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000004"),
                column: "descripcion",
                value: "Pedí que se justifiquen tus faltas por un motivo de salud, laboral u otro previsto en el reglamento. Presentá el comprobante original en Secretaría.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0a6a8f1e-4c5b-4d1e-9b77-2f6c1b9d0e02"),
                column: "descripcion",
                value: "Personal de Bedelía / Secretaría: administra planes, mesas, trámites, hallazgos y documentos.");

            migrationBuilder.UpdateData(
                table: "tramite_tipos",
                keyColumn: "id",
                keyValue: new Guid("0193a5d0-0000-7000-8000-000000000004"),
                column: "descripcion",
                value: "Pedí que se justifiquen tus faltas por un motivo de salud, laboral u otro previsto en el reglamento. Presentá el comprobante original en Bedelía.");
        }
    }
}
