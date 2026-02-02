using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Email;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Infrastructure.Time;
using LeadRelay.Web.WhatsApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISiteRepository, InMemorySiteRepository>();
builder.Services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
builder.Services.AddSingleton<WhatsAppConversationService>();

builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.AddHttpClient<WhatsAppClient>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

app.UseStaticFiles();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
