using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppOnboardingMigrationTests
{
    [Test]
    public async Task migration_rejects_duplicate_legacy_emails_before_altering_sites()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("ConnectionStrings__LeadRelay");
        if (string.IsNullOrWhiteSpace(configuredConnection))
            Assert.Ignore("Requires the CI MySQL connection string.");

        var databaseName = $"LeadRelayMigrationTest_{Guid.NewGuid():N}";
        var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection) { Database = "" };
        await using var admin = new MySqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"CREATE DATABASE `{databaseName}`;");

        try
        {
            var testBuilder = new MySqlConnectionStringBuilder(configuredConnection) { Database = databaseName };
            var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
                .UseMySql(
                    testBuilder.ConnectionString,
                    new MySqlServerVersion(new Version(8, 0, 0)),
                    mysql => mysql.MigrationsAssembly(typeof(LeadRelayDbContext).Assembly.FullName))
                .Options;
            await using var db = new LeadRelayDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260212211500_AddSiteWhatsAppPhoneNumberId");
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO `Sites`
                    (`Id`, `Name`, `BusinessSummary`, `AllowedDomainsJson`, `FieldsJson`, `IntroMessage`, `OwnerEmail`, `WhatsAppNumber`, `WhatsAppPhoneNumberId`)
                VALUES
                    ('preflight-a', 'A', NULL, '[]', '[]', NULL, 'duplicate@example.com', '', NULL),
                    ('preflight-b', 'B', NULL, '[]', '[]', NULL, 'duplicate@example.com', '', NULL);
                """);

            Assert.That(
                async () => await migrator.MigrateAsync("20260817120000_AddWhatsAppOnboardingAndHardening"),
                Throws.Exception);

            await using var check = new MySqlConnection(testBuilder.ConnectionString);
            await check.OpenAsync();
            await using var command = check.CreateCommand();
            command.CommandText =
                """
                SELECT `DATA_TYPE`
                FROM `INFORMATION_SCHEMA`.`COLUMNS`
                WHERE `TABLE_SCHEMA` = DATABASE()
                  AND `TABLE_NAME` = 'Sites'
                  AND `COLUMN_NAME` = 'OwnerEmail';
                """;
            var dataType = (string?)await command.ExecuteScalarAsync();
            Assert.That(dataType, Is.EqualTo("longtext"));
        }
        finally
        {
            Assert.That(databaseName, Does.StartWith("LeadRelayMigrationTest_"));
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS `{databaseName}`;");
        }
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
