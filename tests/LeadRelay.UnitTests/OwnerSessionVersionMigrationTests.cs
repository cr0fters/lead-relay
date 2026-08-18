using LeadRelay.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class OwnerSessionVersionMigrationTests
{
    [Test]
    public void migration_adds_positive_default_for_existing_owner_accounts()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyUp(builder);

        var column = builder.Operations.OfType<AddColumnOperation>().Single();
        Assert.That(column.Name, Is.EqualTo("SessionVersion"));
        Assert.That(column.Table, Is.EqualTo("OwnerAccounts"));
        Assert.That(column.IsNullable, Is.False);
        Assert.That(column.DefaultValue, Is.EqualTo(1L));
    }

    [Test]
    public void down_migration_removes_session_version()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        new TestableMigration().ApplyDown(builder);

        var column = builder.Operations.OfType<DropColumnOperation>().Single();
        Assert.That(column.Name, Is.EqualTo("SessionVersion"));
        Assert.That(column.Table, Is.EqualTo("OwnerAccounts"));
    }

    private sealed class TestableMigration : AddOwnerSessionVersion
    {
        public void ApplyUp(MigrationBuilder builder) => base.Up(builder);
        public void ApplyDown(MigrationBuilder builder) => base.Down(builder);
    }
}
