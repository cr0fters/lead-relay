using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260819120000_AddLeadTestAttribution")]
public partial class AddLeadTestAttribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsTest",
            table: "Leads",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "LastOutboundTestRecipient",
            table: "WhatsAppConnections",
            type: "varchar(20)",
            maxLength: 20,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsTest",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "LastOutboundTestRecipient",
            table: "WhatsAppConnections");
    }
}
