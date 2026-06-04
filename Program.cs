using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages: every .cshtml file in /Pages becomes a routable page automatically
builder.Services.AddRazorPages();

// SQLite: path is absolute so it works both locally and on Azure App Service.
// To switch to Azure SQL later, just replace this block with UseSqlServer(...).
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "workshop.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("WorkshopDb")
        ?? $"Data Source={dbPath}"));

// Our custom file upload service
builder.Services.AddScoped<IFileService, FileService>();

// Session for admin login
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();   // serves /wwwroot (css, js, images, videos)
app.UseRouting();
app.UseSession();
app.MapRazorPages();

// Auto-create DB on first run — no CLI needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Ensure any tables/columns added after initial creation also exist (safe to run repeatedly)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ReservedDays (
            Id    INTEGER PRIMARY KEY AUTOINCREMENT,
            Date  TEXT    NOT NULL,
            Label TEXT
        )");

    // IsArchived column added June 2026 — ALTER TABLE is a no-op if it already exists
    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Workshops ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0");
    }
    catch { /* column already present — safe to ignore */ }
}

app.Run();
