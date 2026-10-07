using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalizedFormPdfArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FinalPdfFileSizeBytes",
                table: "FormSubmissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalPdfPath",
                table: "FormSubmissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalPdfSha256",
                table: "FormSubmissions",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalPdfFileSizeBytes",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "FinalPdfPath",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "FinalPdfSha256",
                table: "FormSubmissions");
        }
    }
}
