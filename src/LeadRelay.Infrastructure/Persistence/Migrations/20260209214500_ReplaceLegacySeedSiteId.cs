using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209214500_ReplaceLegacySeedSiteId")]
public partial class ReplaceLegacySeedSiteId : Migration
{
    private const string LegacySiteId = "site_demo";
    private const string GuidSiteId = "2c7f9e0e-487f-4adf-8f0c-68c0f0d7b204";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            SET FOREIGN_KEY_CHECKS = 0;

            UPDATE `ConversationStates`
            SET `SiteId` = '{GuidSiteId}',
                `Id` = CONCAT('{GuidSiteId}', ':', `WaId`)
            WHERE `SiteId` = '{LegacySiteId}';

            UPDATE `Leads` SET `SiteId` = '{GuidSiteId}' WHERE `SiteId` = '{LegacySiteId}';
            UPDATE `Projects` SET `SiteId` = '{GuidSiteId}' WHERE `SiteId` = '{LegacySiteId}';
            UPDATE `Customers` SET `SiteId` = '{GuidSiteId}' WHERE `SiteId` = '{LegacySiteId}';
            UPDATE `OwnerAccounts` SET `SiteId` = '{GuidSiteId}' WHERE `SiteId` = '{LegacySiteId}';
            UPDATE `Sites` SET `Id` = '{GuidSiteId}' WHERE `Id` = '{LegacySiteId}';

            SET FOREIGN_KEY_CHECKS = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            SET FOREIGN_KEY_CHECKS = 0;

            UPDATE `ConversationStates`
            SET `SiteId` = '{LegacySiteId}',
                `Id` = CONCAT('{LegacySiteId}', ':', `WaId`)
            WHERE `SiteId` = '{GuidSiteId}';

            UPDATE `Leads` SET `SiteId` = '{LegacySiteId}' WHERE `SiteId` = '{GuidSiteId}';
            UPDATE `Projects` SET `SiteId` = '{LegacySiteId}' WHERE `SiteId` = '{GuidSiteId}';
            UPDATE `Customers` SET `SiteId` = '{LegacySiteId}' WHERE `SiteId` = '{GuidSiteId}';
            UPDATE `OwnerAccounts` SET `SiteId` = '{LegacySiteId}' WHERE `SiteId` = '{GuidSiteId}';
            UPDATE `Sites` SET `Id` = '{LegacySiteId}' WHERE `Id` = '{GuidSiteId}';

            SET FOREIGN_KEY_CHECKS = 1;
            """);
    }
}
