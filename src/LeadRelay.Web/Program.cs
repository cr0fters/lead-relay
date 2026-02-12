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
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.ValidateRequiredSecrets();

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IClock, SystemClock>();
var connectionString = builder.Configuration.GetConnectionString("LeadRelay");
builder.Services.AddDbContext<LeadRelayDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySql => mySql.MigrationsAssembly(typeof(LeadRelayDbContext).Assembly.FullName)));
builder.Services.AddScoped<ISiteRepository, EfSiteRepository>();
builder.Services.AddScoped<ILeadRepository, EfLeadRepository>();
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
builder.Services.AddScoped<WhatsAppConversationService>();
builder.Services.AddScoped<LeadCaptureService>();

builder.Services.Configure<ConversationOptions>(builder.Configuration.GetSection("Conversation"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection("AdminAuth"));
builder.Services.Configure<OwnerPortalOptions>(builder.Configuration.GetSection("OwnerPortal"));
builder.Services.AddScoped<OwnerSessionService>();
builder.Services.AddScoped<IOwnerPasswordAuthService, OwnerPasswordAuthService>();
builder.Services.AddScoped<IMessageChannel, WhatsAppMessageChannel>();
builder.Services.AddScoped<IMessageChannel, EmailMessageChannel>();
builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();
builder.Services.AddHttpClient<WhatsAppClient>();
builder.Services.AddHttpClient<OpenAIClient>();


var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

if (app.Environment.IsDevelopment())
{
    await app.LogDatabaseInfoAsync(connectionString);
    await app.ApplyDatabaseMigrationsAsync();
    await app.ApplySeedDataAsync();
}

app.UseStaticFiles();
app.UseMiddleware<AdminTokenMiddleware>();
app.UseMiddleware<OwnerAuthMiddleware>();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
