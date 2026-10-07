using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedPdfTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TermTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DraftPdfPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DraftPdfFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DraftPdfFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DraftPdfSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    DraftLayoutJson = table.Column<string>(type: "jsonb", nullable: false),
                    DraftRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TermVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TermTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PdfPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PdfFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PdfFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PdfSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LayoutJson = table.Column<string>(type: "jsonb", nullable: false),
                    LayoutHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermVersions_TermTemplates_TermTemplateId",
                        column: x => x.TermTemplateId,
                        principalTable: "TermTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TermSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TermVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValuesHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    FinalPdfPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FinalPdfFileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FinalPdfSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermSubmissions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TermSubmissions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TermSubmissions_TermVersions_TermVersionId",
                        column: x => x.TermVersionId,
                        principalTable: "TermVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermSubmissions_AppointmentId_UpdatedAtUtc",
                table: "TermSubmissions",
                columns: new[] { "AppointmentId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TermSubmissions_ClientId_UpdatedAtUtc",
                table: "TermSubmissions",
                columns: new[] { "ClientId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TermSubmissions_TermVersionId_Status",
                table: "TermSubmissions",
                columns: new[] { "TermVersionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TermTemplates_Name",
                table: "TermTemplates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TermTemplates_Status_UpdatedAtUtc",
                table: "TermTemplates",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TermVersions_TermTemplateId_VersionNumber",
                table: "TermVersions",
                columns: new[] { "TermTemplateId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TermSubmissions");

            migrationBuilder.DropTable(
                name: "TermVersions");

            migrationBuilder.DropTable(
                name: "TermTemplates");
        }
    }
}
