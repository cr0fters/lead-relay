using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209224000_AddLeadBotPauseFlag")]
public partial class AddLeadBotPauseFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsBotPaused",
            table: "Leads",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            """
            UPDATE `Leads`
            SET `IsBotPaused` = 1,
                `Status` = 'open'
            WHERE `Status` = 'paused';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsBotPaused",
            table: "Leads");
    }
}
