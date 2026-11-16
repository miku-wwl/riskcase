using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskCase.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentReviewIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssessmentId",
                schema: "riskcase",
                table: "risk_decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessedById",
                schema: "riskcase",
                table: "risk_cases",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BasisVersion",
                schema: "riskcase",
                table: "risk_cases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAssessmentId",
                schema: "riskcase",
                table: "risk_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewSubmittedAt",
                schema: "riskcase",
                table: "risk_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewSubmittedById",
                schema: "riskcase",
                table: "risk_cases",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedAssessmentId",
                schema: "riskcase",
                table: "risk_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    AssessorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CaseVersion = table.Column<int>(type: "integer", nullable: false),
                    BasisVersion = table.Column<int>(type: "integer", nullable: false),
                    IsLegacy = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_assessments", x => x.Id);
                    table.UniqueConstraint("AK_risk_assessments_CaseId_Id", x => new { x.CaseId, x.Id });
                    table.CheckConstraint("ck_assessment_author", "\"IsLegacy\" OR \"AssessorId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_risk_assessments_risk_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "riskcase",
                        principalTable: "risk_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_CaseId_AssessmentId",
                schema: "riskcase",
                table: "risk_decisions",
                columns: new[] { "CaseId", "AssessmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_CaseId_CaseVersion",
                schema: "riskcase",
                table: "risk_assessments",
                columns: new[] { "CaseId", "CaseVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_risk_decisions_risk_assessments_CaseId_AssessmentId",
                schema: "riskcase",
                table: "risk_decisions",
                columns: new[] { "CaseId", "AssessmentId" },
                principalSchema: "riskcase",
                principalTable: "risk_assessments",
                principalColumns: new[] { "CaseId", "Id" },
                onDelete: ReferentialAction.Restrict);

            // Preserve only what Round 1 actually retained. The author and overwritten
            // rationales cannot be inferred reliably from the current assignee.
            migrationBuilder.Sql("""
                INSERT INTO riskcase.risk_assessments
                  ("Id", "CaseId", "Level", "Rationale", "AssessorId", "AssessedAt", "CaseVersion", "BasisVersion", "IsLegacy")
                SELECT gen_random_uuid(), c."Id", c."AssessmentLevel", c."AssessmentRationale", NULL,
                       c."AssessedAt", COALESCE((SELECT max(a."CaseVersion") FROM riskcase.audit_events a
                          WHERE a."CaseId"=c."Id" AND a."Operation"='AssessmentRecorded'),0), 0, true
                FROM riskcase.risk_cases c
                WHERE c."AssessmentLevel" IS NOT NULL AND c."AssessmentRationale" IS NOT NULL AND c."AssessedAt" IS NOT NULL;

                INSERT INTO riskcase.audit_events ("Id", "CaseId", "CaseVersion", "ActorId", "Operation", "DetailsJson", "OccurredAt")
                SELECT gen_random_uuid(), "Id", "Version"+1, 'migration:round2', 'LegacyReviewInvalidated',
                  '{"reason":"Legacy review lacks a verifiable assessment author; reassessment required."}'::jsonb, now()
                FROM riskcase.risk_cases WHERE "Status"='Reviewing';
                UPDATE riskcase.risk_cases SET "Status"='Investigating', "Version"="Version"+1, "LastModified"=now()
                  WHERE "Status"='Reviewing';
                UPDATE riskcase.risk_cases SET "BasisVersion"="Version";
                UPDATE riskcase.risk_cases SET "AssessmentLevel"=NULL, "AssessmentRationale"=NULL, "AssessedAt"=NULL
                  WHERE "Status" <> 'Resolved';

                CREATE FUNCTION riskcase.reject_history_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Business history is append-only' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER immutable_assessments BEFORE UPDATE OR DELETE ON riskcase.risk_assessments
                  FOR EACH ROW EXECUTE FUNCTION riskcase.reject_history_change();
                CREATE TRIGGER immutable_decisions BEFORE UPDATE OR DELETE ON riskcase.risk_decisions
                  FOR EACH ROW EXECUTE FUNCTION riskcase.reject_history_change();
                CREATE TRIGGER immutable_audit BEFORE UPDATE OR DELETE ON riskcase.audit_events
                  FOR EACH ROW EXECUTE FUNCTION riskcase.reject_history_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER immutable_assessments ON riskcase.risk_assessments;
                DROP TRIGGER immutable_decisions ON riskcase.risk_decisions;
                DROP TRIGGER immutable_audit ON riskcase.audit_events;
                DROP FUNCTION riskcase.reject_history_change();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_risk_decisions_risk_assessments_CaseId_AssessmentId",
                schema: "riskcase",
                table: "risk_decisions");

            migrationBuilder.DropTable(
                name: "risk_assessments",
                schema: "riskcase");

            migrationBuilder.DropIndex(
                name: "IX_risk_decisions_CaseId_AssessmentId",
                schema: "riskcase",
                table: "risk_decisions");

            migrationBuilder.DropColumn(
                name: "AssessmentId",
                schema: "riskcase",
                table: "risk_decisions");

            migrationBuilder.DropColumn(
                name: "AssessedById",
                schema: "riskcase",
                table: "risk_cases");

            migrationBuilder.DropColumn(
                name: "BasisVersion",
                schema: "riskcase",
                table: "risk_cases");

            migrationBuilder.DropColumn(
                name: "CurrentAssessmentId",
                schema: "riskcase",
                table: "risk_cases");

            migrationBuilder.DropColumn(
                name: "ReviewSubmittedAt",
                schema: "riskcase",
                table: "risk_cases");

            migrationBuilder.DropColumn(
                name: "ReviewSubmittedById",
                schema: "riskcase",
                table: "risk_cases");

            migrationBuilder.DropColumn(
                name: "ReviewedAssessmentId",
                schema: "riskcase",
                table: "risk_cases");
        }
    }
}
