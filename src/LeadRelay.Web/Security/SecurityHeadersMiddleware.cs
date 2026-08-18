namespace LeadRelay.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    internal const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "connect-src 'self'; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "frame-src 'none'; " +
        "img-src 'self' data:; " +
        "object-src 'none'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://unpkg.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "upgrade-insecure-requests";

    public Task InvokeAsync(HttpContext context)
    {
        if (!environment.IsDevelopment())
        {
            var isHttps = context.Request.IsHttps;
            ApplyHeaders(context.Response, isHttps);
            context.Response.OnStarting(() =>
            {
                ApplyHeaders(context.Response, isHttps);
                return Task.CompletedTask;
            });
        }

        return next(context);
    }

    private static void ApplyHeaders(HttpResponse response, bool isHttps)
    {
        var headers = response.Headers;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=()";

        if (isHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000";
    }
}
