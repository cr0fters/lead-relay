using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Email;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Infrastructure.Time;
using LeadRelay.Web.AI;
using LeadRelay.Web.Leads;
using LeadRelay.Web.Security;
using LeadRelay.Web.WhatsApp;
using LeadRelay.Web.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHttpClient<WhatsAppClient>();
builder.Services.AddHttpClient<OpenAIClient>();


var app = builder.Build();

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
