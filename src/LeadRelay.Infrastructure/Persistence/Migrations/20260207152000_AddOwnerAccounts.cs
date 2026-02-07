using System;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260207152000_AddOwnerAccounts")]
public partial class AddOwnerAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OwnerAccounts",
            columns: table => new
            {
                SiteId = table.Column<string>(type: "varchar(255)", nullable: false),
                PasswordHash = table.Column<string>(type: "longtext", nullable: true),
                ResetTokenHash = table.Column<string>(type: "longtext", nullable: true),
                ResetTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OwnerAccounts", x => x.SiteId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OwnerAccounts");
    }
}
