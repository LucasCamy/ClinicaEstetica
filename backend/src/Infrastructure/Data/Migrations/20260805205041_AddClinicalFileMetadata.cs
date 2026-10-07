using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ClientPhotos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "ClientPhotos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "ClientPhotos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ClientPhotos",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "OriginalFileSizeBytes",
                table: "ClientPhotos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "ClientPhotos",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "ClientPhotos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ClientDocuments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "ClientDocuments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "ClientDocuments",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "OriginalFileSizeBytes",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "ClientPhotos");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ClientDocuments");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "ClientDocuments");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "ClientDocuments");
        }
    }
}
