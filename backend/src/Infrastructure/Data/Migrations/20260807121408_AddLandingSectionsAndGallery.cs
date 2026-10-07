using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingSectionsAndGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AboutImageUrl",
                table: "CmsContents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AboutText",
                table: "CmsContents",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AboutTitle",
                table: "CmsContents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CarouselItemsJson",
                table: "CmsContents",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ClinicImageUrl",
                table: "CmsContents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClinicText",
                table: "CmsContents",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClinicTitle",
                table: "CmsContents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "CmsContents"
                SET "DoctorPhotoUrl" = '/brand/dra-leilaine-portrait.jpg'
                WHERE "DoctorPhotoUrl" LIKE '%photo-1594824813566-8207198e3b1c%'
                   OR "DoctorPhotoUrl" LIKE '%maisbemestar.com.br/wp-content/uploads/2018/01/estetica-facil-saude-rosto.jpg%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AboutImageUrl",
                table: "CmsContents");

            migrationBuilder.DropColumn(
                name: "AboutText",
                table: "CmsContents");

            migrationBuilder.DropColumn(
                name: "AboutTitle",
                table: "CmsContents");

            migrationBuilder.DropColumn(
                name: "CarouselItemsJson",
                table: "CmsContents");

            migrationBuilder.DropColumn(
                name: "ClinicImageUrl",
                table: "CmsContents");

            migrationBuilder.DropColumn(
                name: "ClinicText",
                table: "CmsContents");

            migrationBuilder.DropColumn(
                name: "ClinicTitle",
                table: "CmsContents");

        }
    }
}
