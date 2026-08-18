using LeadRelay.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerControlledProjectStagesMigrationTests
{
    [Test]
    public void migration_adds_stage_history_normalizes_legacy_values_and_indexes_filters()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyUp(builder);

        var added = builder.Operations.OfType<AddColumnOperation>().Single();
        Assert.That(added.Table, Is.EqualTo("Projects"));
        Assert.That(added.Name, Is.EqualTo("StageChangesJson"));
        Assert.That(added.IsNullable, Is.True, "The additive column must be deploy-safe for existing rows.");

        var sql = builder.Operations.OfType<SqlOperation>().Single().Sql;
        Assert.That(sql, Does.Contain("ELSE 'new'"),
            "Ambiguous legacy states must fall back to New instead of inventing a won/lost outcome.");
        Assert.That(sql, Does.Contain("`StageChangesJson` = '[]'"));

        var altered = builder.Operations.OfType<AlterColumnOperation>().Single();
        Assert.That(altered.Name, Is.EqualTo("StageChangesJson"));
        Assert.That(altered.IsNullable, Is.False);

        var indexes = builder.Operations.OfType<CreateIndexOperation>().Select(x => x.Name).ToList();
        Assert.That(indexes, Is.EquivalentTo(new[]
        {
            "IX_Leads_SiteId_CreatedAtUtc_Id",
            "IX_Projects_SiteId_Status"
        }));
    }

    private sealed class TestableMigration : AddOwnerControlledProjectStages
    {
        public void ApplyUp(MigrationBuilder builder) => base.Up(builder);
    }
}
