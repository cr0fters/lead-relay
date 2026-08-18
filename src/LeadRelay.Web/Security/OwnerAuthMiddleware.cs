namespace LeadRelay.Web.Security;

public sealed class OwnerAuthMiddleware(RequestDelegate next)
{
    public const string ContextKey = "OwnerAuth";

    private readonly RequestDelegate _next = next;

    public async Task Invoke(HttpContext context, OwnerSessionService sessions)
    {
        if (!context.Request.Path.StartsWithSegments("/owner", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/owner/login", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/owner/register", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/owner/password", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/owner/verify-email/confirm", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = sessions.GetSessionToken(context);
        var auth = await sessions.ValidateAsync(token, context.RequestAborted);
        if (auth is null)
        {
            sessions.SignOut(context);
            var encodedReturnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/owner/login?returnUrl={encodedReturnUrl}");
            return;
        }

        context.Items[ContextKey] = auth;
        await _next(context);
    }
}
