using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class LeadRelayDbContextFactory : IDesignTimeDbContextFactory<LeadRelayDbContext>
{
    private static readonly ServerVersion DesignTimeMySqlVersion = ServerVersion.Parse("8.0.36-mysql");

    public LeadRelayDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__LeadRelay") ??
            "Server=localhost;Port=3307;User ID=root;Password=root;Database=LeadRelay";

        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            // Design-time operations must not require a live DB connection.
            .UseMySql(connectionString, DesignTimeMySqlVersion)
            .Options;

        return new LeadRelayDbContext(options);
    }
}
