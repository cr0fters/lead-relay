using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209134500_EnforceLeadProjectIntegrityAndLeanAttribution")]
public partial class EnforceLeadProjectIntegrityAndLeanAttribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Leads",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext");

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Projects",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext");

        migrationBuilder.DropColumn(
            name: "PageUrl",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "Referrer",
            table: "Leads");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_SiteId_Id",
            table: "Customers",
            columns: new[] { "SiteId", "Id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Projects_SiteId_Id",
            table: "Projects",
            columns: new[] { "SiteId", "Id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Leads_SiteId_Id",
            table: "Leads",
            columns: new[] { "SiteId", "Id" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Projects_Customers_SiteId_CustomerId",
            table: "Projects",
            columns: new[] { "SiteId", "CustomerId" },
            principalTable: "Customers",
            principalColumns: new[] { "SiteId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Leads_Customers_SiteId_CustomerId",
            table: "Leads",
            columns: new[] { "SiteId", "CustomerId" },
            principalTable: "Customers",
            principalColumns: new[] { "SiteId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Leads_Projects_SiteId_ProjectId",
            table: "Leads",
            columns: new[] { "SiteId", "ProjectId" },
            principalTable: "Projects",
            principalColumns: new[] { "SiteId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Projects_Customers_SiteId_CustomerId",
            table: "Projects");

        migrationBuilder.DropForeignKey(
            name: "FK_Leads_Customers_SiteId_CustomerId",
            table: "Leads");

        migrationBuilder.DropForeignKey(
            name: "FK_Leads_Projects_SiteId_ProjectId",
            table: "Leads");

        migrationBuilder.DropIndex(
            name: "IX_Customers_SiteId_Id",
            table: "Customers");

        migrationBuilder.DropIndex(
            name: "IX_Projects_SiteId_Id",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_Leads_SiteId_Id",
            table: "Leads");

        migrationBuilder.AddColumn<string>(
            name: "PageUrl",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Referrer",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Leads",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Projects",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32);
    }
}
