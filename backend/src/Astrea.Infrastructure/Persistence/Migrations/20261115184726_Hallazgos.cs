using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hallazgos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "objetos_perdidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    publicado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo_publico = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descripcion_privada = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    categoria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sector_encontrado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    franja_horaria = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    punto_resguardo = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    fecha_hallazgo = table.Column<DateOnly>(type: "date", nullable: false),
                    foto_storage_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    foto_content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    fecha_publicacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_baja = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dado_de_baja_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objetos_perdidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_objetos_perdidos_usuarios_dado_de_baja_por_usuario_id",
                        column: x => x.dado_de_baja_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_objetos_perdidos_usuarios_publicado_por_usuario_id",
                        column: x => x.publicado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entregas_objeto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    objeto_perdido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_retirante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    dni_retirante = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    verificado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identidad_verificada = table.Column<bool>(type: "boolean", nullable: false),
                    observacion_interna = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_entrega = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entregas_objeto", x => x.id);
                    table.ForeignKey(
                        name: "fk_entregas_objeto_objetos_perdidos_objeto_perdido_id",
                        column: x => x.objeto_perdido_id,
                        principalTable: "objetos_perdidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entregas_objeto_usuarios_verificado_por_usuario_id",
                        column: x => x.verificado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entregas_objeto_objeto_perdido_id",
                table: "entregas_objeto",
                column: "objeto_perdido_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entregas_objeto_verificado_por_usuario_id",
                table: "entregas_objeto",
                column: "verificado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_objetos_perdidos_dado_de_baja_por_usuario_id",
                table: "objetos_perdidos",
                column: "dado_de_baja_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_objetos_perdidos_estado_fecha_hallazgo",
                table: "objetos_perdidos",
                columns: new[] { "estado", "fecha_hallazgo" });

            migrationBuilder.CreateIndex(
                name: "ix_objetos_perdidos_publicado_por_usuario_id_fecha_publicacion",
                table: "objetos_perdidos",
                columns: new[] { "publicado_por_usuario_id", "fecha_publicacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entregas_objeto");

            migrationBuilder.DropTable(
                name: "objetos_perdidos");
        }
    }
}
