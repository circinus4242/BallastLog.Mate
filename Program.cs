using BallastLog.Mate.Data;
using BallastLog.Mate.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.WebHost.UseUrls("http://127.0.0.1:7777");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var baseDir = AppContext.BaseDirectory;
    var dataDir = Path.Combine(baseDir, "data");
    Directory.CreateDirectory(dataDir);
    var dbFile = Path.Combine(dataDir, "ballast.db");
    opt.UseSqlite($"Data Source={dbFile}");
});

builder.Services.AddRazorPages();

builder.Services.AddScoped<RecalcService>();
builder.Services.AddScoped<ProfileIoService>();
builder.Services.AddScoped<CsvService>();
builder.Services.AddHostedService<BrowserLauncherHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.ShipProfiles.AnyAsync())
        db.ShipProfiles.Add(new BallastLog.Mate.Models.ShipProfile { Id = 1 });
    await db.SaveChangesAsync();
}

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();

app.Run();