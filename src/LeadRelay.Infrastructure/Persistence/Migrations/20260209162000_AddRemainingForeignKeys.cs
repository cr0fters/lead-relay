using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209162000_AddRemainingForeignKeys")]
public partial class AddRemainingForeignKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ConversationStates_SiteId_LeadId",
            table: "ConversationStates",
            columns: new[] { "SiteId", "LeadId" });

        migrationBuilder.AddForeignKey(
            name: "FK_Customers_Sites_SiteId",
            table: "Customers",
            column: "SiteId",
            principalTable: "Sites",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Projects_Sites_SiteId",
            table: "Projects",
            column: "SiteId",
            principalTable: "Sites",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Leads_Sites_SiteId",
            table: "Leads",
            column: "SiteId",
            principalTable: "Sites",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ConversationStates_Sites_SiteId",
            table: "ConversationStates",
            column: "SiteId",
            principalTable: "Sites",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_OwnerAccounts_Sites_SiteId",
            table: "OwnerAccounts",
            column: "SiteId",
            principalTable: "Sites",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ConversationStates_Leads_SiteId_LeadId",
            table: "ConversationStates",
            columns: new[] { "SiteId", "LeadId" },
            principalTable: "Leads",
            principalColumns: new[] { "SiteId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Customers_Sites_SiteId",
            table: "Customers");

        migrationBuilder.DropForeignKey(
            name: "FK_Projects_Sites_SiteId",
            table: "Projects");

        migrationBuilder.DropForeignKey(
            name: "FK_Leads_Sites_SiteId",
            table: "Leads");

        migrationBuilder.DropForeignKey(
            name: "FK_ConversationStates_Sites_SiteId",
            table: "ConversationStates");

        migrationBuilder.DropForeignKey(
            name: "FK_OwnerAccounts_Sites_SiteId",
            table: "OwnerAccounts");

        migrationBuilder.DropForeignKey(
            name: "FK_ConversationStates_Leads_SiteId_LeadId",
            table: "ConversationStates");

        migrationBuilder.DropIndex(
            name: "IX_ConversationStates_SiteId_LeadId",
            table: "ConversationStates");
    }
}
