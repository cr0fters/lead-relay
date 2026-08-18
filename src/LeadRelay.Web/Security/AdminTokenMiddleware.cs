using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Security;

public sealed class AdminTokenMiddleware(
    RequestDelegate next,
    IOptions<AdminAuthOptions> options,
    IWebHostEnvironment environment)
{
    private readonly RequestDelegate _next = next;
    private readonly AdminAuthOptions _options = options.Value;

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/admin/login", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var configuredToken = _options.Token?.Trim();
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            await RejectAsync(context, isApiRequest: IsApiRequest(context));
            return;
        }

        var requestToken = GetToken(context);
        if (!TokenMatches(configuredToken, requestToken))
        {
            await RejectAsync(context, isApiRequest: IsApiRequest(context));
            return;
        }

        PersistTokenCookie(context, requestToken!);
        await _next(context);
    }

    private string? GetToken(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_options.HeaderName, out var headerToken) && !string.IsNullOrWhiteSpace(headerToken))
            return headerToken.ToString().Trim();

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();

        if (context.Request.Cookies.TryGetValue(_options.CookieName, out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
            return cookieToken.Trim();

        if (context.Request.Query.TryGetValue(_options.QueryParameterName, out var queryToken) && !string.IsNullOrWhiteSpace(queryToken))
            return queryToken.ToString().Trim();

        return null;
    }

    private void PersistTokenCookie(HttpContext context, string token)
    {
        context.Response.Cookies.Append(_options.CookieName, token, AuthCookieOptions.Create(context, environment));
    }

    private static bool TokenMatches(string expected, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }

    private static bool IsApiRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/admin/api", StringComparison.OrdinalIgnoreCase);
    }

    private static Task RejectAsync(HttpContext context, bool isApiRequest)
    {
        if (!isApiRequest)
        {
            var encoded = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/admin/login?returnUrl={encoded}");
            return Task.CompletedTask;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
