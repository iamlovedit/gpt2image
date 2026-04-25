using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ImageRelay.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RpmLimit = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyLimit = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_api_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpstreamName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "request_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpstreamAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UpstreamModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    BusinessStatus = table.Column<int>(type: "integer", nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SseEventCount = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true),
                    ImageInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ImageOutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ImageTotalTokens = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "upstream_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    ChatGptAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CoolingUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuccessCount = table.Column<long>(type: "bigint", nullable: false),
                    FailureCount = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyLimit = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AccountType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProxyKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    RateMultiplier = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AutoPauseOnExpired = table.Column<bool>(type: "boolean", nullable: true),
                    ChatGptUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OrganizationId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PlanType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubscriptionExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawMetadataJson = table.Column<string>(type: "text", nullable: true),
                    CodexPrimaryUsedPercent = table.Column<int>(type: "integer", nullable: true),
                    CodexSecondaryUsedPercent = table.Column<int>(type: "integer", nullable: true),
                    CodexPrimaryWindowMinutes = table.Column<int>(type: "integer", nullable: true),
                    CodexSecondaryWindowMinutes = table.Column<int>(type: "integer", nullable: true),
                    CodexPrimaryResetAfterSeconds = table.Column<int>(type: "integer", nullable: true),
                    CodexSecondaryResetAfterSeconds = table.Column<int>(type: "integer", nullable: true),
                    CodexPrimaryResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CodexSecondaryResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CodexPrimaryOverSecondaryLimitPercent = table.Column<int>(type: "integer", nullable: true),
                    CodexRateLimitUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upstream_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "upstream_header_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Originator = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upstream_header_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_Username",
                table: "admin_users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_api_keys_KeyHash",
                table: "client_api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_mappings_ExternalName",
                table: "model_mappings",
                column: "ExternalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_ClientKeyId",
                table: "request_logs",
                column: "ClientKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_StartedAt",
                table: "request_logs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_UpstreamAccountId",
                table: "request_logs",
                column: "UpstreamAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_upstream_accounts_Status_LastUsedAt",
                table: "upstream_accounts",
                columns: new[] { "Status", "LastUsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "client_api_keys");

            migrationBuilder.DropTable(
                name: "model_mappings");

            migrationBuilder.DropTable(
                name: "request_logs");

            migrationBuilder.DropTable(
                name: "upstream_accounts");

            migrationBuilder.DropTable(
                name: "upstream_header_settings");
        }
    }
}
