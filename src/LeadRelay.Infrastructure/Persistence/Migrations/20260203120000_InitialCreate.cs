using System;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260203120000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Sites",
            columns: table => new
            {
                Id = table.Column<string>(type: "varchar(255)", nullable: false),
                Name = table.Column<string>(type: "longtext", nullable: false),
                BusinessSummary = table.Column<string>(type: "longtext", nullable: true),
                AllowedDomainsJson = table.Column<string>(type: "longtext", nullable: false),
                FieldsJson = table.Column<string>(type: "longtext", nullable: false),
                OptionalFieldsJson = table.Column<string>(type: "longtext", nullable: false),
                IntroMessage = table.Column<string>(type: "longtext", nullable: true),
                OwnerEmail = table.Column<string>(type: "longtext", nullable: false),
                WhatsAppNumber = table.Column<string>(type: "longtext", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sites", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Leads",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                SiteId = table.Column<string>(type: "varchar(255)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                Name = table.Column<string>(type: "longtext", nullable: true),
                Email = table.Column<string>(type: "longtext", nullable: true),
                Phone = table.Column<string>(type: "longtext", nullable: true),
                Intent = table.Column<string>(type: "longtext", nullable: true),
                Notes = table.Column<string>(type: "longtext", nullable: true),
                PageUrl = table.Column<string>(type: "longtext", nullable: true),
                Referrer = table.Column<string>(type: "longtext", nullable: true),
                UtmJson = table.Column<string>(type: "longtext", nullable: false),
                FieldsJson = table.Column<string>(type: "longtext", nullable: false),
                ConversationJson = table.Column<string>(type: "longtext", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Leads", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ConversationStates",
            columns: table => new
            {
                Id = table.Column<string>(type: "varchar(255)", nullable: false),
                SiteId = table.Column<string>(type: "varchar(255)", nullable: false),
                WaId = table.Column<string>(type: "varchar(255)", nullable: false),
                StepIndex = table.Column<int>(type: "int", nullable: false),
                CollectedJson = table.Column<string>(type: "longtext", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                HistoryJson = table.Column<string>(type: "longtext", nullable: false),
                SystemPromptOverride = table.Column<string>(type: "longtext", nullable: true),
                LeadId = table.Column<Guid>(type: "char(36)", nullable: true),
                LeadCreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationStates", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationStates_SiteId_WaId",
            table: "ConversationStates",
            columns: new[] { "SiteId", "WaId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ConversationStates");
        migrationBuilder.DropTable(name: "Leads");
        migrationBuilder.DropTable(name: "Sites");
    }
}
