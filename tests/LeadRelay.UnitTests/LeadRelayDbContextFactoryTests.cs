using LeadRelay.Infrastructure.Persistence;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LeadRelayDbContextFactoryTests
{
    [Test]
    public void create_db_context_does_not_require_live_database_connection()
    {
        var previous = Environment.GetEnvironmentVariable("ConnectionStrings__LeadRelay");
        Environment.SetEnvironmentVariable("ConnectionStrings__LeadRelay", "Server=unreachable-host.invalid;Port=3306;User ID=root;Password=root;Database=LeadRelay");

        try
        {
            var factory = new LeadRelayDbContextFactory();
            using var dbContext = factory.CreateDbContext([]);

            Assert.That(dbContext, Is.Not.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__LeadRelay", previous);
        }
    }
}
