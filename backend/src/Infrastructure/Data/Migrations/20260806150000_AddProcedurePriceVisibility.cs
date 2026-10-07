using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PainelEstetica.Infrastructure.Data;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations;

[DbContext(typeof(EsteticaDbContext))]
[Migration("20260806150000_AddProcedurePriceVisibility")]
public partial class AddProcedurePriceVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPriceHiddenOnWebsite",
            table: "ProcedureTypes",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsPriceHiddenOnWebsite",
            table: "ProcedureTypes");
    }
}
