using LeadRelay.Application.Abstractions;
using LeadRelay.Infrastructure.Email;
using LeadRelay.Infrastructure.Persistence;
using LeadRelay.Infrastructure.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISiteRepository, InMemorySiteRepository>();
builder.Services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

app.UseStaticFiles();
app.MapControllers();

app.Run();
