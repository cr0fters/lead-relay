using LeadRelay.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LeadOwnerViewedMigrationTests
{
    [Test]
    public void migration_adds_view_timestamp_backfills_existing_leads_and_indexes_new_count()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyUp(builder);

        var column = builder.Operations.OfType<AddColumnOperation>().Single();
        Assert.That(column.Table, Is.EqualTo("Leads"));
        Assert.That(column.Name, Is.EqualTo("OwnerViewedAtUtc"));
        Assert.That(column.IsNullable, Is.True);

        var sql = builder.Operations.OfType<SqlOperation>().Single().Sql;
        Assert.That(sql, Does.Contain("`OwnerViewedAtUtc` = `CreatedAtUtc`"),
            "Historical leads should not appear as newly arrived after deployment.");

        var index = builder.Operations.OfType<CreateIndexOperation>().Single();
        Assert.That(index.Name, Is.EqualTo("IX_Leads_SiteId_OwnerViewedAtUtc"));
        Assert.That(index.Columns, Is.EqualTo(new[] { "SiteId", "OwnerViewedAtUtc" }));
    }

    [Test]
    public void down_migration_removes_index_before_column()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyDown(builder);

        Assert.That(builder.Operations[0], Is.TypeOf<DropIndexOperation>());
        Assert.That(builder.Operations[1], Is.TypeOf<DropColumnOperation>());
    }

    private sealed class TestableMigration : AddLeadOwnerViewedAt
    {
        public void ApplyUp(MigrationBuilder builder) => base.Up(builder);
        public void ApplyDown(MigrationBuilder builder) => base.Down(builder);
    }
}
