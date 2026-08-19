using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260819140000_TrackWidgetInstallation")]
public partial class TrackWidgetInstallation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "WidgetInstalledAtUtc",
            table: "OwnerAccounts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WidgetInstalledDomain",
            table: "OwnerAccounts",
            type: "varchar(255)",
            maxLength: 255,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WidgetInstalledAtUtc",
            table: "OwnerAccounts");

        migrationBuilder.DropColumn(
            name: "WidgetInstalledDomain",
            table: "OwnerAccounts");
    }
}
