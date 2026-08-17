using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260817120000_AddWhatsAppOnboardingAndHardening")]
public partial class AddWhatsAppOnboardingAndHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Validate legacy rows in a temporary table before any permanent DDL. This makes
        // duplicate or overlength data fail without partially altering the Sites table.
        migrationBuilder.Sql("DROP TEMPORARY TABLE IF EXISTS `LeadRelay_OnboardingMigrationPreflight`;");
        migrationBuilder.Sql(
            """
            CREATE TEMPORARY TABLE `LeadRelay_OnboardingMigrationPreflight` (
                `OwnerEmail` longtext NOT NULL,
                `WhatsAppNumber` longtext NOT NULL,
                `WhatsAppPhoneNumberId` longtext NULL,
                `OwnerEmailIndexValue` varchar(255) NOT NULL,
                `WhatsAppPhoneNumberIdIndexValue` varchar(64) NULL,
                CONSTRAINT `CK_Preflight_OwnerEmailLength` CHECK (CHAR_LENGTH(`OwnerEmail`) <= 255),
                CONSTRAINT `CK_Preflight_WhatsAppNumberLength` CHECK (CHAR_LENGTH(`WhatsAppNumber`) <= 64),
                CONSTRAINT `CK_Preflight_WhatsAppPhoneNumberIdLength` CHECK (`WhatsAppPhoneNumberId` IS NULL OR CHAR_LENGTH(`WhatsAppPhoneNumberId`) <= 64),
                UNIQUE KEY `UX_Preflight_OwnerEmail` (`OwnerEmailIndexValue`),
                UNIQUE KEY `UX_Preflight_WhatsAppPhoneNumberId` (`WhatsAppPhoneNumberIdIndexValue`)
            );
            """);
        migrationBuilder.Sql(
            """
            INSERT INTO `LeadRelay_OnboardingMigrationPreflight`
                (`OwnerEmail`, `WhatsAppNumber`, `WhatsAppPhoneNumberId`, `OwnerEmailIndexValue`, `WhatsAppPhoneNumberIdIndexValue`)
            SELECT
                `OwnerEmail`,
                `WhatsAppNumber`,
                `WhatsAppPhoneNumberId`,
                LOWER(TRIM(`OwnerEmail`)),
                CASE
                    WHEN `WhatsAppPhoneNumberId` IS NULL THEN NULL
                    ELSE TRIM(`WhatsAppPhoneNumberId`)
                END
            FROM `Sites`;
            """);
        migrationBuilder.Sql("DROP TEMPORARY TABLE `LeadRelay_OnboardingMigrationPreflight`;");

        migrationBuilder.AlterColumn<string>(
            name: "WhatsAppNumber",
            table: "Sites",
            type: "varchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext");

        migrationBuilder.AlterColumn<string>(
            name: "OwnerEmail",
            table: "Sites",
            type: "varchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext");

        migrationBuilder.CreateIndex(
            name: "IX_Sites_OwnerEmail",
            table: "Sites",
            column: "OwnerEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sites_WhatsAppPhoneNumberId",
            table: "Sites",
            column: "WhatsAppPhoneNumberId",
            unique: true);

        migrationBuilder.CreateTable(
            name: "WhatsAppConnections",
            columns: table => new
            {
                SiteId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                WabaId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                PhoneNumberId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                DisplayPhoneNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                AccessTokenCiphertext = table.Column<string>(type: "longtext", nullable: false),
                Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                WebhookSubscribedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                LastInboundAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                LastOutboundTestAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                LastError = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WhatsAppConnections", x => x.SiteId);
                table.ForeignKey(
                    name: "FK_WhatsAppConnections_Sites_SiteId",
                    column: x => x.SiteId,
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WhatsAppMessageReceipts",
            columns: table => new
            {
                SiteId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                MessageId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                SideEffectsStartedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WhatsAppMessageReceipts", x => new { x.SiteId, x.MessageId });
                table.ForeignKey(
                    name: "FK_WhatsAppMessageReceipts_Sites_SiteId",
                    column: x => x.SiteId,
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppConnections_PhoneNumberId",
            table: "WhatsAppConnections",
            column: "PhoneNumberId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppMessageReceipts_StartedAtUtc",
            table: "WhatsAppMessageReceipts",
            column: "StartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppMessageReceipts_SideEffectsStartedAtUtc",
            table: "WhatsAppMessageReceipts",
            column: "SideEffectsStartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_WhatsAppMessageReceipts_ProcessedAtUtc",
            table: "WhatsAppMessageReceipts",
            column: "ProcessedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WhatsAppConnections");
        migrationBuilder.DropTable(name: "WhatsAppMessageReceipts");
        migrationBuilder.DropIndex(name: "IX_Sites_OwnerEmail", table: "Sites");
        migrationBuilder.DropIndex(name: "IX_Sites_WhatsAppPhoneNumberId", table: "Sites");

        migrationBuilder.AlterColumn<string>(
            name: "WhatsAppNumber",
            table: "Sites",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64);

        migrationBuilder.AlterColumn<string>(
            name: "OwnerEmail",
            table: "Sites",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldMaxLength: 255);
    }
}
