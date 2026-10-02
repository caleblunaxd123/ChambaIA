using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemanticEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingHash",
                table: "JobOffers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingHash",
                table: "CandidateProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_Embedding",
                table: "JobOffers",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobOffers_Embedding",
                table: "JobOffers");

            migrationBuilder.DropColumn(
                name: "EmbeddingHash",
                table: "JobOffers");

            migrationBuilder.DropColumn(
                name: "EmbeddingHash",
                table: "CandidateProfiles");
        }
    }
}
