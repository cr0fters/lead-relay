using LeadRelay.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerLegalAcceptanceMigrationTests
{
    [Test]
    public void migration_adds_nullable_acceptance_audit_columns_without_backfilling_consent()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyUp(builder);

        var columns = builder.Operations.OfType<AddColumnOperation>().ToDictionary(x => x.Name);
        Assert.That(columns.Keys, Is.EquivalentTo(new[]
        {
            "LegalDocumentsAcceptedAtUtc",
            "TermsVersion",
            "PrivacyPolicyVersion"
        }));
        Assert.That(columns.Values.All(x => x.Table == "OwnerAccounts"), Is.True);
        Assert.That(columns.Values.All(x => x.IsNullable), Is.True);
        Assert.That(columns["TermsVersion"].MaxLength, Is.EqualTo(32));
        Assert.That(columns["PrivacyPolicyVersion"].MaxLength, Is.EqualTo(32));
        Assert.That(builder.Operations.OfType<SqlOperation>(), Is.Empty,
            "Existing accounts must not be falsely backfilled as having accepted the documents.");
    }

    [Test]
    public void down_migration_removes_only_acceptance_audit_columns()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyDown(builder);

        var drops = builder.Operations.OfType<DropColumnOperation>().ToList();
        Assert.That(builder.Operations, Has.Count.EqualTo(3));
        Assert.That(drops.Select(x => x.Name), Is.EquivalentTo(new[]
        {
            "LegalDocumentsAcceptedAtUtc",
            "TermsVersion",
            "PrivacyPolicyVersion"
        }));
        Assert.That(drops.All(x => x.Table == "OwnerAccounts"), Is.True);
    }

    private sealed class TestableMigration : AddOwnerLegalAcceptance
    {
        public void ApplyUp(MigrationBuilder builder) => base.Up(builder);
        public void ApplyDown(MigrationBuilder builder) => base.Down(builder);
    }
}
