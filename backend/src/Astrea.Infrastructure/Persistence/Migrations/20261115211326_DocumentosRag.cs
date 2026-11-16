using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Astrea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentosRag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_rag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fuente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_documento = table.Column<DateOnly>(type: "date", nullable: true),
                    storage_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    cargado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    error_procesamiento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    cantidad_paginas = table.Column<int>(type: "integer", nullable: true),
                    cantidad_fragmentos = table.Column<int>(type: "integer", nullable: true),
                    fecha_indexacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retirado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_rag", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_rag_usuarios_cargado_por_usuario_id",
                        column: x => x.cargado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_rag_usuarios_retirado_por_usuario_id",
                        column: x => x.retirado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fragmentos_rag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_rag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_fragmento = table.Column<int>(type: "integer", nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    pagina_desde = table.Column<int>(type: "integer", nullable: false),
                    pagina_hasta = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fragmentos_rag", x => x.id);
                    table.ForeignKey(
                        name: "fk_fragmentos_rag_documentos_rag_documento_rag_id",
                        column: x => x.documento_rag_id,
                        principalTable: "documentos_rag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_rag_cargado_por_usuario_id",
                table: "documentos_rag",
                column: "cargado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_rag_estado_fecha_carga",
                table: "documentos_rag",
                columns: new[] { "estado", "fecha_carga" });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_rag_hash_sha256",
                table: "documentos_rag",
                column: "hash_sha256");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_rag_retirado_por_usuario_id",
                table: "documentos_rag",
                column: "retirado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_rag_serie_id_version",
                table: "documentos_rag",
                columns: new[] { "serie_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fragmentos_rag_documento_rag_id_numero_fragmento",
                table: "fragmentos_rag",
                columns: new[] { "documento_rag_id", "numero_fragmento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fragmentos_rag_embedding",
                table: "fragmentos_rag",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fragmentos_rag");

            migrationBuilder.DropTable(
                name: "documentos_rag");
        }
    }
}
