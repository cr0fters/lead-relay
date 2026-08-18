using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260818120000_AddOwnerEmailVerification")]
public partial class AddOwnerEmailVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EmailVerificationTokenHash",
            table: "OwnerAccounts",
            type: "varchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerificationTokenExpiresAtUtc",
            table: "OwnerAccounts",
            type: "datetime(6)",
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerificationSentAtUtc",
            table: "OwnerAccounts",
            type: "datetime(6)",
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerifiedAtUtc",
            table: "OwnerAccounts",
            type: "datetime(6)",
            nullable: true);

        // Existing accounts predate verification and must remain usable after deployment.
        migrationBuilder.Sql("UPDATE `OwnerAccounts` SET `EmailVerifiedAtUtc` = `UpdatedAtUtc` WHERE `EmailVerifiedAtUtc` IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmailVerificationTokenHash", table: "OwnerAccounts");
        migrationBuilder.DropColumn(name: "EmailVerificationTokenExpiresAtUtc", table: "OwnerAccounts");
        migrationBuilder.DropColumn(name: "EmailVerificationSentAtUtc", table: "OwnerAccounts");
        migrationBuilder.DropColumn(name: "EmailVerifiedAtUtc", table: "OwnerAccounts");
    }
}
