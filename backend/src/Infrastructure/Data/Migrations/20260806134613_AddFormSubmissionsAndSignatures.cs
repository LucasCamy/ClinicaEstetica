using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelEstetica.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormSubmissionsAndSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AnswersJson = table.Column<string>(type: "jsonb", nullable: false),
                    AnswersHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_FormSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormSubmissionAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmendmentNumber = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AnswersJson = table.Column<string>(type: "jsonb", nullable: false),
                    AnswersHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PreviousAnswersHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissionAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissionAmendments_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormSubmissionAmendmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FieldId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SignerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SignerDeclaration = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    StrokesJson = table.Column<string>(type: "jsonb", nullable: false),
                    RenderedSvg = table.Column<string>(type: "text", nullable: false),
                    SignatureHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AnswersHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SchemaHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PointerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CanvasWidth = table.Column<int>(type: "integer", nullable: false),
                    CanvasHeight = table.Column<int>(type: "integer", nullable: false),
                    ConductedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSignatures_FormSubmissionAmendments_FormSubmissionAmend~",
                        column: x => x.FormSubmissionAmendmentId,
                        principalTable: "FormSubmissionAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormSignatures_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormSignatures_FormSubmissionAmendmentId",
                table: "FormSignatures",
                column: "FormSubmissionAmendmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSignatures_FormSubmissionId_FieldId",
                table: "FormSignatures",
                columns: new[] { "FormSubmissionId", "FieldId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissionAmendments_FormSubmissionId_AmendmentNumber",
                table: "FormSubmissionAmendments",
                columns: new[] { "FormSubmissionId", "AmendmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_AppointmentId_UpdatedAtUtc",
                table: "FormSubmissions",
                columns: new[] { "AppointmentId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_ClientId_UpdatedAtUtc",
                table: "FormSubmissions",
                columns: new[] { "ClientId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_FormVersionId_Status",
                table: "FormSubmissions",
                columns: new[] { "FormVersionId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormSignatures");

            migrationBuilder.DropTable(
                name: "FormSubmissionAmendments");

            migrationBuilder.DropTable(
                name: "FormSubmissions");
        }
    }
}
