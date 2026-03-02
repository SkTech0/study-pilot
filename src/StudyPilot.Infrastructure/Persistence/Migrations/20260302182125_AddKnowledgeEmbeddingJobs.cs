using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeEmbeddingJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeEmbeddingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeEmbeddingJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEmbeddingJobs_DocumentId",
                table: "KnowledgeEmbeddingJobs",
                column: "DocumentId",
                unique: true,
                filter: "\"Status\" IN ('Pending','Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEmbeddingJobs_Status",
                table: "KnowledgeEmbeddingJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEmbeddingJobs_Status_CreatedAtUtc",
                table: "KnowledgeEmbeddingJobs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEmbeddingJobs_Status_NextRetryAtUtc",
                table: "KnowledgeEmbeddingJobs",
                columns: new[] { "Status", "NextRetryAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeEmbeddingJobs");
        }
    }
}
