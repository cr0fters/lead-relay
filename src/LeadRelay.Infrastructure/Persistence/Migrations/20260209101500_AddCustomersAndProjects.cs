using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209101500_AddCustomersAndProjects")]
public partial class AddCustomersAndProjects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                SiteId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                Name = table.Column<string>(type: "longtext", nullable: true),
                Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                Phone = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                ExternalContactId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                SiteId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                CustomerId = table.Column<Guid>(type: "char(36)", nullable: false),
                Name = table.Column<string>(type: "longtext", nullable: false),
                Summary = table.Column<string>(type: "longtext", nullable: true),
                Status = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "CustomerId",
            table: "Leads",
            type: "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            table: "Leads",
            type: "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "Leads",
            type: "longtext",
            nullable: false,
            defaultValue: "open");

        migrationBuilder.AlterColumn<string>(
            name: "SiteId",
            table: "Leads",
            type: "varchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_SiteId_Email",
            table: "Customers",
            columns: new[] { "SiteId", "Email" });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_SiteId_ExternalContactId",
            table: "Customers",
            columns: new[] { "SiteId", "ExternalContactId" });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_SiteId_Phone",
            table: "Customers",
            columns: new[] { "SiteId", "Phone" });

        migrationBuilder.CreateIndex(
            name: "IX_Leads_SiteId_CustomerId",
            table: "Leads",
            columns: new[] { "SiteId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_Leads_SiteId_ProjectId",
            table: "Leads",
            columns: new[] { "SiteId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_SiteId_CustomerId",
            table: "Projects",
            columns: new[] { "SiteId", "CustomerId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Customers");
        migrationBuilder.DropTable(name: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_Leads_SiteId_CustomerId",
            table: "Leads");

        migrationBuilder.DropIndex(
            name: "IX_Leads_SiteId_ProjectId",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "CustomerId",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "ProjectId",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "Leads");

        migrationBuilder.AlterColumn<string>(
            name: "SiteId",
            table: "Leads",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldMaxLength: 255);
    }
}
