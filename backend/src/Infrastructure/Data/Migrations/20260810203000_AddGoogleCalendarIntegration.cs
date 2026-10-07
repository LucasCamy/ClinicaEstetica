using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations;

[DbContext(typeof(EsteticaDbContext))]
[Migration("20260810203000_AddGoogleCalendarIntegration")]
public partial class AddGoogleCalendarIntegration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GoogleCalendarEventId",
            table: "Appointments",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "GoogleCalendarConnections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EncryptedRefreshToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                CalendarId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                CalendarName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSuccessfulSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_GoogleCalendarConnections", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_GoogleCalendarConnections_CalendarId",
            table: "GoogleCalendarConnections",
            column: "CalendarId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GoogleCalendarConnections");

        migrationBuilder.DropColumn(
            name: "GoogleCalendarEventId",
            table: "Appointments");
    }
}
