using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPilot.Infrastructure.Persistence.Migrations
{
    [Migration("20260302120000_AddDocumentFailureReason")]
    public partial class AddDocumentFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Documents");
        }
    }
}
