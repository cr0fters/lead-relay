using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class LeadRelayDbContextFactory : IDesignTimeDbContextFactory<LeadRelayDbContext>
{
    public LeadRelayDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__LeadRelay") ??
            "Server=localhost;Port=3307;User ID=root;Password=root;Database=LeadRelay";

        var options = new DbContextOptionsBuilder<LeadRelayDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new LeadRelayDbContext(options);
    }
}
