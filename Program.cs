using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using WorkshopZagreb.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages: every .cshtml file in /Pages becomes a routable page automatically
builder.Services.AddRazorPages();

// SQLite: path is absolute so it works both locally and on Azure App Service.
// On Azure, "Run From Package" mounts the app folder read-only and replaces it
// on every deploy, so the db file CANNOT live there or it resets each push
// (and the seed data below would re-insert the sample workshops every time).
// /home is Azure App Service's persistent storage and survives deploys/restarts.
// To switch to Azure SQL later, just replace this block with UseSqlServer(...).
var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
var dataDir = isAzure ? "/home/data" : builder.Environment.ContentRootPath;
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "workshop.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("WorkshopDb")
        ?? $"Data Source={dbPath}"));

// Our custom file upload service
builder.Services.AddScoped<IFileService, FileService>();

// Email service — sends via Google Workspace SMTP
builder.Services.AddScoped<IEmailService, EmailService>();

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

// On Azure, admin-uploaded images are saved to /home/data/images (persistent
// storage, see FileService) instead of the read-only wwwroot — serve them
// at the same /images/... URLs so nothing else needs to change.
if (isAzure)
{
    Directory.CreateDirectory("/home/data/images");
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider("/home/data/images"),
        RequestPath = "/images"
    });
}
app.UseRouting();
app.UseSession();
app.MapRazorPages();

