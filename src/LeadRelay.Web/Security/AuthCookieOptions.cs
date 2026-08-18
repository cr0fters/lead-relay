namespace LeadRelay.Web.Security;

public static class AuthCookieOptions
{
    public static CookieOptions Create(HttpContext context, IWebHostEnvironment environment, TimeSpan? maxAge = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            MaxAge = maxAge
        };
    }
}
