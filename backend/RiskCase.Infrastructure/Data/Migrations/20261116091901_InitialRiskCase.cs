using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiskCase.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialRiskCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "riskcase");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Segment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    RiskTier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_receipts",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "riskcase",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "riskcase",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "riskcase",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "riskcase",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "riskcase",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "riskcase",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "riskcase",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "riskcase",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "riskcase",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaskedNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "riskcase",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_alerts",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalAlertId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IngestionFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_alerts_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "riskcase",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_risk_alerts_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "riskcase",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_cases",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AssignedAnalystId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AssessmentLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AssessmentRationale = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_cases_risk_alerts_AlertId",
                        column: x => x.AlertId,
                        principalSchema: "riskcase",
                        principalTable: "risk_alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_signals",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TransactionReferences = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_signals_risk_alerts_AlertId",
                        column: x => x.AlertId,
                        principalSchema: "riskcase",
                        principalTable: "risk_alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_transactions",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CounterpartyReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CounterpartyLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_transactions_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "riskcase",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_risk_transactions_risk_alerts_AlertId",
                        column: x => x.AlertId,
                        principalSchema: "riskcase",
                        principalTable: "risk_alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseVersion = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_events_risk_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "riskcase",
                        principalTable: "risk_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_case_links",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    TargetNodeId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AnalystId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CaseVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_case_links", x => x.Id);
                    table.CheckConstraint("ck_risk_case_links_distinct_nodes", "\"SourceNodeId\" <> \"TargetNodeId\"");
                    table.ForeignKey(
                        name: "FK_risk_case_links_risk_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "riskcase",
                        principalTable: "risk_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_case_notes",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalystId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CaseVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_case_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_case_notes_risk_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "riskcase",
                        principalTable: "risk_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_decisions",
                schema: "riskcase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Disposition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnalystId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CaseVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_decisions_risk_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "riskcase",
                        principalTable: "risk_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_CustomerId",
                schema: "riskcase",
                table: "accounts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_MaskedNumber",
                schema: "riskcase",
                table: "accounts",
                column: "MaskedNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "riskcase",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "riskcase",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "riskcase",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "riskcase",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "riskcase",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "riskcase",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "riskcase",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_CaseId_CaseVersion",
                schema: "riskcase",
                table: "audit_events",
                columns: new[] { "CaseId", "CaseVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_CustomerNumber",
                schema: "riskcase",
                table: "customers",
                column: "CustomerNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_receipts_CreatedAt",
                schema: "riskcase",
                table: "idempotency_receipts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_receipts_Scope_Key",
                schema: "riskcase",
                table: "idempotency_receipts",
                columns: new[] { "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_alerts_AccountId",
                schema: "riskcase",
                table: "risk_alerts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_alerts_CustomerId",
                schema: "riskcase",
                table: "risk_alerts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_alerts_DetectedAt_Severity",
                schema: "riskcase",
                table: "risk_alerts",
                columns: new[] { "DetectedAt", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_alerts_SourceSystem_ExternalAlertId",
                schema: "riskcase",
                table: "risk_alerts",
                columns: new[] { "SourceSystem", "ExternalAlertId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_case_links_CaseId_SourceNodeId_TargetNodeId",
                schema: "riskcase",
                table: "risk_case_links",
                columns: new[] { "CaseId", "SourceNodeId", "TargetNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_case_notes_CaseId_OccurredAt",
                schema: "riskcase",
                table: "risk_case_notes",
                columns: new[] { "CaseId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_cases_AlertId",
                schema: "riskcase",
                table: "risk_cases",
                column: "AlertId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_cases_Status_AssignedAnalystId_LastModified",
                schema: "riskcase",
                table: "risk_cases",
                columns: new[] { "Status", "AssignedAnalystId", "LastModified" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_CaseId_CaseVersion",
                schema: "riskcase",
                table: "risk_decisions",
                columns: new[] { "CaseId", "CaseVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_signals_AlertId_Code",
                schema: "riskcase",
                table: "risk_signals",
                columns: new[] { "AlertId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_transactions_AccountId",
                schema: "riskcase",
                table: "risk_transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_transactions_AlertId_ExternalReference",
                schema: "riskcase",
                table: "risk_transactions",
                columns: new[] { "AlertId", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_transactions_OccurredAt",
                schema: "riskcase",
                table: "risk_transactions",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "idempotency_receipts",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_case_links",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_case_notes",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_decisions",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_signals",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_transactions",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_cases",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "risk_alerts",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "riskcase");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "riskcase");
        }
    }
}