// Auto-create DB on first run — no CLI needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ReservedDays (
            Id    INTEGER PRIMARY KEY AUTOINCREMENT,
            Date  TEXT    NOT NULL,
            Label TEXT
        )");

    // IsArchived column added June 2026 — ALTER TABLE is a no-op if it already exists
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already present — safe to ignore */ }

    // IsPinned column added June 2026 — pinned workshops appear at the top with no date
    // (legacy: superseded by IsReservable below, kept only as the historical source for the backfill)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already present — safe to ignore */ }

    // IsReservable column added July 2026 — replaces IsPinned; marks a workshop as always-bookable
    // (not tied to specific dated occurrences) rather than "pinned to the top of the list"
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN IsReservable INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already present — safe to ignore */ }
    // BookingType column added July 2026 — how a reservable workshop is booked (e.g. 'webpage' link)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN BookingType TEXT"); }
    catch { /* column already present — safe to ignore */ }
    // BookingValue column added July 2026 — the URL/value paired with BookingType
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN BookingValue TEXT"); }
    catch { /* column already present — safe to ignore */ }

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS WorkshopOccurrences (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            WorkshopId INTEGER NOT NULL,
            Date       TEXT    NOT NULL,
            StartTime  TEXT    NOT NULL,
            EndTime    TEXT,
            EntrioUrl  TEXT,
            CreatedAt  TEXT    NOT NULL,
            FOREIGN KEY (WorkshopId) REFERENCES Workshops(Id) ON DELETE CASCADE
        )");

    // One-row-per-key marker table so one-time seeds/backfills stay one-time,
    // even if their result is later deleted by an admin (this fixes a real bug:
    // the old "seed birthday workshop if none currently exist" check would
    // recreate it on every deploy after someone deleted it).
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS SeedFlags (
            Key   TEXT PRIMARY KEY,
            Value INTEGER NOT NULL DEFAULT 1
        )");

    // One-time-only: seed IsReservable from the legacy IsPinned column. Guarded via SeedFlags
    // (rather than running unconditionally on every startup) so that once an admin changes a
    // workshop's IsReservable flag through the app, the next deploy/restart does not silently
    // stomp it back to the stale IsPinned value — that would recreate the exact "state reset
    // on redeploy" bug this whole migration exists to fix.
    var isReservableBackfillInserted = db.Database.ExecuteSqlRaw(
        "INSERT OR IGNORE INTO SeedFlags (Key, Value) VALUES ('IsReservableBackfilled', 1)");
    if (isReservableBackfillInserted > 0)
    {
        db.Database.ExecuteSqlRaw("UPDATE Workshops SET IsReservable = IsPinned");
    }

    db.Database.ExecuteSqlRaw(@"
        UPDATE Workshops
        SET BookingType = 'webpage', BookingValue = '/suradnja#upit'
        WHERE IsReservable = 1 AND BookingType IS NULL");

    var occurrencesBackfillInserted = db.Database.ExecuteSqlRaw(
        "INSERT OR IGNORE INTO SeedFlags (Key, Value) VALUES ('OccurrencesBackfilled', 1)");
    if (occurrencesBackfillInserted > 0)
    {
        try
        {
            // Legacy Date/StartTime/EndTime/EntrioUrl columns only exist on a database that
            // was created before Task 1's model split (they've been removed from the current
            // Workshop C# model, so EnsureCreated() never adds them to a brand-new database).
            // On a fresh database this throws "no such column" — which is fine, since a fresh
            // database has no legacy per-workshop dates to backfill into occurrences anyway.
            db.Database.ExecuteSqlRaw(@"
                INSERT INTO WorkshopOccurrences (WorkshopId, Date, StartTime, EndTime, EntrioUrl, CreatedAt)
                SELECT Id, Date, StartTime, EndTime, EntrioUrl, CreatedAt
                FROM Workshops
                -- Pinned/reservable workshops are excluded: their legacy Date/StartTime were
                -- meaningless sentinel values (e.g. 2099-01-01), not real occurrence dates.
                WHERE IsPinned = 0");
        }
        catch { /* legacy columns don't exist on a fresh database — nothing to backfill */ }
    }

    // If a reservable workshop already exists (migrated from the old IsPinned data
    // via the backfill above, or seeded by a prior version of this app), mark the
    // seed as already-consumed so we don't try to insert a second workshop with the
    // same Slug below (Slug has a unique index — a duplicate insert would throw and
    // crash the app on startup on any existing deployment).
    if (db.Workshops.Any(w => w.IsReservable))
    {
        db.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO SeedFlags (Key, Value) VALUES ('ReservableWorkshopSeeded', 1)");
    }

    var reservableSeedInserted = db.Database.ExecuteSqlRaw(
        "INSERT OR IGNORE INTO SeedFlags (Key, Value) VALUES ('ReservableWorkshopSeeded', 1)");
    if (reservableSeedInserted > 0)
    {
        db.Workshops.Add(new Workshop
        {
            Name = "Rođendanska radionica",
            Description = "Proslavite poseban dan na jedinstven način — rezervirajte naš prostor za svoju skupinu i zajedno naučite nešto novo. Odaberite temu radionice po želji i mi organiziramo sve ostalo.",
            InstagramPostUrl = "",
            Price = 25,
            MaxParticipants = 15,
            Slug = "rodendanska-radionica",
            IsReservable = true,
            BookingType = "webpage",
            BookingValue = "/suradnja#upit",
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS MenuCategories (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Name         TEXT    NOT NULL,
            MainCategory INTEGER NOT NULL,
            DisplayOrder INTEGER NOT NULL DEFAULT 0
        )");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS MenuItems (
            Id             INTEGER PRIMARY KEY AUTOINCREMENT,
            MenuCategoryId INTEGER NOT NULL,
            Name           TEXT    NOT NULL,
            Price          TEXT    NOT NULL DEFAULT '0',
            Ingredients    TEXT,
            IsAddon        INTEGER NOT NULL DEFAULT 0,
            DisplayOrder   INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (MenuCategoryId) REFERENCES MenuCategories(Id) ON DELETE CASCADE
        )");

    if (!db.MenuCategories.Any())
    {
        MenuSeed.Seed(db);
    }
}

app.Run();

// One-off seed of the original static menu content into MenuCategory/MenuItem rows,
// all under the "Pića" main category. "Hrana" starts empty — owners add it later.
static class MenuSeed
{
    public static void Seed(AppDbContext db)
    {
        int catOrder = 0;
        int catId = 0;

        MenuCategory NewCategory(string name)
        {
            var cat = new MenuCategory
            {
                Id = ++catId,
                Name = name,
                MainCategory = MainMenuCategory.Pica,
                DisplayOrder = catOrder++
            };
            db.MenuCategories.Add(cat);
            return cat;
        }

        void Items(MenuCategory cat, params (string Name, decimal Price, bool IsAddon)[] items)
        {
            int order = 0;
            foreach (var (name, price, isAddon) in items)
            {
                db.MenuItems.Add(new MenuItem
                {
                    MenuCategoryId = cat.Id,
                    Name = name,
                    Price = price,
                    IsAddon = isAddon,
                    DisplayOrder = order++
                });
            }
        }

        var kava = NewCategory("Kava / Broom Coffee Roasters");
        Items(kava,
            ("Espresso", 2.20m, false),
            ("Double espresso", 3.30m, false),
            ("Coffee with milk", 2.60m, false),
            ("Cortado", 2.30m, false),
            ("Cappuccino", 2.70m, false),
            ("Caffe latte", 3.10m, false),
            ("Americano", 3.30m, false),
            ("Flat White", 3.90m, false),
            ("Iced latte", 3.10m, false),
            ("Espresso tonic", 7.50m, false),
            ("Babyccino", 1.20m, false),
            ("Bademovo mlijeko", 0.50m, true),
            ("Zobeno mlijeko", 0.50m, true),
            ("Sojino mlijeko", 0.50m, true)
        );

        var filterKava = NewCategory("Filter kava");
        Items(filterKava,
            ("V60 1 cup", 5.50m, false),
            ("V60 2 cups", 7.00m, false),
            ("Batch brew / Maccamster", 3.00m, false),
            ("Cold brew", 3.50m, false)
        );

        var caj = NewCategory("Čaj");
        Items(caj,
            ("Organic matcha latte", 4.80m, false),
            ("Peppermint Mint organic tea", 2.50m, false),
            ("Chamomile organic tea", 2.50m, false),
            ("Organic Sencha tea", 2.50m, false),
            ("Premium Earl Grey", 2.50m, false),
            ("Willi Vanili tea", 2.50m, false),
            ("Hibiskus tea", 2.50m, false)
        );

        var sokovi = NewCategory("Sokovi");
        Items(sokovi,
            ("Fresh orange juice", 3.50m, false),
            ("Lemonade", 2.70m, false),
            ("Homemade ice tea", 3.60m, false),
            ("Workshop mix", 3.60m, false),
            ("Guuc juices", 3.70m, false)
        );

        var voda = NewCategory("Voda");
        Items(voda,
            ("Jana", 1.70m, false),
            ("Jamnica", 1.70m, false)
        );

        var pivo = NewCategory("Hrvatsko craft pivo");
        Items(pivo,
            ("Zeppelin ale", 3.60m, false),
            ("Zeppelin lager", 3.60m, false)
        );

        var vino = NewCategory("Vino");
        Items(vino,
            ("La vie en Rose", 4.60m, false),
            ("Villa chambre d'amour", 4.60m, false),
            ("Chocolatero", 4.60m, false),
            ("Pjenušac", 4.60m, false)
        );

        var pica = NewCategory("Pića");
        Items(pica,
            ("Mimosa", 4.10m, false),
            ("Hugo", 5.50m, false),
            ("Espresso Martini", 7.00m, false),
            ("Gin tonic", 5.70m, false),
            ("Tequila Clase Azul", 25.00m, false)
        );

        db.SaveChanges();
    }
}
