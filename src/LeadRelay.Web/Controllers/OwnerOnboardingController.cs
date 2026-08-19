using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadRelay.Web.Controllers;

public sealed class OwnerOnboardingController(
    ISiteRepository sites,
    LeadRelayDbContext db,
    WhatsAppOnboardingService onboarding,
    IConfiguration configuration,
    IOptions<WhatsAppOptions> whatsAppOptions) : Controller
{
    private readonly WhatsAppOptions _whatsAppOptions = whatsAppOptions.Value;

    [HttpGet("/owner/onboarding")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");
        var model = await BuildModelAsync(auth, ct);
        model.Success = TempData["Onboarding.Success"] as string;
        model.Error = TempData["Onboarding.Error"] as string;
        return View(model);
    }

    [HttpPost("/owner/onboarding/whatsapp/embedded-signup/complete")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    [RequestFormLimits(ValueLengthLimit = 8192)]
    public async Task<IActionResult> CompleteEmbeddedSignup(
        [FromForm] string? authorizationCode,
        [FromForm] string? wabaId,
        [FromForm] string? phoneNumberId,
        CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var result = await onboarding.ConnectEmbeddedSignupAsync(
            auth.SiteId,
            new WhatsAppEmbeddedSignupRequest(authorizationCode, wabaId, phoneNumberId),
            ct);
        if (result.Succeeded)
            TempData["Onboarding.Success"] = "WhatsApp Business App connected through Meta. Send an inbound message to verify webhook delivery.";
        else
            TempData["Onboarding.Error"] = result.Error ?? "Unable to connect WhatsApp.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/owner/onboarding/whatsapp/test")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TestWhatsApp([FromForm] string? recipient, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");

        var result = await onboarding.SendTestAsync(auth.SiteId, recipient, ct);
        if (result.Succeeded)
            TempData["Onboarding.Success"] = "Test message sent successfully.";
        else
            TempData["Onboarding.Error"] = result.Error ?? "Unable to send the test message.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/owner/onboarding/whatsapp/disconnect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectWhatsApp(CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");
        await onboarding.DisconnectAsync(auth.SiteId, ct);
        TempData["Onboarding.Success"] = "WhatsApp disconnected. The stored credential was removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/owner/onboarding/domain")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDomain([FromForm] string? domain, CancellationToken ct)
    {
        var auth = GetAuthContext();
        if (auth is null) return Redirect("/owner/login");
        var site = await sites.GetByIdAsync(auth.SiteId, ct);
        if (site is null) return NotFound();

        var normalizedDomain = WebsiteDomainNormalizer.Normalize(domain);
        if (normalizedDomain is null)
        {
            TempData["Onboarding.Error"] = "Enter a valid website domain, for example example.com.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedDomains = WebsiteDomainNormalizer.NormalizeList(
            string.Join('\n', site.AllowedDomains.Append(normalizedDomain)));
        if (normalizedDomains.Error is not null)
        {
            TempData["Onboarding.Error"] = normalizedDomains.Error;
            return RedirectToAction(nameof(Index));
        }

        var updated = CopySiteWithDomains(site, normalizedDomains.Domains);
        await sites.UpsertAsync(updated, ct);
        TempData["Onboarding.Success"] = "Website domain saved. Your widget snippet is ready.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<OwnerOnboardingModel> BuildModelAsync(OwnerAuthContext auth, CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(auth.SiteId, ct) ?? throw new InvalidOperationException("Owner site not found.");
        var connection = await onboarding.GetSummaryAsync(auth.SiteId, ct);
        var account = await db.OwnerAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SiteId == auth.SiteId, ct);
        var hasWhatsAppLead = await db.Leads.AsNoTracking()
            .AnyAsync(x => x.SiteId == auth.SiteId && x.Channel == "whatsapp" && !x.IsTest, ct);
        var publicBaseUrl = (configuration["PublicBaseUrl"] ?? "").TrimEnd('/');
        var widgetUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : publicBaseUrl;
        var whatsAppDigits = new string((site.WhatsAppNumber ?? "").Where(char.IsDigit).ToArray());
        var isWidgetInstalled = account?.WidgetInstalledAtUtc.HasValue == true &&
            !string.IsNullOrWhiteSpace(account.WidgetInstalledDomain) &&
            DomainAllowList.IsAllowedDomain(
                site.AllowedDomains,
                $"https://{account.WidgetInstalledDomain}",
                null);

        return new OwnerOnboardingModel
        {
            SiteId = auth.SiteId,
            SiteName = site.Name,
            OwnerEmail = auth.OwnerEmail,
            Connection = connection,
            AllowedDomains = site.AllowedDomains,
            IsEmailVerified = account?.EmailVerifiedAtUtc.HasValue == true,
            WidgetInstalledAtUtc = account?.WidgetInstalledAtUtc,
            WidgetInstalledDomain = account?.WidgetInstalledDomain,
            IsWidgetInstalled = isWidgetInstalled,
            HasFirstLead = hasWhatsAppLead,
            IsEmbeddedSignupAvailable = _whatsAppOptions.IsEmbeddedSignupConfigured,
            MetaAppId = _whatsAppOptions.MetaAppId?.Trim(),
            EmbeddedSignupConfigurationId = _whatsAppOptions.EmbeddedSignupConfigurationId?.Trim(),
            EmbeddedSignupVersion = _whatsAppOptions.EmbeddedSignupVersion?.Trim() ?? "v4",
            GraphApiVersion = _whatsAppOptions.GraphApiVersion?.Trim() ?? "v23.0",
            WidgetSnippet = $"<script src=\"{widgetUrl}/widget/bootstrap.js?siteId={site.Id}\"></script>",
            WhatsAppPreviewUrl = string.IsNullOrWhiteSpace(whatsAppDigits)
                ? null
                : $"https://wa.me/{whatsAppDigits}?text={Uri.EscapeDataString("Hi")}"
        };
    }

    private OwnerAuthContext? GetAuthContext()
        => HttpContext.Items.TryGetValue(OwnerAuthMiddleware.ContextKey, out var value)
            ? value as OwnerAuthContext
            : null;

    private static Site CopySiteWithDomains(Site site, IReadOnlyList<string> domains) => new()
    {
        Id = site.Id,
        Name = site.Name,
        BusinessSummary = site.BusinessSummary,
        AllowedDomains = domains,
        Fields = site.Fields,
        IntroMessage = site.IntroMessage,
        OwnerEmail = site.OwnerEmail,
        WhatsAppNumber = site.WhatsAppNumber,
        WhatsAppPhoneNumberId = site.WhatsAppPhoneNumberId
    };

    public sealed class OwnerOnboardingModel
    {
        public string SiteId { get; set; } = "";
        public string SiteName { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public WhatsAppConnectionSummary Connection { get; set; } = new(false, "not_connected", null, null, null, null, null, null, null, null);
        public IReadOnlyList<string> AllowedDomains { get; set; } = [];
        public bool IsEmailVerified { get; set; }
        public DateTimeOffset? WidgetInstalledAtUtc { get; set; }
        public string? WidgetInstalledDomain { get; set; }
        public bool IsWidgetInstalled { get; set; }
        public bool HasFirstLead { get; set; }
        public bool IsEmbeddedSignupAvailable { get; set; }
        public string? MetaAppId { get; set; }
        public string? EmbeddedSignupConfigurationId { get; set; }
        public string EmbeddedSignupVersion { get; set; } = "v4";
        public string GraphApiVersion { get; set; } = "v23.0";
        public string WidgetSnippet { get; set; } = "";
        public string? WhatsAppPreviewUrl { get; set; }
        public string? Error { get; set; }
        public string? Success { get; set; }
        public bool IsWhatsAppReady => Connection.IsConnected && Connection.IsWebhookVerified && Connection.HasSuccessfulTest;
        public bool IsSubscriptionActive => false;
        public int CompletedSteps => new[]
        {
            IsEmailVerified,
            IsWhatsAppReady,
            IsWidgetInstalled,
            IsSubscriptionActive,
            HasFirstLead
        }.Count(x => x);
    }
}
