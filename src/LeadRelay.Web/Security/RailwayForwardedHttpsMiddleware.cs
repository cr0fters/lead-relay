namespace LeadRelay.Web.Security;

public sealed class RailwayForwardedHttpsMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    internal const string ForwardedProtoHeader = "X-Forwarded-Proto";
    private readonly bool _isRailway =
        !string.IsNullOrWhiteSpace(configuration["RAILWAY_ENVIRONMENT"]);

    public Task InvokeAsync(HttpContext context)
    {
        if (_isRailway)
        {
            var forwardedProto = context.Request.Headers[ForwardedProtoHeader];
            if (forwardedProto.Count == 1 &&
                string.Equals(forwardedProto[0], Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Scheme = Uri.UriSchemeHttps;
            }
        }

        return next(context);
    }
}
