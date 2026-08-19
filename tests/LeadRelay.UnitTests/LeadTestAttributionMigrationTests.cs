using LeadRelay.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LeadTestAttributionMigrationTests
{
    [Test]
    public void migration_defaults_existing_leads_to_real_and_tracks_the_configured_test_recipient()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyUp(builder);

        var columns = builder.Operations.OfType<AddColumnOperation>().ToList();
        var isTest = columns.Single(x => x.Table == "Leads" && x.Name == "IsTest");
        Assert.That(isTest.IsNullable, Is.False);
        Assert.That(isTest.DefaultValue, Is.False);

        var recipient = columns.Single(x => x.Table == "WhatsAppConnections" && x.Name == "LastOutboundTestRecipient");
        Assert.That(recipient.IsNullable, Is.True);
        Assert.That(recipient.MaxLength, Is.EqualTo(20));
    }

    [Test]
    public void down_migration_removes_both_attribution_columns()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyDown(builder);

        var columns = builder.Operations.OfType<DropColumnOperation>().ToList();
        Assert.That(columns.Select(x => (x.Table, x.Name)), Is.EquivalentTo(new[]
        {
            ("Leads", "IsTest"),
            ("WhatsAppConnections", "LastOutboundTestRecipient")
        }));
    }

    private sealed class TestableMigration : AddLeadTestAttribution
    {
        public void ApplyUp(MigrationBuilder builder) => base.Up(builder);
        public void ApplyDown(MigrationBuilder builder) => base.Down(builder);
    }
}
