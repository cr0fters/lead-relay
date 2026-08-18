using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace LeadRelay.Web.Controllers;

[ApiController]
public sealed partial class VersionController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("/.well-known/leadrelay-version")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Get()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";

        return Ok(new VersionResponse(
            Service: "leadrelay",
            CommitSha: ResolveCommitSha(configuration),
            Version: ResolveAssemblyVersion()));
    }

    private static string? ResolveCommitSha(IConfiguration source)
    {
        var candidates = new[]
        {
            source["RAILWAY_GIT_COMMIT_SHA"],
            source["GIT_COMMIT_SHA"],
            ResolveAssemblyCommitSha()
        };

        return candidates
            .Select(x => (x ?? "").Trim().ToLowerInvariant())
            .FirstOrDefault(x => GitShaRegex().IsMatch(x));
    }

    private static string ResolveAssemblyVersion()
    {
        var assembly = typeof(VersionController).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private static string? ResolveAssemblyCommitSha()
    {
        var informationalVersion = ResolveAssemblyVersion();
        var separator = informationalVersion.LastIndexOf('+');
        return separator >= 0 && separator < informationalVersion.Length - 1
            ? informationalVersion[(separator + 1)..]
            : null;
    }

    [GeneratedRegex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitShaRegex();

    public sealed record VersionResponse(string Service, string? CommitSha, string Version);
}
