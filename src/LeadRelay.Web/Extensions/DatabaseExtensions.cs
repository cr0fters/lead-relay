using LeadRelay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace LeadRelay.Web.Extensions;

public static class DatabaseExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeadRelayDbContext>();
        await db.Database.MigrateAsync();
    }

    public static async Task ApplySeedDataAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeadRelayDbContext>();
        await SeedData.EnsureSeededAsync(db, CancellationToken.None);
    }

    public static async Task LogDatabaseInfoAsync(this IApplicationBuilder app, string? connectionString)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("LeadRelay.Database");
        var db = scope.ServiceProvider.GetRequiredService<LeadRelayDbContext>();

        var safeConnection = string.IsNullOrWhiteSpace(connectionString)
            ? "<null>"
            : Regex.Replace(connectionString, @"(Password|Pwd)\s*=\s*[^;]+", "$1=***", RegexOptions.IgnoreCase);

        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DATABASE()";
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
