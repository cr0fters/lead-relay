using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Email;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Infrastructure.Time;
using LeadRelay.Web.AI;
using LeadRelay.Web.Leads;
using LeadRelay.Web.Messaging;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using LeadRelay.Web.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using System.Text.RegularExpressions;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.ValidateRequiredSecrets();

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(rateLimiting =>
{
    rateLimiting.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiting.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        }));
    rateLimiting.AddPolicy("lead-intake", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        }));
});

builder.Services.AddSingleton<IClock, SystemClock>();
var connectionString = builder.Configuration.GetConnectionString("LeadRelay");
builder.Services.AddDbContext<LeadRelayDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySql => mySql.MigrationsAssembly(typeof(LeadRelayDbContext).Assembly.FullName)));
builder.Services.AddScoped<ISiteRepository, EfSiteRepository>();
builder.Services.AddScoped<ILeadRepository, EfLeadRepository>();
builder.Services.Configure<PostmarkOptions>(builder.Configuration.GetSection("Postmark"));
builder.Services.AddTransient<ConsoleEmailSender>();
builder.Services.AddHttpClient<PostmarkEmailSender>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PostmarkOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.ApiBaseUrl)
        ? "https://api.postmarkapp.com"
        : options.ApiBaseUrl.Trim();
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
});
builder.Services.AddTransient<IEmailSender>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PostmarkOptions>>().Value;
    var hasPostmarkConfig =
        options.Enabled &&
        !string.IsNullOrWhiteSpace(options.ServerToken) &&
        !string.IsNullOrWhiteSpace(options.FromEmail);

    return hasPostmarkConfig
        ? sp.GetRequiredService<PostmarkEmailSender>()
        : sp.GetRequiredService<ConsoleEmailSender>();
});
builder.Services.AddScoped<WhatsAppConversationService>();
builder.Services.AddScoped<LeadCaptureService>();

builder.Services.Configure<ConversationOptions>(builder.Configuration.GetSection("Conversation"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection("AdminAuth"));
builder.Services.Configure<OwnerPortalOptions>(builder.Configuration.GetSection("OwnerPortal"));
builder.Services.AddScoped<OwnerSessionService>();
builder.Services.AddScoped<IOwnerPasswordAuthService, OwnerPasswordAuthService>();
builder.Services.AddScoped<IOwnerRegistrationService, OwnerRegistrationService>();
builder.Services.AddScoped<IOwnerEmailVerificationService, OwnerEmailVerificationService>();
builder.Services.AddScoped<IMessageChannel, WhatsAppMessageChannel>();
builder.Services.AddScoped<IMessageChannel, EmailMessageChannel>();
builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();
builder.Services.AddScoped<IWhatsAppWebhookGuard, WhatsAppWebhookGuard>();
builder.Services.AddSingleton<WhatsAppWebhookRateLimiter>();
builder.Services.AddScoped<WhatsAppSiteResolver>();
builder.Services.AddScoped<WhatsAppCredentialProtector>();
builder.Services.AddHttpClient<WhatsAppClient>();
builder.Services.AddHttpClient<WhatsAppOnboardingService>();
builder.Services.AddHttpClient<OpenAIClient>();


var app = builder.Build();

if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    var configuredProxies = builder.Configuration
        .GetSection("ForwardedHeaders:KnownProxies")
        .Get<string[]>() ?? [];
    if (configuredProxies.Length == 0)
        throw new InvalidOperationException("ForwardedHeaders:Enabled requires at least one ForwardedHeaders:KnownProxies IP address.");

    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
    };
    foreach (var configuredProxy in configuredProxies)
    {
        if (IPAddress.TryParse(configuredProxy, out var proxyAddress))
            forwardedHeadersOptions.KnownProxies.Add(proxyAddress);
        else
            throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains an invalid IP address: {configuredProxy}");
    }
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

if (app.Environment.IsDevelopment())
{
    await app.LogDatabaseInfoAsync(connectionString);
    await app.ApplyDatabaseMigrationsAsync();
    await app.ApplySeedDataAsync();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<AdminTokenMiddleware>();
app.UseMiddleware<OwnerAuthMiddleware>();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
