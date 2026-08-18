using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260818150000_AddOwnerLegalAcceptance")]
public partial class AddOwnerLegalAcceptance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LegalDocumentsAcceptedAtUtc",
            table: "OwnerAccounts",
            type: "datetime(6)",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "TermsVersion",
            table: "OwnerAccounts",
            type: "varchar(32)",
            maxLength: 32,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "PrivacyPolicyVersion",
            table: "OwnerAccounts",
            type: "varchar(32)",
            maxLength: 32,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LegalDocumentsAcceptedAtUtc", table: "OwnerAccounts");
        migrationBuilder.DropColumn(name: "TermsVersion", table: "OwnerAccounts");
        migrationBuilder.DropColumn(name: "PrivacyPolicyVersion", table: "OwnerAccounts");
    }
}
