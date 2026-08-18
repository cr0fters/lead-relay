using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260818190000_AddLeadOwnerViewedAt")]
public partial class AddLeadOwnerViewedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "OwnerViewedAtUtc",
            table: "Leads",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.Sql("UPDATE `Leads` SET `OwnerViewedAtUtc` = `CreatedAtUtc` WHERE `OwnerViewedAtUtc` IS NULL;");

        migrationBuilder.CreateIndex(
            name: "IX_Leads_SiteId_OwnerViewedAtUtc",
            table: "Leads",
            columns: new[] { "SiteId", "OwnerViewedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Leads_SiteId_OwnerViewedAtUtc",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "OwnerViewedAtUtc",
            table: "Leads");
    }
}
