using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsistenteConversaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_ultimo_mensaje = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversaciones_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mensajes_chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    contenido = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    fuentes_json = table.Column<string>(type: "jsonb", nullable: false),
                    sin_evidencia = table.Column<bool>(type: "boolean", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensajes_chat", x => x.id);
                    table.ForeignKey(
                        name: "fk_mensajes_chat_conversaciones_conversacion_id",
                        column: x => x.conversacion_id,
                        principalTable: "conversaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversaciones_fecha_ultimo_mensaje",
                table: "conversaciones",
                column: "fecha_ultimo_mensaje");

            migrationBuilder.CreateIndex(
                name: "ix_conversaciones_usuario_id_fecha_ultimo_mensaje",
                table: "conversaciones",
                columns: new[] { "usuario_id", "fecha_ultimo_mensaje" });

            migrationBuilder.CreateIndex(
                name: "ix_mensajes_chat_conversacion_id_fecha",
                table: "mensajes_chat",
                columns: new[] { "conversacion_id", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mensajes_chat");

            migrationBuilder.DropTable(
                name: "conversaciones");
        }
    }
}
