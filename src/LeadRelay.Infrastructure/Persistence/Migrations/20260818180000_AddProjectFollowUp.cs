using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260818180000_AddProjectFollowUp")]
public partial class AddProjectFollowUp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OwnerNotes",
            table: "Projects",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "NextAction",
            table: "Projects",
            type: "varchar(500)",
            maxLength: 500,
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextActionAtUtc",
            table: "Projects",
            type: "datetime(6)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OwnerNotes", table: "Projects");
        migrationBuilder.DropColumn(name: "NextAction", table: "Projects");
        migrationBuilder.DropColumn(name: "NextActionAtUtc", table: "Projects");
    }
}
