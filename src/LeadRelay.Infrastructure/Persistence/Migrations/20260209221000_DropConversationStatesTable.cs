using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209221000_DropConversationStatesTable")]
public partial class DropConversationStatesTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ConversationStates");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConversationStates",
            columns: table => new
            {
                Id = table.Column<string>(type: "varchar(255)", nullable: false),
                SiteId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                WaId = table.Column<string>(type: "varchar(255)", nullable: false),
                StepIndex = table.Column<int>(type: "int", nullable: false),
                CollectedJson = table.Column<string>(type: "longtext", nullable: false),
                HistoryJson = table.Column<string>(type: "longtext", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                SessionStartedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                IsPaused = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ContactName = table.Column<string>(type: "longtext", nullable: true),
                SystemPromptOverride = table.Column<string>(type: "longtext", nullable: true),
                LeadId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                LeadCreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationStates", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversationStates_Sites_SiteId",
                    column: x => x.SiteId,
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ConversationStates_Leads_SiteId_LeadId",
                    columns: x => new { x.SiteId, x.LeadId },
                    principalTable: "Leads",
                    principalColumns: new[] { "SiteId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationStates_SiteId_WaId",
            table: "ConversationStates",
            columns: new[] { "SiteId", "WaId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConversationStates_SiteId_LeadId",
            table: "ConversationStates",
            columns: new[] { "SiteId", "LeadId" });
    }
}
