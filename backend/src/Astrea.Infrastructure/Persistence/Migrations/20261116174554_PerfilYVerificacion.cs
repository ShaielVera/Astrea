using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerfilYVerificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "celular",
                table: "usuarios",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "celular_verificado_en",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "datos_confirmados_en",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_contacto",
                table: "usuarios",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_contacto_verificado_en",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            // Los datos que ya había cargado Secretaría quedan como confirmados (no se los puede cambiar el estudiante).
            migrationBuilder.Sql("UPDATE usuarios SET datos_confirmados_en = updated_at WHERE dni IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "codigos_verificacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    destino = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vence_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codigos_verificacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_codigos_verificacion_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_dni",
                table: "usuarios",
                column: "dni");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_verificacion_usuario_id_canal_creado_en",
                table: "codigos_verificacion",
                columns: new[] { "usuario_id", "canal", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_codigos_verificacion_vence_en",
                table: "codigos_verificacion",
                column: "vence_en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigos_verificacion");

            migrationBuilder.DropIndex(
                name: "ix_usuarios_dni",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "celular",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "celular_verificado_en",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "datos_confirmados_en",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "email_contacto",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "email_contacto_verificado_en",
                table: "usuarios");
        }
    }
}
