using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260818170000_AddOwnerControlledProjectStages")]
public partial class AddOwnerControlledProjectStages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StageChangesJson",
            table: "Projects",
            type: "longtext",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE `Projects`
            SET `Status` = CASE
                WHEN LOWER(TRIM(`Status`)) IN ('new', 'qualified', 'contacted', 'won', 'lost')
                    THEN LOWER(TRIM(`Status`))
                ELSE 'new'
            END,
            `StageChangesJson` = '[]';
            """);

        migrationBuilder.AlterColumn<string>(
            name: "StageChangesJson",
            table: "Projects",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Leads_SiteId_CreatedAtUtc_Id",
            table: "Leads",
            columns: new[] { "SiteId", "CreatedAtUtc", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_SiteId_Status",
            table: "Projects",
            columns: new[] { "SiteId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Leads_SiteId_CreatedAtUtc_Id", table: "Leads");
        migrationBuilder.DropIndex(name: "IX_Projects_SiteId_Status", table: "Projects");
        migrationBuilder.DropColumn(name: "StageChangesJson", table: "Projects");
    }
}
