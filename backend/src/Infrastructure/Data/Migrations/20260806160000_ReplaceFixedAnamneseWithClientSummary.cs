using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PainelEstetica.Infrastructure.Data;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations;

[DbContext(typeof(EsteticaDbContext))]
[Migration("20260806160000_ReplaceFixedAnamneseWithClientSummary")]
public partial class ReplaceFixedAnamneseWithClientSummary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Anamneses");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Anamneses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                MedicalHistory = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Allergies = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CurrentMedications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                SkinType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                HasBotoxOrFillers = table.Column<bool>(type: "boolean", nullable: false),
                IsPregnantOrLactating = table.Column<bool>(type: "boolean", nullable: false),
                AgreedToTerms = table.Column<bool>(type: "boolean", nullable: false),
                SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SignatureUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Anamneses", x => x.Id);
                table.ForeignKey(
                    name: "FK_Anamneses_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Anamneses_ClientId",
            table: "Anamneses",
            column: "ClientId",
            unique: true);
    }
}
