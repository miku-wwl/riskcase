using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskCase.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegacyAssessorProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_audit_events_CaseId_Id",
                schema: "riskcase",
                table: "audit_events",
                columns: new[] { "CaseId", "Id" });

            migrationBuilder.CreateTable(
                name: "legacy_assessor_participation",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseVersion = table.Column<int>(type: "integer", nullable: false),
                    AssessorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    SourceAuditId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAssessmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_assessor_participation", x => x.Id);
                    table.CheckConstraint("ck_legacy_author_identity", "\"AssessorId\" IS NULL OR length(btrim(\"AssessorId\")) > 0");
                    table.CheckConstraint("ck_legacy_author_provenance", "(\"SourceAuditId\" IS NULL) <> (\"SourceAssessmentId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_legacy_assessor_participation_audit_events_CaseId_SourceAud~",
                        columns: x => new { x.CaseId, x.SourceAuditId },
                        principalSchema: "riskcase",
                        principalTable: "audit_events",
                        principalColumns: new[] { "CaseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legacy_assessor_participation_risk_assessments_CaseId_Sourc~",
                        columns: x => new { x.CaseId, x.SourceAssessmentId },
                        principalSchema: "riskcase",
                        principalTable: "risk_assessments",
                        principalColumns: new[] { "CaseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legacy_assessor_participation_risk_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "riskcase",
                        principalTable: "risk_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_assessor_participation_CaseId_AssessorId",
                schema: "riskcase",
                table: "legacy_assessor_participation",
                columns: new[] { "CaseId", "AssessorId" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_assessor_participation_CaseId_SourceAssessmentId",
                schema: "riskcase",
                table: "legacy_assessor_participation",
                columns: new[] { "CaseId", "SourceAssessmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_assessor_participation_CaseId_SourceAuditId",
                schema: "riskcase",
                table: "legacy_assessor_participation",
                columns: new[] { "CaseId", "SourceAuditId" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_assessor_participation_SourceAssessmentId",
                schema: "riskcase",
                table: "legacy_assessor_participation",
                column: "SourceAssessmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legacy_assessor_participation_SourceAuditId",
                schema: "riskcase",
                table: "legacy_assessor_participation",
                column: "SourceAuditId",
                unique: true);

            // R1 retained command actors, even when earlier rationale was overwritten. Index
            // those identities without inventing assessment levels, rationale or timestamps.
            migrationBuilder.Sql("""
                INSERT INTO riskcase.legacy_assessor_participation
                  ("Id","CaseId","CaseVersion","AssessorId","SourceAuditId","SourceAssessmentId")
                SELECT gen_random_uuid(), a."CaseId", a."CaseVersion",
                  CASE WHEN btrim(a."ActorId") = '' OR lower(btrim(a."ActorId")) IN ('unknown','system')
                    OR a."ActorId" LIKE 'migration:%' OR a."ActorId" LIKE 'upstream:%' THEN NULL
                    ELSE a."ActorId" END, a."Id", NULL
                FROM riskcase.audit_events a
                WHERE a."Operation" = 'AssessmentRecorded' AND NOT EXISTS (
                  SELECT 1 FROM riskcase.risk_assessments s
                  WHERE s."CaseId"=a."CaseId" AND s."CaseVersion"=a."CaseVersion" AND NOT s."IsLegacy");

                -- A retained legacy snapshot with no matching audit is an explicit identity gap.
                INSERT INTO riskcase.legacy_assessor_participation
                  ("Id","CaseId","CaseVersion","AssessorId","SourceAuditId","SourceAssessmentId")
                SELECT gen_random_uuid(), s."CaseId", s."CaseVersion", s."AssessorId", NULL, s."Id"
                FROM riskcase.risk_assessments s
                WHERE s."IsLegacy" AND NOT EXISTS (
                  SELECT 1 FROM riskcase.audit_events a WHERE a."CaseId"=s."CaseId"
                    AND a."CaseVersion"=s."CaseVersion" AND a."Operation"='AssessmentRecorded');

                CREATE TRIGGER legacy_assessor_participation_append_only
                  BEFORE UPDATE OR DELETE ON riskcase.legacy_assessor_participation
                  FOR EACH ROW EXECUTE FUNCTION riskcase.reject_history_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Historical author provenance is forward-only. Restore a verified backup to roll back the release.");
        }
    }
}
