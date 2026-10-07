using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcedureReturnsAndCompletionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecommendedReturnDays",
                table: "ProcedureTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Appointments"
                SET "CompletedAtUtc" = "ScheduledDateTime"
                WHERE "Status" = 3 AND "CompletedAtUtc" IS NULL;
                """);

            migrationBuilder.UpdateData(
                table: "ProcedureTypes",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "RecommendedReturnDays",
                value: null);

            migrationBuilder.UpdateData(
                table: "ProcedureTypes",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "RecommendedReturnDays",
                value: null);

            migrationBuilder.UpdateData(
                table: "ProcedureTypes",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "RecommendedReturnDays",
                value: null);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcedureTypes_RecommendedReturnDays",
                table: "ProcedureTypes",
                sql: "\"RecommendedReturnDays\" IS NULL OR \"RecommendedReturnDays\" BETWEEN 1 AND 3650");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_CompletedAtUtc",
                table: "Appointments",
                columns: new[] { "Status", "CompletedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProcedureTypes_RecommendedReturnDays",
                table: "ProcedureTypes");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Status_CompletedAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RecommendedReturnDays",
                table: "ProcedureTypes");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Appointments");
        }
    }
}
