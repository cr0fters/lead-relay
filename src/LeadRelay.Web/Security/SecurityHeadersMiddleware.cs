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
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "upgrade-insecure-requests";

    internal const string EmbeddedSignupContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "connect-src 'self' https://www.facebook.com https://graph.facebook.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "frame-src https://www.facebook.com https://web.facebook.com https://staticxx.facebook.com; " +
        "img-src 'self' data:; " +
        "object-src 'none'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://connect.facebook.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "upgrade-insecure-requests";

    public async Task InvokeAsync(HttpContext context)
    {
        var applySecurityHeaders = !environment.IsDevelopment();
        var isHttps = context.Request.IsHttps;
        var contentSecurityPolicy = context.Request.Path.StartsWithSegments("/owner/onboarding")
            ? EmbeddedSignupContentSecurityPolicy
            : ContentSecurityPolicy;
        if (applySecurityHeaders)
        {
            ApplyHeaders(context.Response, isHttps, contentSecurityPolicy);
            context.Response.OnStarting(() =>
            {
                ApplyHeaders(context.Response, isHttps, contentSecurityPolicy);
                return Task.CompletedTask;
            });
        }

        await next(context);

        if (applySecurityHeaders && !context.Response.HasStarted)
            ApplyHeaders(context.Response, isHttps, contentSecurityPolicy);
    }

    private static void ApplyHeaders(HttpResponse response, bool isHttps, string contentSecurityPolicy)
    {
        var headers = response.Headers;
        headers["Content-Security-Policy"] = contentSecurityPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=()";

        if (isHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000";
    }
}
