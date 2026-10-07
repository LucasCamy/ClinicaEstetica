using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations;

[DbContext(typeof(EsteticaDbContext))]
[Migration("20260810220000_AddPublicLandingHours")]
public partial class AddPublicLandingHours : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PublicHoursWeekdays",
            table: "CmsContents",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "Segunda a Sexta: 08h00 às 19h00");

        migrationBuilder.AddColumn<string>(
            name: "PublicHoursSaturday",
            table: "CmsContents",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "Sábados: 08h00 às 13h00");

        migrationBuilder.AddColumn<string>(
            name: "PublicHoursNote",
            table: "CmsContents",
            type: "character varying(240)",
            maxLength: 240,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PublicHoursWeekdays", table: "CmsContents");
        migrationBuilder.DropColumn(name: "PublicHoursSaturday", table: "CmsContents");
        migrationBuilder.DropColumn(name: "PublicHoursNote", table: "CmsContents");
    }
}
