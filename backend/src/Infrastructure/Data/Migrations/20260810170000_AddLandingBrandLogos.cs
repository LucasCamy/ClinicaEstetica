using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations;

[DbContext(typeof(EsteticaDbContext))]
[Migration("20260810170000_AddLandingBrandLogos")]
public partial class AddLandingBrandLogos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LogoOnDarkUrl",
            table: "CmsContents",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "/brand/leilaine-arakaki-logo-light.png");

        migrationBuilder.AddColumn<string>(
            name: "LogoOnLightUrl",
            table: "CmsContents",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "/brand/leilaine-arakaki-logo-dark.png");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LogoOnDarkUrl",
            table: "CmsContents");

        migrationBuilder.DropColumn(
            name: "LogoOnLightUrl",
            table: "CmsContents");
    }
}
