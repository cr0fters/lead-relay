using LeadRelay.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class ProjectFollowUpMigrationTests
{
    [Test]
    public void migration_adds_nullable_follow_up_fields()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyUp(builder);

        var columns = builder.Operations.OfType<AddColumnOperation>().ToDictionary(x => x.Name);
        Assert.That(columns.Keys, Is.EquivalentTo(new[] { "OwnerNotes", "NextAction", "NextActionAtUtc" }));
        Assert.That(columns.Values.All(x => x.Table == "Projects" && x.IsNullable), Is.True);
        Assert.That(columns["OwnerNotes"].MaxLength, Is.EqualTo(4000));
        Assert.That(columns["NextAction"].MaxLength, Is.EqualTo(500));
        Assert.That(builder.Operations.OfType<CreateIndexOperation>(), Is.Empty);
    }

    private sealed class TestableMigration : AddProjectFollowUp
    {
        public void ApplyUp(MigrationBuilder builder) => base.Up(builder);
    }
}
