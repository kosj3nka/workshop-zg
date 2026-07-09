# Reservable Workshops, Occurrences, Homepage & Icon Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `Workshop` into a template + `WorkshopOccurrence` (one row per date), rename the pinned concept to "reservable" with a configurable Book button (email/webpage), fix the pinned-reseed-on-deploy bug and the silent email-send-failure bug, reorder the homepage, add a 2-month upcoming-workshops window, and icon-ify contact/social links.

**Architecture:** `Workshop` keeps shared content (name, description, images, host, price, reservable/booking fields). A new `WorkshopOccurrence` table holds one row per date (date, time, ticket link) with a FK to `Workshop`. Schema changes follow this project's existing convention — raw guarded SQL in `Program.cs` on startup, additive only, no `dotnet ef` migrations. The dead `PinnedWorkshop` model/table/admin page is deleted.

**Tech Stack:** ASP.NET Core 8 Razor Pages, EF Core 8 + SQLite, vanilla CSS/JS (no build step).

**Testing note:** This project has no automated test project (no xUnit/NUnit anywhere in the repo). Every task's verification step is therefore a manual check via the running dev server (`dotnet run`, or the `preview_start`/`preview_*` tools) rather than a unit test — consistent with how this codebase has always been verified. Do not add a new test framework as part of this plan; that would be an unrelated, unrequested scope expansion.

**Reference:** Design spec at `docs/superpowers/specs/2026-07-07-reservable-workshops-and-occurrences-design.md`.

---

## File Structure

| File | Responsibility |
|---|---|
| `Models/Workshop.cs` | Template: shared content + reservable/booking fields. Rewritten. |
| `Models/WorkshopOccurrence.cs` | **New.** One date/time/ticket-link row per workshop. |
| `Models/PinnedWorkshop.cs` | **Deleted.** Dead code. |
| `Data/AppDbContext.cs` | Add `WorkshopOccurrence` DbSet + cascade config, remove `PinnedWorkshop` DbSet, update seed data shape. |
| `Program.cs` | Schema creation for `WorkshopOccurrences`, `IsReservable`/`BookingType`/`BookingValue` columns, one-time backfill, fixed seed-once logic (bug fix), removed `PinnedWorkshops` table/seed code. |
| `Helpers/GoogleCalendarHelper.cs` | Signature takes `WorkshopOccurrence` instead of reading date/time off `Workshop`. |
| `Services/EmailService.cs` | Silent-failure fix (bug fix) + `SendWorkshopAnnouncementAsync` takes the occurrence too. |
| `Pages/Admin/Workshops/Edit.cshtml(.cs)` | Rewritten: no date/time/ticket fields on the main form; inline occurrence list + add form; reservable Book-method fields. |
| `Pages/Admin/Index.cshtml(.cs)` | "Pinned" → "Reservable" labels; upcoming row shows next occurrence + count. |
| `Pages/Admin/Pinned/` | **Deleted.** Dead code. |
| `Pages/Workshops/Index.cshtml(.cs)` | One card per workshop; reservable cards get a Book button. |
| `Pages/Workshops/Detail.cshtml(.cs)` | Occurrence table for regular workshops; Book button for reservable; icon-ified host links. |
| `Pages/Index.cshtml(.cs)` | Newsletter section moved; upcoming-workshops query fixed to a 2-month window + reservable workshops appended; calendar query joins occurrences. |
| `Pages/Shared/_Layout.cshtml` | Footer social/email links → icon-only. |
| `Pages/About.cshtml` | Contact row social/email links → icon-only. |

---

## Task 1: Data model — `Workshop` and `WorkshopOccurrence`

**Files:**
- Modify: `Models/Workshop.cs`
- Create: `Models/WorkshopOccurrence.cs`
- Delete: `Models/PinnedWorkshop.cs`

- [ ] **Step 1: Rewrite `Models/Workshop.cs`**

```csharp
namespace WorkshopZagreb.Models;

// This is the data model — EF Core turns this class into a database table.
// Workshop is the shared template: name, description, images, host, price.
// Specific dates live on WorkshopOccurrence (see that file).
public class Workshop
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? BannerUrl { get; set; }       // main card/hero image (required via form)
    public string? LogoUrl { get; set; }         // small square profile icon (optional)
    public required string InstagramPostUrl { get; set; }

    public string? HostName { get; set; }
    public string? HostInstagram { get; set; }
    public string? HostWebsite { get; set; }
    public decimal? Price { get; set; }
    public int? MaxParticipants { get; set; }

    // Slug is the URL-friendly version of the name: "Watercolour Basics" -> "watercolour-basics"
    // Used in the URL: /workshops/watercolour-basics
    public required string Slug { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Manually hidden by admin — stays in archive tab regardless of dates
    public bool IsArchived { get; set; }

    // Reservable workshops are booked as a group for no fixed date (e.g. a
    // birthday party) — shown with a single Book button instead of a date/time,
    // and have zero WorkshopOccurrence rows. BookingType is "email" or "webpage".
    public bool IsReservable { get; set; }
    public string? BookingType { get; set; }
    public string? BookingValue { get; set; }

    // A workshop can have multiple photos and multiple dates (occurrences)
    public List<WorkshopPhoto> Photos { get; set; } = new();
    public List<WorkshopOccurrence> Occurrences { get; set; } = new();
}
```

- [ ] **Step 2: Create `Models/WorkshopOccurrence.cs`**

```csharp
namespace WorkshopZagreb.Models;

// One specific date/time a workshop runs on. A regular workshop can have
// several of these (e.g. the same class repeated on different weeks).
// Reservable workshops have none — they're dateless by definition.
public class WorkshopOccurrence
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }

    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? EntrioUrl { get; set; }      // link to Entrio ticket page for this date

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsUpcoming => Date >= DateTime.Today;
}
```

- [ ] **Step 3: Delete `Models/PinnedWorkshop.cs`**

```bash
rm "Models/PinnedWorkshop.cs"
```

- [ ] **Step 4: Verify the project still builds (it won't yet — expected)**

Run: `dotnet build`
Expected: FAILS with errors in `AppDbContext.cs`, `Program.cs`, and every page referencing `Workshop.Date`/`.IsPinned`/etc. — this is expected; those get fixed in the following tasks. This step is just to confirm you're starting from a known error state, not a silent typo.

- [ ] **Step 5: Commit**

```bash
git add Models/Workshop.cs Models/WorkshopOccurrence.cs
git rm Models/PinnedWorkshop.cs
git commit -m "Split Workshop into template + WorkshopOccurrence, add reservable/booking fields"
```

---

## Task 2: `AppDbContext` — wire up the new entity, drop the dead one

**Files:**
- Modify: `Data/AppDbContext.cs`

- [ ] **Step 1: Replace the whole file**

```csharp
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Data;

// AppDbContext is the bridge between your C# models and the actual database.
// Think of it as the "database session" — you query through it and save through it.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Each DbSet = one database table
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<WorkshopOccurrence> WorkshopOccurrences => Set<WorkshopOccurrence>();
    public DbSet<WorkshopPhoto> WorkshopPhotos => Set<WorkshopPhoto>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<ReservedDay> ReservedDays => Set<ReservedDay>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Slug must be unique — no two workshops can have the same URL
        modelBuilder.Entity<Workshop>()
            .HasIndex(w => w.Slug)
            .IsUnique();

        // Email must be unique in the subscribers table
        modelBuilder.Entity<Subscriber>()
            .HasIndex(s => s.Email)
            .IsUnique();

        // One workshop -> many photos, delete photos when workshop is deleted
        modelBuilder.Entity<Workshop>()
            .HasMany(w => w.Photos)
            .WithOne(p => p.Workshop)
            .HasForeignKey(p => p.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        // One workshop -> many occurrences (dates), delete occurrences when workshop is deleted
        modelBuilder.Entity<Workshop>()
            .HasMany(w => w.Occurrences)
            .WithOne(o => o.Workshop)
            .HasForeignKey(o => o.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        // One menu category -> many menu items, delete items when category is deleted
        modelBuilder.Entity<MenuCategory>()
            .HasMany(c => c.Items)
            .WithOne(i => i.Category)
            .HasForeignKey(i => i.MenuCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed some sample workshops so the site isn't empty on first run
        modelBuilder.Entity<Workshop>().HasData(
            new Workshop
            {
                Id = 1,
                Name = "Akvarel za početnike",
                Description = "Naučite osnove akvarela u opuštenom okruženju uz kavu. Sve materijale osiguravamo mi!",
                BannerUrl = "/images/unutra.webp",
                LogoUrl = null,
                InstagramPostUrl = "https://www.instagram.com/workshop.zagreb/",
                HostName = "Ana Kovač",
                HostInstagram = "https://instagram.com/anakovac.art",
                Price = 35,
                MaxParticipants = 12,
                Slug = "akvarel-za-pocetnike",
                IsArchived = false,
                IsReservable = false,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new Workshop
            {
                Id = 2,
                Name = "Keramika za sve",
                Description = "Uvod u oblikovanje gline na lončarskom kolu. Iskustvo nije potrebno — samo volontiranje za pranje ruku.",
                BannerUrl = "/images/table.webp",
                LogoUrl = null,
                InstagramPostUrl = "https://www.instagram.com/workshop.zagreb/",
                HostName = "Marko Blažević",
                Price = 45,
                MaxParticipants = 8,
                Slug = "keramika-za-sve",
                IsArchived = false,
                IsReservable = false,
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new Workshop
            {
                Id = 3,
                Name = "Makramé osnove",
                Description = "Naučite plesti makramé uzlove i izradite vlastiti zidni ukras.",
                BannerUrl = "/images/prostor.jpg",
                LogoUrl = null,
                InstagramPostUrl = "https://www.instagram.com/workshop.zagreb/",
                Price = 30,
                MaxParticipants = 10,
                Slug = "makrame-osnove",
                IsArchived = false,
                IsReservable = false,
                CreatedAt = new DateTime(2026, 1, 1)
            }
        );

        // Matching seed dates for the three sample workshops above
        modelBuilder.Entity<WorkshopOccurrence>().HasData(
            new WorkshopOccurrence
            {
                Id = 1,
                WorkshopId = 1,
                Date = new DateTime(2026, 1, 8),
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                EntrioUrl = "https://entrio.hr",
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new WorkshopOccurrence
            {
                Id = 2,
                WorkshopId = 2,
                Date = new DateTime(2026, 1, 15),
                StartTime = new TimeSpan(11, 0, 0),
                EndTime = new TimeSpan(14, 0, 0),
                CreatedAt = new DateTime(2026, 1, 1)
            },
            new WorkshopOccurrence
            {
                Id = 3,
                WorkshopId = 3,
                Date = new DateTime(2026, 1, 22),
                StartTime = new TimeSpan(16, 0, 0),
                CreatedAt = new DateTime(2026, 1, 1)
            }
        );
    }
}
```

Note: seed dates changed from `DateTime.Today.AddDays(N)` (a moving target) to fixed dates. This only affects a brand-new empty database (`EnsureCreated` only seeds once, on first creation) — it does not touch your existing `workshop.db`. Since these are just placeholder sample rows for a fresh dev database, a fixed date is fine (and safer than a "seed value changes every day" pattern EF normally warns about).

- [ ] **Step 2: Commit**

```bash
git add Data/AppDbContext.cs
git commit -m "Wire WorkshopOccurrence into AppDbContext, drop PinnedWorkshop"
```

---

## Task 3: `Program.cs` — schema, backfill, and the reseed-on-deploy bug fix

**Files:**
- Modify: `Program.cs`

- [ ] **Step 1: Replace the seeding block (everything between `db.Database.EnsureCreated();` and `app.Run();`)**

Find this block (currently lines 71–173) and replace it entirely with:

```csharp
    db.Database.EnsureCreated();

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ReservedDays (
            Id    INTEGER PRIMARY KEY AUTOINCREMENT,
            Date  TEXT    NOT NULL,
            Label TEXT
        )");

    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already present — safe to ignore */ }

    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already present — safe to ignore */ }

    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN IsReservable INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already present — safe to ignore */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN BookingType TEXT"); }
    catch { /* column already present — safe to ignore */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Workshops ADD COLUMN BookingValue TEXT"); }
    catch { /* column already present — safe to ignore */ }

    db.Database.ExecuteSqlRaw("UPDATE Workshops SET IsReservable = IsPinned");

    db.Database.ExecuteSqlRaw(@"
        UPDATE Workshops
        SET BookingType = 'webpage', BookingValue = '/suradnja#upit'
        WHERE IsReservable = 1 AND BookingType IS NULL");

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

    var occurrencesBackfillInserted = db.Database.ExecuteSqlRaw(
        "INSERT OR IGNORE INTO SeedFlags (Key, Value) VALUES ('OccurrencesBackfilled', 1)");
    if (occurrencesBackfillInserted > 0)
    {
        db.Database.ExecuteSqlRaw(@"
            INSERT INTO WorkshopOccurrences (WorkshopId, Date, StartTime, EndTime, EntrioUrl, CreatedAt)
            SELECT Id, Date, StartTime, EndTime, EntrioUrl, CreatedAt
            FROM Workshops
            WHERE IsPinned = 0");
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
```

Delete the entire old `PinnedWorkshops` table creation + seed block (the one referencing `db.PinnedWorkshops` and `new PinnedWorkshop { ... }`) — it no longer compiles since `PinnedWorkshop` was deleted in Task 1, and the feature itself is dead code being removed.

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: still FAILS (other files not yet updated) — but the error list should no longer mention `Program.cs`. Confirm by checking the error output only references `Pages/...` and `Helpers/GoogleCalendarHelper.cs` and `Services/EmailService.cs`.

- [ ] **Step 3: Commit**

```bash
git add Program.cs
git commit -m "Fix pinned-workshop reseed-on-deploy bug; add occurrence schema + backfill"
```

---

## Task 4: `GoogleCalendarHelper` and `EmailService` — signature updates + email silent-failure bug fix

**Files:**
- Modify: `Helpers/GoogleCalendarHelper.cs`
- Modify: `Services/EmailService.cs`

- [ ] **Step 1: Replace `Helpers/GoogleCalendarHelper.cs`**

```csharp
using System.Net;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Helpers;

public static class GoogleCalendarHelper
{
    public static string BuildAddToCalendarUrl(Workshop w, WorkshopOccurrence occ)
    {
        var start = occ.Date.Date + occ.StartTime;
        var end = occ.Date.Date + (occ.EndTime ?? occ.StartTime.Add(TimeSpan.FromHours(2)));

        string Fmt(DateTime dt) => dt.ToString("yyyyMMddTHHmmss");

        var query = $"action=TEMPLATE" +
                    $"&text={WebUtility.UrlEncode(w.Name)}" +
                    $"&dates={Fmt(start)}/{Fmt(end)}" +
                    $"&details={WebUtility.UrlEncode(w.Description)}" +
                    $"&location={WebUtility.UrlEncode("Workshop Zagreb")}" +
                    $"&ctz=Europe/Zagreb";

        return $"https://www.google.com/calendar/render?{query}";
    }
}
```

- [ ] **Step 2: Update `Services/EmailService.cs` — interface + announcement method + silent-failure fix**

Change the interface method signature:

```csharp
public interface IEmailService
{
    Task SendConfirmationAsync(string toEmail, string unsubscribeToken);
    Task SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence occurrence, IList<Subscriber> subscribers, string? subject = null);
    Task SendInquiryAsync(InquiryInput input);
}
```

Replace `SendWorkshopAnnouncementAsync`'s body (it currently reads `workshop.Date`/`.StartTime`/`.EndTime`/`.EntrioUrl` — those move to the `occurrence` parameter):

```csharp
    public async Task SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence occurrence, IList<Subscriber> subscribers, string? subject = null)
    {
        if (!subscribers.Any()) return;

        var date    = occurrence.Date.ToString("dd. MM. yyyy");
        var time    = occurrence.StartTime.ToString(@"hh\:mm");
        var endTime = occurrence.EndTime.HasValue ? $" – {occurrence.EndTime.Value:hh\\:mm}" : "";
        var price   = workshop.Price.HasValue ? $"{workshop.Price:0} €" : "Besplatno";
        var maxPax  = workshop.MaxParticipants.HasValue
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;width:100px;'>Mjesta</td><td style='font-weight:500;'>max {workshop.MaxParticipants}</td></tr>"
            : "";
        var hostRow = !string.IsNullOrEmpty(workshop.HostName)
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;'>Voditelj</td><td style='font-weight:500;'>{workshop.HostName}</td></tr>"
            : "";
        var ticketBtn = !string.IsNullOrEmpty(occurrence.EntrioUrl)
            ? $"""<p style="margin:28px 0 8px;"><a href="{occurrence.EntrioUrl}" style="background:#c8a96e;color:#fff;padding:12px 32px;text-decoration:none;display:inline-block;font-size:0.9rem;font-weight:600;">Kupi ulaznicu</a></p>"""
            : "";
        var calendarUrl = $"{SiteBase()}/#calendar";
        subject ??= $"Nova radionica: {workshop.Name} — Workshop Zagreb";

        foreach (var sub in subscribers)
        {
            var unsub = UnsubscribeUrl(sub.Token);
            var html = $"""
                <div style="font-family:Inter,Arial,sans-serif;max-width:540px;margin:0 auto;color:#1a1a1a;padding:32px 0;">
                  <p style="font-size:0.72rem;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;color:#c8a96e;margin-bottom:8px;">Nova radionica</p>
                  <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:1.9rem;line-height:1.2;margin:0 0 24px;">{workshop.Name}</h1>

                  <table style="width:100%;border-collapse:collapse;margin-bottom:28px;">
                    <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;width:100px;">Datum</td><td style="font-weight:500;">{date}</td></tr>
                    <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;">Vrijeme</td><td style="font-weight:500;">{time}{endTime}</td></tr>
                    <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;">Cijena</td><td style="font-weight:500;">{price}</td></tr>
                    {maxPax}
                    {hostRow}
                  </table>

                  <p style="line-height:1.75;margin-bottom:28px;">{workshop.Description}</p>

                  {ticketBtn}
                  <p style="margin-top:16px;">
                    <a href="{calendarUrl}" style="color:#c8a96e;font-size:0.9rem;">Pogledaj kalendar radionica →</a>
                  </p>

                  <hr style="border:none;border-top:1px solid #e5e0d8;margin:40px 0;" />
                  <p style="font-size:0.72rem;color:#999;line-height:1.6;">
                    Workshop Zagreb, Zagreb<br/>
                    <a href="{unsub}" style="color:#999;">Odjavi se s newslettera</a>
                  </p>
                </div>
                """;

            await SendOneAsync(sub.Email, subject, html);
        }
    }
```

Fix the silent-failure bug in `SendOneAsync` — it currently only checks `Host`/`From`, never `Password`, so a blank password authenticates and fails silently:

```csharp
    private async Task SendOneAsync(string toEmail, string subject, string html, string? replyTo = null)
    {
        var smtp = _config.GetSection("Email:Smtp");
        var host = smtp["Host"];
        var from = smtp["From"];
        var password = smtp["Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(password))
        {
            _log.LogWarning("Email:Smtp not fully configured (missing Host/From/Password) — skipping send to {To}", toEmail);
            return;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(from));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            if (replyTo != null) msg.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            msg.Subject = subject;
            msg.Body = new TextPart("html") { Text = html };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtp["Username"], password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send email to {To}", toEmail);
        }
    }
```

This doesn't fix the actual missing App Password (that's a config/ops task the site owner does in Google Workspace, covered separately) — it makes the failure mode an honest, loud "not configured" warning instead of a silent auth failure.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: errors now only in `Pages/Admin/Workshops/Edit.cshtml.cs`, `Pages/Admin/Index.cshtml.cs`, `Pages/Admin/Pinned/*`, `Pages/Workshops/*`, `Pages/Index.cshtml.cs`.

- [ ] **Step 4: Commit**

```bash
git add Helpers/GoogleCalendarHelper.cs Services/EmailService.cs
git commit -m "Fix email silent-failure bug; adapt calendar/announcement helpers to occurrences"
```

---

## Task 5: Admin — delete the dead Pinned pages

**Files:**
- Delete: `Pages/Admin/Pinned/Edit.cshtml`
- Delete: `Pages/Admin/Pinned/Edit.cshtml.cs`

- [ ] **Step 1: Delete the directory**

```bash
rm -rf "Pages/Admin/Pinned"
```

- [ ] **Step 2: Commit**

```bash
git add -A Pages/Admin/Pinned
git commit -m "Remove dead PinnedWorkshop admin CRUD pages"
```

---

## Task 6: Admin — Workshop edit page (occurrences + reservable Book fields)

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs`
- Modify: `Pages/Admin/Workshops/Edit.cshtml`

- [ ] **Step 1: Replace `Pages/Admin/Workshops/Edit.cshtml.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using WorkshopZagreb.Services;

namespace WorkshopZagreb.Pages.Admin.Workshops;

public class WorkshopEditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileService _files;
    private readonly IEmailService _email;

    public WorkshopEditModel(AppDbContext db, IFileService files, IEmailService email)
    {
        _db = db;
        _files = files;
        _email = email;
    }

    [BindProperty]
    public WorkshopInputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerFile { get; set; }

    [BindProperty]
    public IFormFile? LogoFile { get; set; }

    [BindProperty]
    public List<IFormFile> PhotoFiles { get; set; } = new();

    [BindProperty]
    public OccurrenceInputModel NewOccurrence { get; set; } = new();

    public List<WorkshopPhoto> ExistingPhotos { get; set; } = new();
    public List<WorkshopOccurrence> Occurrences { get; set; } = new();
    public bool IsNew { get; set; }
    public bool IsArchivedWorkshop { get; set; }
    public string? StatusMessage { get; set; }

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        IsNew = action == "new";

        if (!IsNew && id.HasValue)
        {
            var workshop = await _db.Workshops
                .Include(w => w.Photos)
                .Include(w => w.Occurrences)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workshop == null) return NotFound();

            Input = new WorkshopInputModel
            {
                Id = workshop.Id,
                Name = workshop.Name,
                IsReservable = workshop.IsReservable,
                BookingType = workshop.BookingType ?? "webpage",
                BookingValue = workshop.BookingValue ?? "",
                Description = workshop.Description,
                Price = workshop.Price,
                MaxParticipants = workshop.MaxParticipants,
                InstagramPostUrl = workshop.InstagramPostUrl,
                HostName = workshop.HostName,
                HostInstagram = workshop.HostInstagram,
                HostWebsite = workshop.HostWebsite,
                ExistingLogoUrl   = workshop.LogoUrl,
                ExistingBannerUrl = workshop.BannerUrl,
            };
            ExistingPhotos = workshop.Photos.OrderBy(p => p.Order).ToList();
            Occurrences = workshop.Occurrences.OrderBy(o => o.Date).ToList();
            IsArchivedWorkshop = workshop.IsArchived;
        }
        else
        {
            NewOccurrence.Date = DateTime.Today.AddDays(7); // sensible default
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        IsNew = action == "new";

        if (!IsNew && Input.Id > 0)
        {
            ExistingPhotos = await _db.WorkshopPhotos.Where(p => p.WorkshopId == Input.Id).ToListAsync();
            Occurrences = await _db.WorkshopOccurrences.Where(o => o.WorkshopId == Input.Id).OrderBy(o => o.Date).ToListAsync();
        }

        if (!ModelState.IsValid)
            return Page();

        if (IsNew)
        {
            if (BannerFile == null)
            {
                ModelState.AddModelError("BannerFile", "Banner image is required.");
                return Page();
            }
            if (!Input.IsReservable && NewOccurrence.Date == default)
            {
                ModelState.AddModelError("NewOccurrence.Date", "Date is required for a non-reservable workshop.");
                return Page();
            }

            var bannerUrl = await _files.SaveImageAsync(BannerFile, "workshops");
            var logoUrl   = LogoFile != null
                ? await _files.SaveImageAsync(LogoFile, "workshops")
                : null;

            var workshop = new Workshop
            {
                Name = Input.Name,
                IsReservable = Input.IsReservable,
                BookingType = Input.IsReservable ? Input.BookingType : null,
                BookingValue = Input.IsReservable ? Input.BookingValue : null,
                Description = Input.Description,
                Price = Input.Price,
                MaxParticipants = Input.MaxParticipants,
                InstagramPostUrl = Input.InstagramPostUrl ?? "",
                HostName = Input.HostName,
                HostInstagram = Input.HostInstagram,
                HostWebsite = Input.HostWebsite,
                BannerUrl = bannerUrl,
                LogoUrl   = logoUrl,
                Slug = await GenerateUniqueSlugAsync(Input.Name),
            };

            _db.Workshops.Add(workshop);
            await _db.SaveChangesAsync();
            await SavePhotosAsync(workshop.Id);

            WorkshopOccurrence? firstOccurrence = null;
            if (!Input.IsReservable)
            {
                firstOccurrence = new WorkshopOccurrence
                {
                    WorkshopId = workshop.Id,
                    Date = NewOccurrence.Date,
                    StartTime = NewOccurrence.StartTime,
                    EndTime = NewOccurrence.EndTime,
                    EntrioUrl = NewOccurrence.EntrioUrl,
                };
                _db.WorkshopOccurrences.Add(firstOccurrence);
                await _db.SaveChangesAsync();
            }

            if (Input.NotifySubscribers && firstOccurrence != null)
            {
                var subject = string.IsNullOrWhiteSpace(Input.EmailSubject)
                    ? $"Nova radionica! - {workshop.Name}"
                    : Input.EmailSubject;
                var newSubs = await ActiveSubscribersAsync();
                _ = _email.SendWorkshopAnnouncementAsync(workshop, firstOccurrence, newSubs, subject);
            }
        }
        else
        {
            var workshop = await _db.Workshops.FindAsync(Input.Id);
            if (workshop == null) return NotFound();

            workshop.Name = Input.Name;
            workshop.IsReservable = Input.IsReservable;
            workshop.BookingType = Input.IsReservable ? Input.BookingType : null;
            workshop.BookingValue = Input.IsReservable ? Input.BookingValue : null;
            workshop.Description = Input.Description;
            workshop.Price = Input.Price;
            workshop.MaxParticipants = Input.MaxParticipants;
            workshop.InstagramPostUrl = Input.InstagramPostUrl ?? "";
            workshop.HostName = Input.HostName;
            workshop.HostInstagram = Input.HostInstagram;
            workshop.HostWebsite = Input.HostWebsite;

            if (BannerFile != null)
            {
                if (workshop.BannerUrl != null) _files.DeleteImage(workshop.BannerUrl);
                workshop.BannerUrl = await _files.SaveImageAsync(BannerFile, "workshops");
            }

            if (LogoFile != null)
            {
                if (workshop.LogoUrl != null) _files.DeleteImage(workshop.LogoUrl);
                workshop.LogoUrl = await _files.SaveImageAsync(LogoFile, "workshops");
            }

            await _db.SaveChangesAsync();
            await SavePhotosAsync(workshop.Id);
        }

        return RedirectToPage("/Admin/Index");
    }

    // Adds one more date to an existing (non-reservable) workshop — this is the
    // "New date" feature: reuse the workshop's content, just pick a new date.
    public async Task<IActionResult> OnPostAddOccurrenceAsync(int workshopId)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        _db.WorkshopOccurrences.Add(new WorkshopOccurrence
        {
            WorkshopId = workshopId,
            Date = NewOccurrence.Date,
            StartTime = NewOccurrence.StartTime,
            EndTime = NewOccurrence.EndTime,
            EntrioUrl = NewOccurrence.EntrioUrl,
        });
        await _db.SaveChangesAsync();

        return RedirectToPage(new { action = "edit", id = workshopId });
    }

    public async Task<IActionResult> OnPostUpdateOccurrenceAsync(int occurrenceId, int workshopId, DateTime occDate, TimeSpan occStartTime, TimeSpan? occEndTime, string? occEntrioUrl)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var occurrence = await _db.WorkshopOccurrences.FindAsync(occurrenceId);
        if (occurrence != null)
        {
            occurrence.Date = occDate;
            occurrence.StartTime = occStartTime;
            occurrence.EndTime = occEndTime;
            occurrence.EntrioUrl = occEntrioUrl;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { action = "edit", id = workshopId });
    }

    public async Task<IActionResult> OnPostDeleteOccurrenceAsync(int occurrenceId, int workshopId)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var occurrence = await _db.WorkshopOccurrences.FindAsync(occurrenceId);
        if (occurrence != null)
        {
            _db.WorkshopOccurrences.Remove(occurrence);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { action = "edit", id = workshopId });
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = true; await _db.SaveChangesAsync(); }
        return RedirectToPage("/Admin/Index");
    }

    public async Task<IActionResult> OnPostUnarchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = false; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { action = "edit", id });
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int photoId, string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var photo = await _db.WorkshopPhotos.FindAsync(photoId);
        if (photo != null)
        {
            _files.DeleteImage(photo.Url);
            _db.WorkshopPhotos.Remove(photo);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { action = "edit", id = id });
    }

    private async Task SavePhotosAsync(int workshopId)
    {
        int order = await _db.WorkshopPhotos
            .Where(p => p.WorkshopId == workshopId)
            .CountAsync();

        foreach (var file in PhotoFiles)
        {
            if (file.Length > 0)
            {
                var url = await _files.SaveImageAsync(file, "workshops");
                _db.WorkshopPhotos.Add(new WorkshopPhoto
                {
                    WorkshopId = workshopId,
                    Url = url,
                    Order = order++
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    private Task<List<Subscriber>> ActiveSubscribersAsync() =>
        _db.Subscribers
            .Where(s => s.ConfirmedAt != null && s.UnsubscribedAt == null)
            .ToListAsync();

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var base_slug = SlugHelper.Generate(name);
        var slug = base_slug;
        int i = 2;
        while (await _db.Workshops.AnyAsync(w => w.Slug == slug))
        {
            slug = $"{base_slug}-{i++}";
        }
        return slug;
    }
}

public class WorkshopInputModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsReservable { get; set; }
    public string BookingType { get; set; } = "webpage";
    public string BookingValue { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal? Price { get; set; }
    public int? MaxParticipants { get; set; }
    public string InstagramPostUrl { get; set; } = "";
    public string? HostName { get; set; }
    public string? HostInstagram { get; set; }
    public string? HostWebsite { get; set; }
    public string? ExistingLogoUrl   { get; set; }
    public string? ExistingBannerUrl { get; set; }
    public bool NotifySubscribers { get; set; }
    public string EmailSubject { get; set; } = "";
}

public class OccurrenceInputModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new TimeSpan(14, 0, 0);
    public TimeSpan? EndTime { get; set; }
    public string? EntrioUrl { get; set; }
}
```

Note: `IsArchivedWorkshop` now reflects the manual `IsArchived` flag directly (archiving is whole-workshop, per the design spec — not date-based anymore), and there's a new `OnPostUnarchiveAsync` handler for symmetry (the old code revived a workshop implicitly by editing its date past today; that mechanism doesn't exist anymore since date is no longer on `Workshop`).

- [ ] **Step 2: Replace `Pages/Admin/Workshops/Edit.cshtml`**

```html
@page "/admin/workshops/{action}/{id:int?}"
@model WorkshopZagreb.Pages.Admin.Workshops.WorkshopEditModel
@{
    Layout = "_AdminLayout";
    bool isNew = Model.IsNew;
    ViewData["Title"] = isNew ? "Nova radionica" : "Uredi radionicu";
}

<div style="margin-bottom:24px;">
    <a href="/admin" style="font-size:0.85rem; color:var(--terracotta);">← Natrag na popis</a>
</div>

<h1 class="admin-page-title">@(isNew ? "Nova radionica" : "Uredi: " + Model.Input.Name)</h1>

@if (Model.IsArchivedWorkshop)
{
    <div class="login-error" style="background:#FEF9EC; color:#92610A; border-left:4px solid #F6C84B; margin-bottom:24px;">
        Ova radionica je arhivirana.
        <form method="post" asp-page-handler="Unarchive" asp-route-id="@Model.Input.Id" style="display:inline;">
            <button type="submit" class="btn btn-outline btn-sm" style="margin-left:8px;">Vrati iz arhive</button>
        </form>
    </div>
}

<form method="post" enctype="multipart/form-data" class="form-card">
    <input type="hidden" name="Input.Id" value="@Model.Input.Id" />

    <p class="form-section-title">Osnovni podaci</p>

    <div class="form-group">
        <label class="form-label">Naziv radionice <span class="required">*</span></label>
        <input type="text" name="Input.Name" value="@Model.Input.Name"
               class="form-control" required placeholder="npr. Akvarel za početnike" />
    </div>

    <div class="form-group">
        <label style="display:flex;align-items:center;gap:10px;cursor:pointer;">
            <input type="hidden" name="Input.IsReservable" value="false" />
            <input type="checkbox" id="chk-reservable" name="Input.IsReservable" value="true"
                   @(Model.Input.IsReservable ? "checked" : "") style="width:16px;height:16px;"
                   onchange="toggleReservable(this.checked)" />
            <span class="form-label" style="margin:0;">Reservable (rezervira se za grupu, bez specifičnog datuma)</span>
        </label>
    </div>

    @if (isNew)
    {
        <div id="date-row" style="display:@(Model.Input.IsReservable ? "none" : "")">
            <div class="form-row">
                <div class="form-group">
                    <label class="form-label">Datum <span class="required">*</span></label>
                    <input type="date" id="input-date" name="NewOccurrence.Date" value="@Model.NewOccurrence.Date.ToString("yyyy-MM-dd")"
                           class="form-control" @(Model.Input.IsReservable ? "" : "required") />
                </div>
                <div class="form-group">
                    <label class="form-label">Početak <span class="required">*</span></label>
                    <input type="time" id="input-starttime" name="NewOccurrence.StartTime" value="@Model.NewOccurrence.StartTime.ToString(@"hh\:mm")"
                           class="form-control" @(Model.Input.IsReservable ? "" : "required") />
                </div>
            </div>
            <div class="form-group">
                <label class="form-label">Kraj <span style="opacity:0.5;font-weight:400;">(opcionalno)</span></label>
                <input type="time" name="NewOccurrence.EndTime"
                       value="@(Model.NewOccurrence.EndTime.HasValue ? Model.NewOccurrence.EndTime.Value.ToString(@"hh\:mm") : "")"
                       class="form-control" />
            </div>
            <div class="form-group">
                <label class="form-label">Entrio URL (ulaznice) <span style="opacity:0.5;font-weight:400;">(opcionalno)</span></label>
                <input type="url" name="NewOccurrence.EntrioUrl" value="@Model.NewOccurrence.EntrioUrl"
                       class="form-control" placeholder="https://entrio.hr/event/..." />
            </div>
        </div>
    }

    <div id="reservable-row" style="display:@(Model.Input.IsReservable ? "" : "none")">
        <div class="form-group">
            <label class="form-label">Način rezervacije</label>
            <select name="Input.BookingType" class="form-control" onchange="toggleBookingValue(this.value)">
                <option value="webpage" selected="@(Model.Input.BookingType == "webpage")">Web stranica</option>
                <option value="email" selected="@(Model.Input.BookingType == "email")">Email</option>
            </select>
        </div>
        <div class="form-group">
            <label class="form-label" id="booking-value-label">
                @(Model.Input.BookingType == "email" ? "Email adresa" : "URL stranice")
            </label>
            <input type="text" name="Input.BookingValue" value="@Model.Input.BookingValue"
                   class="form-control" placeholder="/suradnja#upit ili npr@workshopzagreb.com" />
        </div>
    </div>

    <script>
    function toggleReservable(isReservable) {
        var dateRow = document.getElementById('date-row');
        if (dateRow) dateRow.style.display = isReservable ? 'none' : '';
        document.getElementById('reservable-row').style.display = isReservable ? '' : 'none';
        var dateInput = document.getElementById('input-date');
        var timeInput = document.getElementById('input-starttime');
        if (dateInput) dateInput.required = !isReservable;
        if (timeInput) timeInput.required = !isReservable;
    }
    function toggleBookingValue(type) {
        document.getElementById('booking-value-label').textContent = type === 'email' ? 'Email adresa' : 'URL stranice';
    }
    </script>

    <div class="form-group">
        <label class="form-label">Cijena (€) <span style="opacity:0.5;font-weight:400;">(opcionalno)</span></label>
        <input type="number" name="Input.Price" value="@Model.Input.Price"
               class="form-control" min="0" step="1" placeholder="npr. 25" />
    </div>

    <div class="form-group">
        <label class="form-label">Opis <span class="required">*</span></label>
        <textarea name="Input.Description" class="form-control" required
                  placeholder="Opišite radionicu — što će polaznici naučiti, što je uključeno...">@Model.Input.Description</textarea>
    </div>

    <div class="form-group">
        <label class="form-label">Max polaznika</label>
        <input type="number" name="Input.MaxParticipants" value="@Model.Input.MaxParticipants"
               class="form-control" min="1" placeholder="npr. 12" />
    </div>

    <p class="form-section-title">Instagram</p>

    <div class="form-group">
        <label class="form-label">Link na Instagram objavu <span style="font-weight:400; font-size:0.9rem; opacity:0.6;">(opcionalno)</span></label>
        <input type="url" name="Input.InstagramPostUrl" value="@Model.Input.InstagramPostUrl"
               class="form-control" placeholder="https://www.instagram.com/p/..." />
    </div>

    <p class="form-section-title">Voditelj radionice <span style="font-weight:400; font-size:0.9rem; opacity:0.6;">(opcionalno)</span></p>

    <div class="form-row">
        <div class="form-group">
            <label class="form-label">Ime voditelja</label>
            <input type="text" name="Input.HostName" value="@Model.Input.HostName" class="form-control" placeholder="Ana Kovač" />
        </div>
        <div class="form-group">
            <label class="form-label">Instagram voditelja</label>
            <input type="url" name="Input.HostInstagram" value="@Model.Input.HostInstagram" class="form-control" placeholder="https://instagram.com/..." />
        </div>
    </div>

    <div class="form-group">
        <label class="form-label">Web stranica voditelja</label>
        <input type="url" name="Input.HostWebsite" value="@Model.Input.HostWebsite" class="form-control" placeholder="https://..." />
    </div>

    <p class="form-section-title">Images</p>

    <div class="form-group">
        <label class="form-label">Banner <span class="required">*</span></label>
        @if (!string.IsNullOrEmpty(Model.Input.ExistingBannerUrl))
        {
            <div style="margin-bottom:12px;">
                <img src="@Model.Input.ExistingBannerUrl" alt="current banner" style="width:100%; max-height:180px; object-fit:cover;" />
                <p class="form-hint">Current banner. Upload a new image to replace it.</p>
            </div>
        }
        <input type="file" name="BannerFile" class="form-control" accept="image/png,image/jpeg,image/webp"
               @(string.IsNullOrEmpty(Model.Input.ExistingBannerUrl) ? "required" : "") />
    </div>

    <div class="form-group">
        <label class="form-label">Logo <span style="font-weight:400; font-size:0.9rem; opacity:0.6;">(optional)</span></label>
        @if (!string.IsNullOrEmpty(Model.Input.ExistingLogoUrl))
        {
            <div style="margin-bottom:12px;">
                <img src="@Model.Input.ExistingLogoUrl" alt="current logo" style="width:64px; height:64px; object-fit:cover; background:var(--cream);" />
            </div>
        }
        <input type="file" name="LogoFile" class="form-control" accept="image/png,image/jpeg,image/webp" />
    </div>

    <div class="form-group">
        <label class="form-label">Additional photos <span style="font-weight:400; font-size:0.9rem; opacity:0.6;">(optional)</span></label>
        @if (Model.ExistingPhotos.Any())
        {
            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-bottom:16px;">
                @foreach (var photo in Model.ExistingPhotos)
                {
                    <div style="position:relative;">
                        <img src="@photo.Url" alt="" style="height:80px; width:80px; object-fit:cover;" />
                        <form method="post" asp-page-handler="DeletePhoto" asp-route-photoId="@photo.Id"
                              style="position:absolute; top:-6px; right:-6px;" onsubmit="return confirm('Delete this photo?')">
                            <button type="submit" style="background:#DC2626; color:white; border:none; border-radius:50%; width:20px; height:20px; font-size:11px; cursor:pointer; line-height:1;">✕</button>
                        </form>
                    </div>
                }
            </div>
        }
        <input type="file" name="PhotoFiles" class="form-control" accept="image/png,image/jpeg,image/webp" multiple />
    </div>

    @if (isNew)
    {
        <p class="form-section-title">Obavijesti pretplatnike</p>
        <div class="form-group">
            <label style="display:flex;align-items:center;gap:10px;cursor:pointer;">
                <input type="hidden" name="Input.NotifySubscribers" value="false" />
                <input type="checkbox" id="chk-notify" name="Input.NotifySubscribers" value="true"
                       style="width:16px;height:16px;" onchange="document.getElementById('notify-fields').style.display = this.checked ? '' : 'none'" />
                <span class="form-label" style="margin:0;">Pošalji email svim pretplatnicima</span>
            </label>
        </div>
        <div id="notify-fields" style="display:none">
            <div class="form-group">
                <label class="form-label">Naslov emaila</label>
                <input type="text" id="email-subject" name="Input.EmailSubject" class="form-control" placeholder="npr. Nova radionica! - Akvarel za početnike" />
            </div>
        </div>
        <script>
        document.addEventListener('DOMContentLoaded', function () {
            var nameInput = document.querySelector('input[name="Input.Name"]');
            var subjectInput = document.getElementById('email-subject');
            if (nameInput && subjectInput) {
                nameInput.addEventListener('input', function () { subjectInput.value = 'Nova radionica! - ' + this.value; });
            }
        });
        </script>
    }

    <div class="form-actions">
        <button type="submit" class="btn btn-primary">@(isNew ? "Objavi radionicu" : "Spremi promjene")</button>
        <a href="/admin" class="btn btn-outline">Odustani</a>
    </div>
</form>

@if (!isNew && !Model.Input.IsReservable)
{
    <div class="form-card" style="margin-top:24px;">
        <p class="form-section-title">Termini</p>

        @if (Model.Occurrences.Any())
        {
            @foreach (var occ in Model.Occurrences)
            {
                <form method="post" asp-page-handler="UpdateOccurrence" style="display:flex;gap:12px;align-items:flex-end;flex-wrap:wrap;margin-bottom:16px;padding-bottom:16px;border-bottom:1px solid var(--border);">
                    <input type="hidden" name="occurrenceId" value="@occ.Id" />
                    <input type="hidden" name="workshopId" value="@Model.Input.Id" />
                    <div class="form-group" style="margin:0;">
                        <label class="form-label">Datum</label>
                        <input type="date" name="occDate" value="@occ.Date.ToString("yyyy-MM-dd")" class="form-control" required />
                    </div>
                    <div class="form-group" style="margin:0;">
                        <label class="form-label">Početak</label>
                        <input type="time" name="occStartTime" value="@occ.StartTime.ToString(@"hh\:mm")" class="form-control" required />
                    </div>
                    <div class="form-group" style="margin:0;">
                        <label class="form-label">Kraj</label>
                        <input type="time" name="occEndTime" value="@(occ.EndTime.HasValue ? occ.EndTime.Value.ToString(@"hh\:mm") : "")" class="form-control" />
                    </div>
                    <div class="form-group" style="margin:0;flex:1;min-width:180px;">
                        <label class="form-label">Entrio URL</label>
                        <input type="url" name="occEntrioUrl" value="@occ.EntrioUrl" class="form-control" placeholder="https://entrio.hr/event/..." />
                    </div>
                    <button type="submit" class="btn btn-outline btn-sm">Spremi</button>
                </form>
                <form method="post" asp-page-handler="DeleteOccurrence" style="margin:-12px 0 16px;"
                      onsubmit="return confirm('Obriši ovaj termin?')">
                    <input type="hidden" name="occurrenceId" value="@occ.Id" />
                    <input type="hidden" name="workshopId" value="@Model.Input.Id" />
                    <button type="submit" class="btn btn-danger btn-sm">Obriši ovaj termin</button>
                </form>
            }
        }
        else
        {
            <p class="form-hint">Nema termina. Dodaj prvi ispod.</p>
        }

        <p class="form-section-title">+ Novi datum</p>
        <form method="post" asp-page-handler="AddOccurrence" style="display:flex;gap:12px;align-items:flex-end;flex-wrap:wrap;">
            <input type="hidden" name="workshopId" value="@Model.Input.Id" />
            <div class="form-group" style="margin:0;">
                <label class="form-label">Datum</label>
                <input type="date" name="NewOccurrence.Date" class="form-control" required />
            </div>
            <div class="form-group" style="margin:0;">
                <label class="form-label">Početak</label>
                <input type="time" name="NewOccurrence.StartTime" value="14:00" class="form-control" required />
            </div>
            <div class="form-group" style="margin:0;">
                <label class="form-label">Kraj</label>
                <input type="time" name="NewOccurrence.EndTime" class="form-control" />
            </div>
            <div class="form-group" style="margin:0;flex:1;min-width:180px;">
                <label class="form-label">Entrio URL</label>
                <input type="url" name="NewOccurrence.EntrioUrl" class="form-control" placeholder="https://entrio.hr/event/..." />
            </div>
            <button type="submit" class="btn btn-primary btn-sm">+ Novi datum</button>
        </form>
    </div>
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: errors now only in `Pages/Admin/Index.cshtml.cs`, `Pages/Workshops/*`, `Pages/Index.cshtml.cs`.

- [ ] **Step 4: Manual verification**

Start the dev server (`preview_start` or `dotnet run`), log into `/admin`, create a new non-reservable workshop with one date, confirm it saves; open it again, add a second date via "+ Novi datum", confirm both dates now appear in the "Termini" list; edit one date's time and confirm it persists; delete one date and confirm it's removed. Then create a reservable workshop, confirm the date fields disappear and the Book method fields appear, save, and confirm it saved with `BookingType`/`BookingValue` (check via the Admin list in Task 7, or query the SQLite file directly: `sqlite3 workshop.db "SELECT Name, IsReservable, BookingType, BookingValue FROM Workshops"`).

- [ ] **Step 5: Commit**

```bash
git add "Pages/Admin/Workshops/Edit.cshtml.cs" "Pages/Admin/Workshops/Edit.cshtml"
git commit -m "Admin: inline occurrence editor (new-date feature) + reservable Book fields"
```

---

## Task 7: Admin — workshop list (Pinned → Reservable, next-occurrence display)

**Files:**
- Modify: `Pages/Admin/Index.cshtml.cs`
- Modify: `Pages/Admin/Index.cshtml`

- [ ] **Step 1: Replace `Pages/Admin/Index.cshtml.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Admin;

public class AdminIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public AdminIndexModel(AppDbContext db) => _db = db;

    public List<Workshop> UpcomingWorkshops { get; set; } = new();
    public List<Workshop> PastWorkshops     { get; set; } = new();
    public List<Workshop> ReservableWorkshops { get; set; } = new();

    // For the "next occurrence" line + "+N termina" count in the Upcoming tab
    public Dictionary<int, WorkshopOccurrence> NextOccurrenceByWorkshopId { get; set; } = new();
    public Dictionary<int, int> UpcomingOccurrenceCountByWorkshopId { get; set; } = new();

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var today = DateTime.Today;
        var all = await _db.Workshops.Include(w => w.Occurrences).ToListAsync();

        ReservableWorkshops = all.Where(w => !w.IsArchived && w.IsReservable).ToList();

        var nonReservable = all.Where(w => !w.IsReservable).ToList();
        UpcomingWorkshops = nonReservable
            .Where(w => !w.IsArchived && w.Occurrences.Any(o => o.Date >= today))
            .OrderBy(w => w.Occurrences.Where(o => o.Date >= today).Min(o => o.Date))
            .ToList();
        PastWorkshops = nonReservable
            .Where(w => w.IsArchived || !w.Occurrences.Any(o => o.Date >= today))
            .OrderByDescending(w => w.Occurrences.Any() ? w.Occurrences.Max(o => o.Date) : DateTime.MinValue)
            .ToList();

        foreach (var w in UpcomingWorkshops)
        {
            var upcoming = w.Occurrences.Where(o => o.Date >= today).OrderBy(o => o.Date).ToList();
            NextOccurrenceByWorkshopId[w.Id] = upcoming.First();
            UpcomingOccurrenceCountByWorkshopId[w.Id] = upcoming.Count;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id)
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = true; await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var workshop = await _db.Workshops.Include(w => w.Photos).Include(w => w.Occurrences).FirstOrDefaultAsync(w => w.Id == id);
        if (workshop != null) { _db.Workshops.Remove(workshop); await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }
}
```

- [ ] **Step 2: Replace `Pages/Admin/Index.cshtml`**

```html
@page
@model WorkshopZagreb.Pages.Admin.AdminIndexModel
@{
    Layout = "_AdminLayout";
    ViewData["Title"] = "Workshops";
}

<div class="admin-top-bar">
    <h1 class="admin-page-title">Workshops</h1>
    <a href="/admin/workshops/new" class="btn btn-primary">+ Add workshop</a>
</div>

<div class="admin-tabs">
    <button class="admin-tab-btn active" onclick="switchTab('upcoming', this)">
        Radionice
        @if (Model.UpcomingWorkshops.Any())
        {
            <span class="admin-tab-count">@Model.UpcomingWorkshops.Count</span>
        }
    </button>
    <button class="admin-tab-btn" onclick="switchTab('archive', this)">
        Arhiva
        @if (Model.PastWorkshops.Any())
        {
            <span class="admin-tab-count">@Model.PastWorkshops.Count</span>
        }
    </button>
    <button class="admin-tab-btn" onclick="switchTab('reservable', this)">
        Reservable
        @if (Model.ReservableWorkshops.Any())
        {
            <span class="admin-tab-count">@Model.ReservableWorkshops.Count</span>
        }
    </button>
</div>

<div id="tab-upcoming" class="admin-tab-panel">
    @if (!Model.UpcomingWorkshops.Any())
    {
        <div class="workshops-empty">
            <p>Nema nadolazećih radionica. <a href="/admin/workshops/new" style="color:var(--terracotta)">Dodaj prvu →</a></p>
        </div>
    }
    else
    {
        <div class="admin-list">
            @foreach (var w in Model.UpcomingWorkshops)
            {
                var next = Model.NextOccurrenceByWorkshopId[w.Id];
                var extraCount = Model.UpcomingOccurrenceCountByWorkshopId[w.Id] - 1;
                <div class="admin-list-item">
                    <div style="display:flex;align-items:center;gap:16px;flex:1;min-width:0;">
                        @if (!string.IsNullOrEmpty(w.BannerUrl))
                        {
                            <img src="@w.BannerUrl" alt="" style="width:56px;height:40px;object-fit:cover;flex-shrink:0;" />
                        }
                        else
                        {
                            <div style="width:56px;height:40px;background:var(--cream);flex-shrink:0;border:1px solid var(--border);"></div>
                        }
                        <div class="admin-list-item-info">
                            <h3>@w.Name</h3>
                            <p>
                                @next.Date.ToString("dd. MM. yyyy") u @next.StartTime.ToString(@"hh\:mm")
                                @(w.Price.HasValue ? $" · {w.Price:0} €" : " · Besplatno")
                                @(extraCount > 0 ? $" · +{extraCount} termina" : "")
                            </p>
                        </div>
                    </div>
                    <div class="admin-list-item-actions">
                        <a href="/admin/workshops/edit/@w.Id" class="btn btn-outline btn-sm">Edit</a>
                        <form method="post" asp-page-handler="Archive" asp-route-id="@w.Id"
                              onsubmit="return confirm('Archive @w.Name? It will move to the Arhiva tab.')">
                            <button type="submit" class="btn btn-outline btn-sm">Archive</button>
                        </form>
                        <form method="post" asp-page-handler="Delete" asp-route-id="@w.Id"
                              onsubmit="return confirm('Delete @w.Name?')">
                            <button type="submit" class="btn btn-danger btn-sm">Delete</button>
                        </form>
                    </div>
                </div>
            }
        </div>
    }
</div>

<div id="tab-archive" class="admin-tab-panel" style="display:none">
    @if (!Model.PastWorkshops.Any())
    {
        <div class="workshops-empty"><p>Nema arhiviranih radionica.</p></div>
    }
    else
    {
        <div class="admin-list">
            @foreach (var w in Model.PastWorkshops)
            {
                <div class="admin-list-item">
                    <div style="display:flex;align-items:center;gap:16px;flex:1;min-width:0;">
                        @if (!string.IsNullOrEmpty(w.BannerUrl))
                        {
                            <img src="@w.BannerUrl" alt="" style="width:56px;height:40px;object-fit:cover;flex-shrink:0;opacity:0.55;" />
                        }
                        else
                        {
                            <div style="width:56px;height:40px;background:var(--cream);flex-shrink:0;border:1px solid var(--border);"></div>
                        }
                        <div class="admin-list-item-info">
                            <h3>@w.Name</h3>
                            <p>@(w.Price.HasValue ? $"{w.Price:0} €" : "Besplatno")</p>
                        </div>
                    </div>
                    <div class="admin-list-item-actions">
                        <a href="/admin/workshops/edit/@w.Id" class="btn btn-primary btn-sm">Uredi</a>
                        <form method="post" asp-page-handler="Delete" asp-route-id="@w.Id"
                              onsubmit="return confirm('Delete @w.Name?')">
                            <button type="submit" class="btn btn-danger btn-sm">Delete</button>
                        </form>
                    </div>
                </div>
            }
        </div>
    }
</div>

<div id="tab-reservable" class="admin-tab-panel" style="display:none">
    @if (!Model.ReservableWorkshops.Any())
    {
        <div class="workshops-empty">
            <p>Nema reservable evenata. <a href="/admin/workshops/new" style="color:var(--terracotta)">Dodaj prvi →</a></p>
        </div>
    }
    else
    {
        <div class="admin-list">
            @foreach (var w in Model.ReservableWorkshops)
            {
                <div class="admin-list-item">
                    <div style="display:flex;align-items:center;gap:16px;flex:1;min-width:0;">
                        @if (!string.IsNullOrEmpty(w.BannerUrl))
                        {
                            <img src="@w.BannerUrl" alt="" style="width:56px;height:40px;object-fit:cover;flex-shrink:0;" />
                        }
                        else
                        {
                            <div style="width:56px;height:40px;background:var(--cream);flex-shrink:0;border:1px solid var(--border);"></div>
                        }
                        <div class="admin-list-item-info">
                            <h3>@w.Name</h3>
                            <p>@(w.Price.HasValue ? $"od {w.Price:0} €" : "Cijena po upitu") @(w.MaxParticipants.HasValue ? $" · max {w.MaxParticipants}" : "")</p>
                        </div>
                    </div>
                    <div class="admin-list-item-actions">
                        <a href="/admin/workshops/edit/@w.Id" class="btn btn-outline btn-sm">Edit</a>
                        <form method="post" asp-page-handler="Delete" asp-route-id="@w.Id"
                              onsubmit="return confirm('Obriši @w.Name?')">
                            <button type="submit" class="btn btn-danger btn-sm">Delete</button>
                        </form>
                    </div>
                </div>
            }
        </div>
    }
</div>

<script>
function switchTab(name, btn) {
    document.querySelectorAll('.admin-tab-panel').forEach(function(p) { p.style.display = 'none'; });
    document.querySelectorAll('.admin-tab-btn').forEach(function(b) { b.classList.remove('active'); });
    document.getElementById('tab-' + name).style.display = '';
    btn.classList.add('active');
}
</script>
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: errors now only in `Pages/Workshops/*` and `Pages/Index.cshtml.cs`.

- [ ] **Step 4: Manual verification**

Log into `/admin`, confirm the tab says "Reservable" (not "Pinned"), confirm the seeded "Rođendanska radionica" shows under that tab, confirm a multi-date workshop from Task 6 shows "+1 termina" in the Upcoming tab.

- [ ] **Step 5: Commit**

```bash
git add "Pages/Admin/Index.cshtml.cs" "Pages/Admin/Index.cshtml"
git commit -m "Admin: rename Pinned to Reservable, show next occurrence + count"
```

---

## Task 8: Public workshop listing — one card per workshop

**Files:**
- Modify: `Pages/Workshops/Index.cshtml.cs`
- Modify: `Pages/Workshops/Index.cshtml`

- [ ] **Step 1: Replace `Pages/Workshops/Index.cshtml.cs`**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Workshops;

public class WorkshopsIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public WorkshopsIndexModel(AppDbContext db) => _db = db;

    public List<Workshop> ReservableWorkshops { get; set; } = new();
    public List<Workshop> Workshops { get; set; } = new();
    public Dictionary<int, WorkshopOccurrence> NextOccurrenceByWorkshopId { get; set; } = new();
    public Dictionary<int, int> UpcomingOccurrenceCountByWorkshopId { get; set; } = new();

    public async Task OnGetAsync()
    {
        var today = DateTime.Today;

        ReservableWorkshops = await _db.Workshops
            .Where(w => w.IsReservable && !w.IsArchived)
            .OrderBy(w => w.Name)
            .ToListAsync();

        var candidates = await _db.Workshops
            .Include(w => w.Photos)
            .Include(w => w.Occurrences)
            .Where(w => !w.IsReservable && !w.IsArchived)
            .ToListAsync();

        Workshops = candidates
            .Where(w => w.Occurrences.Any(o => o.Date >= today))
            .OrderBy(w => w.Occurrences.Where(o => o.Date >= today).Min(o => o.Date))
            .ToList();

        foreach (var w in Workshops)
        {
            var upcoming = w.Occurrences.Where(o => o.Date >= today).OrderBy(o => o.Date).ToList();
            NextOccurrenceByWorkshopId[w.Id] = upcoming.First();
            UpcomingOccurrenceCountByWorkshopId[w.Id] = upcoming.Count;
        }
    }
}
```

- [ ] **Step 2: Replace `Pages/Workshops/Index.cshtml`**

```html
@page
@model WorkshopZagreb.Pages.Workshops.WorkshopsIndexModel
@{
    ViewData["Title"] = "Workshops";
}

<div class="workshops-page">
    <div class="workshops-page-header">
        <h1>Workshops</h1>
        <p>Every week a different discipline, a different host, a different way of seeing things. Show up curious — we handle the rest.</p>
    </div>

    <div class="workshops-cta-bar">
        <div class="workshops-cta-inner">
            <p>Planirate privatni event, team building ili vlastitu radionicu u našem prostoru?</p>
            <a href="/suradnja#upit" class="btn btn-outline btn-sm">Rezervirajte →</a>
        </div>
    </div>

    <div class="workshops-grid">
        @foreach (var p in Model.ReservableWorkshops)
        {
            var bookHref = p.BookingType == "email" ? $"mailto:{p.BookingValue}" : p.BookingValue ?? "/suradnja#upit";
            <a href="@bookHref" target="@(p.BookingType == "email" ? null : "_blank")" rel="@(p.BookingType == "email" ? null : "noopener")" class="workshop-card">
                <div class="workshop-card-banner">
                    @if (!string.IsNullOrEmpty(p.BannerUrl))
                    {
                        <img src="@p.BannerUrl" alt="@p.Name" />
                    }
                </div>
                <div class="workshop-card-body">
                    <p class="workshop-card-date">Uvijek dostupno</p>
                    <h3 class="workshop-card-title">@p.Name</h3>
                    <p class="workshop-card-desc">@p.Description</p>
                </div>
                <div class="workshop-card-footer">
                    <span class="btn btn-outline btn-sm">Rezerviraj →</span>
                </div>
            </a>
        }

        @foreach (var w in Model.Workshops)
        {
            bool hasLogo = !string.IsNullOrEmpty(w.LogoUrl)
                           && w.LogoUrl != "/images/workshops/placeholder-logo.png";
            var next = Model.NextOccurrenceByWorkshopId[w.Id];
            var extraCount = Model.UpcomingOccurrenceCountByWorkshopId[w.Id] - 1;
            <a href="/workshops/@w.Slug" class="workshop-card @(hasLogo ? "has-logo" : "")">
                <div class="workshop-card-banner">
                    @if (!string.IsNullOrEmpty(w.BannerUrl))
                    {
                        <img src="@w.BannerUrl" alt="@w.Name" />
                    }
                </div>
                @if (hasLogo)
                {
                    <div class="workshop-card-logo-wrap">
                        <img src="@w.LogoUrl" alt="@w.Name" />
                    </div>
                }
                <div class="workshop-card-body">
                    <p class="workshop-card-date">
                        @(extraCount > 0 ? "Više termina" : next.Date.ToString("dddd, MMMM d, yyyy", new System.Globalization.CultureInfo("en-US")))
                    </p>
                    <h3 class="workshop-card-title">@w.Name</h3>
                    @if (extraCount == 0)
                    {
                        <p class="workshop-card-time">
                            @next.StartTime.ToString(@"hh\:mm")
                            @(next.EndTime.HasValue ? " – " + next.EndTime.Value.ToString(@"hh\:mm") : "")
                        </p>
                    }
                    <p class="workshop-card-desc">@w.Description</p>
                </div>
                <div class="workshop-card-footer">
                    <span class="btn btn-outline btn-sm">Detalji</span>
                </div>
            </a>
        }

        @if (!Model.Workshops.Any() && !Model.ReservableWorkshops.Any())
        {
            <div class="workshops-empty">
                <p>Trenutno nema nadolazećih radionica.<br>
                   Pratite nas na <a href="https://instagram.com/workshop.zagreb" style="color:var(--terracotta)">Instagramu</a> za najave.</p>
            </div>
        }
    </div>
</div>
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: errors now only in `Pages/Workshops/Detail.cshtml(.cs)` and `Pages/Index.cshtml.cs`.

- [ ] **Step 4: Commit**

```bash
git add "Pages/Workshops/Index.cshtml.cs" "Pages/Workshops/Index.cshtml"
git commit -m "Public listing: one card per workshop, reservable Book button"
```

---

## Task 9: Public workshop detail — occurrence table / Book button, icon-ified host links

**Files:**
- Modify: `Pages/Workshops/Detail.cshtml.cs`
- Modify: `Pages/Workshops/Detail.cshtml`

- [ ] **Step 1: Replace `Pages/Workshops/Detail.cshtml.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Workshops;

public class WorkshopDetailModel : PageModel
{
    private readonly AppDbContext _db;
    public WorkshopDetailModel(AppDbContext db) => _db = db;

    public Workshop? Workshop { get; set; }
    public List<WorkshopOccurrence> UpcomingOccurrences { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Workshop = await _db.Workshops
            .Include(w => w.Photos)
            .Include(w => w.Occurrences)
            .FirstOrDefaultAsync(w => w.Slug == slug && !w.IsArchived);

        if (Workshop == null)
            return NotFound();

        UpcomingOccurrences = Workshop.Occurrences
            .Where(o => o.Date >= DateTime.Today)
            .OrderBy(o => o.Date)
            .ToList();

        return Page();
    }
}
```

- [ ] **Step 2: Replace `Pages/Workshops/Detail.cshtml`**

```html
@page "/workshops/{slug}"
@model WorkshopZagreb.Pages.Workshops.WorkshopDetailModel
@using System.Text.Json
@using WorkshopZagreb.Helpers
@{
    ViewData["Title"] = Model.Workshop?.Name ?? "Workshop";
    ViewData["Description"] = Model.Workshop?.Description;
    ViewData["OgImage"] = Model.Workshop?.BannerUrl;
    ViewData["OgType"] = "event";
}

@if (Model.Workshop != null && !Model.Workshop.IsReservable && Model.UpcomingOccurrences.Any())
{
    var _w = Model.Workshop;
    var _occ = Model.UpcomingOccurrences.First();
    var _startDt = _occ.Date.Date + _occ.StartTime;
    var _endDt   = _occ.Date.Date + (_occ.EndTime ?? _occ.StartTime.Add(TimeSpan.FromHours(2)));
    var _tz   = (_occ.Date.Month >= 4 && _occ.Date.Month <= 10) ? "+02:00" : "+01:00";
    var _start = _startDt.ToString("yyyy-MM-ddTHH:mm:ss") + _tz;
    var _end   = _endDt.ToString("yyyy-MM-ddTHH:mm:ss") + _tz;
    string J(string? s) => JsonSerializer.Serialize(s ?? "");

    <script type="application/ld+json">
    {
      "@@context": "https://schema.org",
      "@@type": "Event",
      "name": @Html.Raw(J(_w.Name)),
      "description": @Html.Raw(J(_w.Description)),
      "image": @Html.Raw(J(_w.BannerUrl)),
      "startDate": "@_start",
      "endDate": "@_end",
      "eventStatus": "https://schema.org/EventScheduled",
      "eventAttendanceMode": "https://schema.org/OfflineEventAttendanceMode",
      "location": {
        "@@type": "Place",
        "name": "Workshop Zagreb",
        "address": {
          "@@type": "PostalAddress",
          "streetAddress": "Henrika Degena 2",
          "addressLocality": "Zagreb",
          "addressRegion": "Grad Zagreb",
          "postalCode": "10000",
          "addressCountry": "HR"
        }
      },
      "organizer": {
        "@@type": "Organization",
        "name": "Workshop Zagreb",
        "url": "https://workshopzagreb.com"
      }@((_w.Price.HasValue && !string.IsNullOrEmpty(_occ.EntrioUrl)) ? Html.Raw($@",
      ""offers"": {{
        ""@@type"": ""Offer"",
        ""price"": ""{_w.Price:0}"",
        ""priceCurrency"": ""EUR"",
        ""availability"": ""https://schema.org/InStock"",
        ""url"": {J(_occ.EntrioUrl)}
      }}") : Html.Raw(""))
    }
    </script>
}

@if (Model.Workshop == null)
{
    <div class="static-page"><div class="container"><p>Workshop not found.</p></div></div>
}
else
{
    var w = Model.Workshop;
    <article class="workshop-detail">

        <div class="workshop-hero-banner">
            <div class="workshop-hero-banner-img-area">
                @if (!string.IsNullOrEmpty(w.BannerUrl))
                {
                    <img src="@w.BannerUrl" alt="@w.Name" />
                }
                else
                {
                    <div class="workshop-hero-banner-empty"></div>
                }
            </div>
            @if (!string.IsNullOrEmpty(w.LogoUrl))
            {
                <div class="workshop-hero-logo-wrap">
                    <img src="@w.LogoUrl" alt="@w.Name logo" />
                </div>
            }
        </div>

        <div class="workshop-detail-grid">

            <div>
                <h1 class="workshop-detail-title">@w.Name</h1>

                <div class="workshop-detail-meta">
                    @if (!w.IsReservable)
                    {
                        @foreach (var occ in Model.UpcomingOccurrences)
                        {
                            <span class="meta-pill">
                                <svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
                                @occ.Date.ToString("dddd, MMMM d, yyyy", new System.Globalization.CultureInfo("en-US"))
                                — @occ.StartTime.ToString(@"hh\:mm")@(occ.EndTime.HasValue ? " – " + occ.EndTime.Value.ToString(@"hh\:mm") : "")
                            </span>
                        }
                    }
                    @if (w.MaxParticipants.HasValue)
                    {
                        <span class="meta-pill">
                            <svg viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
                            Max @w.MaxParticipants spots
                        </span>
                    }
                </div>

                <p class="workshop-detail-desc">@w.Description</p>

                @if (!string.IsNullOrEmpty(w.InstagramPostUrl))
                {
                    <div style="margin-top:40px;">
                        <p style="font-size:0.78rem;opacity:0.5;margin-bottom:12px;text-transform:uppercase;letter-spacing:0.1em;">Instagram post</p>
                        <a href="@w.InstagramPostUrl" target="_blank" rel="noopener" class="btn btn-outline btn-sm">
                            View on Instagram ↗
                        </a>
                    </div>
                }

                @if (w.Photos.Any())
                {
                    <div class="workshop-photos-grid">
                        @foreach (var photo in w.Photos)
                        {
                            <img src="@photo.Url" alt="@w.Name" />
                        }
                    </div>
                }
            </div>

            <aside class="workshop-sidebar">
                <div class="sidebar-card">
                    <p class="sidebar-price">@(w.Price.HasValue ? $"{w.Price:0} €" : "Free")</p>
                    <p class="sidebar-seats">
                        @(w.MaxParticipants.HasValue ? $"Max {w.MaxParticipants} participants" : "Open registration")
                    </p>

                    @if (w.IsReservable)
                    {
                        var bookHref = w.BookingType == "email" ? $"mailto:{w.BookingValue}" : w.BookingValue ?? "/suradnja#upit";
                        <a href="@bookHref" target="@(w.BookingType == "email" ? null : "_blank")" rel="@(w.BookingType == "email" ? null : "noopener")" class="btn btn-primary">
                            Rezerviraj →
                        </a>
                    }
                    else if (Model.UpcomingOccurrences.Any())
                    {
                        var nextOcc = Model.UpcomingOccurrences.First();
                        <a href="@GoogleCalendarHelper.BuildAddToCalendarUrl(w, nextOcc)" target="_blank" rel="noopener" class="btn btn-outline">
                            + Add to Google Calendar
                        </a>

                        if (w.Price.HasValue)
                        {
                            if (!string.IsNullOrEmpty(nextOcc.EntrioUrl))
                            {
                                <a href="@nextOcc.EntrioUrl" target="_blank" rel="noopener" class="btn btn-primary">
                                    Buy ticket (Entrio) ↗
                                </a>
                            }
                            else
                            {
                                <a href="https://www.instagram.com/workshop.zagreb/" target="_blank" class="btn btn-primary">
                                    Register via Instagram
                                </a>
                            }
                        }
                    }

                    @if (!string.IsNullOrEmpty(w.HostName))
                    {
                        <div class="sidebar-host">
                            <h4>Workshop host</h4>
                            <p>@w.HostName</p>
                            <div style="display:flex;gap:12px;margin-top:6px;flex-wrap:wrap;align-items:center;">
                                @if (!string.IsNullOrEmpty(w.HostInstagram))
                                {
                                    <a href="@w.HostInstagram" target="_blank" rel="noopener" aria-label="Instagram" class="icon-link">
                                        <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><rect x="2" y="2" width="20" height="20" rx="5" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="12" cy="12" r="4" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="17.5" cy="6.5" r="1.2"/></svg>
                                    </a>
                                }
                                @if (!string.IsNullOrEmpty(w.HostWebsite))
                                {
                                    <a href="@w.HostWebsite" target="_blank" rel="noopener" aria-label="Website" class="icon-link">
                                        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 0 20 15.3 15.3 0 0 1 0-20z"/></svg>
                                    </a>
                                }
                            </div>
                        </div>
                    }
                </div>
            </aside>

        </div>
    </article>
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: errors now only in `Pages/Index.cshtml.cs`.

- [ ] **Step 4: Manual verification**

Visit `/workshops/rodendanska-radionica` (the reservable seed) directly — confirm it shows the Book button, no date/time, no "Add to Google Calendar". Visit a regular workshop with two dates (from Task 6) — confirm both dates list in the meta pills and the sidebar shows the nearest occurrence's ticket/calendar actions.

- [ ] **Step 5: Commit**

```bash
git add "Pages/Workshops/Detail.cshtml.cs" "Pages/Workshops/Detail.cshtml"
git commit -m "Public detail page: occurrence table / reservable Book button, icon host links"
```

---

## Task 10: Homepage — newsletter reorder, 2-month window, reservable appended, calendar via occurrences

**Files:**
- Modify: `Pages/Index.cshtml.cs`
- Modify: `Pages/Index.cshtml`

- [ ] **Step 1: Replace `Pages/Index.cshtml.cs`**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using System.Globalization;

namespace WorkshopZagreb.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Workshop> UpcomingWorkshops { get; set; } = new();
    public List<Workshop> ReservableWorkshops { get; set; } = new();
    public Dictionary<int, WorkshopOccurrence> NextOccurrenceByWorkshopId { get; set; } = new();

    public Dictionary<DateTime, List<(Workshop Workshop, WorkshopOccurrence Occurrence)>> WorkshopsByDate { get; set; } = new();
    public Dictionary<DateTime, ReservedDay> ReservedDays { get; set; } = new();
    public HashSet<DateTime> Holidays { get; set; } = new();

    public DateTime CurrentMonthStart { get; set; }
    public int      CurrentMonthDays  { get; set; }
    public string   CurrentMonthName  { get; set; } = "";
    public int      CurrentYear       { get; set; }

    public DateTime NextMonthStart { get; set; }
    public int      NextMonthDays  { get; set; }
    public string   NextMonthName  { get; set; } = "";
    public int      NextYear       { get; set; }

    public async Task OnGetAsync()
    {
        var en = new CultureInfo("en-US");
        var today = DateTime.Today;

        CurrentMonthStart = new DateTime(today.Year, today.Month, 1);
        CurrentMonthDays  = DateTime.DaysInMonth(today.Year, today.Month);
        CurrentMonthName  = en.DateTimeFormat.GetMonthName(today.Month);
        CurrentYear       = today.Year;

        NextMonthStart = CurrentMonthStart.AddMonths(1);
        NextMonthDays  = DateTime.DaysInMonth(NextMonthStart.Year, NextMonthStart.Month);
        NextMonthName  = en.DateTimeFormat.GetMonthName(NextMonthStart.Month);
        NextYear       = NextMonthStart.Year;

        var rangeEnd = NextMonthStart.AddMonths(1);

        Holidays = CroatianHolidays(CurrentMonthStart.Year);
        if (NextMonthStart.Year != CurrentMonthStart.Year)
            foreach (var h in CroatianHolidays(NextMonthStart.Year))
                Holidays.Add(h);

        // Calendar grid: non-reservable workshops with an occurrence in the visible 2-month window
        var workshopsWithOccurrences = await _db.Workshops
            .Include(w => w.Photos)
            .Include(w => w.Occurrences)
            .Where(w => !w.IsArchived && !w.IsReservable)
            .ToListAsync();

        var calendarEntries = workshopsWithOccurrences
            .SelectMany(w => w.Occurrences
                .Where(o => o.Date >= CurrentMonthStart && o.Date < rangeEnd)
                .Select(o => (Workshop: w, Occurrence: o)))
            .OrderBy(e => e.Occurrence.Date).ThenBy(e => e.Occurrence.StartTime)
            .ToList();

        WorkshopsByDate = calendarEntries
            .GroupBy(e => e.Occurrence.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        ReservedDays = (await _db.ReservedDays
            .Where(r => r.Date >= CurrentMonthStart && r.Date < rangeEnd)
            .ToListAsync())
            .ToDictionary(r => r.Date.Date);

        // Upcoming Workshops strip: non-reservable workshops with a date in the next 2 months, capped at 8
        var twoMonthsOut = today.AddMonths(2);
        UpcomingWorkshops = workshopsWithOccurrences
            .Where(w => w.Occurrences.Any(o => o.Date >= today && o.Date < twoMonthsOut))
            .OrderBy(w => w.Occurrences.Where(o => o.Date >= today && o.Date < twoMonthsOut).Min(o => o.Date))
            .Take(8)
            .ToList();

        foreach (var w in UpcomingWorkshops)
        {
            NextOccurrenceByWorkshopId[w.Id] = w.Occurrences
                .Where(o => o.Date >= today)
                .OrderBy(o => o.Date)
                .First();
        }

        // Reservable workshops appended after the dated ones in the same strip
        ReservableWorkshops = await _db.Workshops
            .Where(w => w.IsReservable && !w.IsArchived)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    private static HashSet<DateTime> CroatianHolidays(int year)
    {
        var easter = CalculateEaster(year);

        return new HashSet<DateTime>
        {
            new(year, 1,  1),
            new(year, 1,  6),
            easter,
            easter.AddDays(1),
            new(year, 5,  1),
            new(year, 5, 30),
            easter.AddDays(60),
            new(year, 6, 22),
            new(year, 8,  5),
            new(year, 8, 15),
            new(year, 11, 1),
            new(year, 11, 18),
            new(year, 12, 25),
            new(year, 12, 26),
        };
    }

    private static DateTime CalculateEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day   = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
```

- [ ] **Step 2: Update `Pages/Index.cshtml`** — three separate edits (upcoming-workshops loop, calendar day rendering, newsletter section move)

Edit 2a — replace the "Upcoming Workshops" section (lines 57–110) to iterate occurrences and append reservable workshops:

```html
<!-- ============= UPCOMING WORKSHOPS ============= -->
<section class="workshops-section">
    <div class="section-header">
        <h2>Upcoming Workshops</h2>
        <a href="/workshops">All workshops →</a>
    </div>

    @if (Model.UpcomingWorkshops.Any() || Model.ReservableWorkshops.Any())
    {
        <div class="workshops-scroll-track">
            @foreach (var w in Model.UpcomingWorkshops)
            {
                bool hasLogo = !string.IsNullOrEmpty(w.LogoUrl)
                               && w.LogoUrl != "/images/workshops/placeholder-logo.png";
                var occ = Model.NextOccurrenceByWorkshopId[w.Id];
                <a href="/workshops/@w.Slug" class="workshop-card @(hasLogo ? "has-logo" : "")">
                    <div class="workshop-card-banner">
                        @if (!string.IsNullOrEmpty(w.BannerUrl))
                        {
                            <img src="@w.BannerUrl" alt="@w.Name" />
                        }
                    </div>
                    @if (hasLogo)
                    {
                        <div class="workshop-card-logo-wrap">
                            <img src="@w.LogoUrl" alt="@w.Name" />
                        </div>
                    }
                    <div class="workshop-card-body">
                        <p class="workshop-card-date">
                            @occ.Date.ToString("ddd, MMM d", new System.Globalization.CultureInfo("en-US"))
                        </p>
                        <h3 class="workshop-card-title">@w.Name</h3>
                        <p class="workshop-card-time">
                            @occ.StartTime.ToString(@"hh\:mm")
                            @(occ.EndTime.HasValue ? " – " + occ.EndTime.Value.ToString(@"hh\:mm") : "")
                        </p>
                        <p class="workshop-card-desc">@w.Description</p>
                    </div>
                    <div class="workshop-card-footer">
                        <span class="workshop-price">@(w.Price.HasValue ? $"{w.Price:0} €" : "Free")</span>
                        @if (w.Price.HasValue && w.Price > 0)
                        {
                            <span class="btn btn-outline btn-sm">Reserve</span>
                        }
                    </div>
                </a>
            }
            @foreach (var p in Model.ReservableWorkshops)
            {
                var bookHref = p.BookingType == "email" ? $"mailto:{p.BookingValue}" : p.BookingValue ?? "/suradnja#upit";
                <a href="@bookHref" target="@(p.BookingType == "email" ? null : "_blank")" rel="@(p.BookingType == "email" ? null : "noopener")" class="workshop-card">
                    <div class="workshop-card-banner">
                        @if (!string.IsNullOrEmpty(p.BannerUrl))
                        {
                            <img src="@p.BannerUrl" alt="@p.Name" />
                        }
                    </div>
                    <div class="workshop-card-body">
                        <p class="workshop-card-date">Uvijek dostupno</p>
                        <h3 class="workshop-card-title">@p.Name</h3>
                        <p class="workshop-card-desc">@p.Description</p>
                    </div>
                    <div class="workshop-card-footer">
                        <span class="btn btn-outline btn-sm">Rezerviraj →</span>
                    </div>
                </a>
            }
        </div>
    }
    else
    {
        <div class="workshops-empty"><p>No upcoming workshops at the moment. Follow us on Instagram for updates!</p></div>
    }
</section>
```

Edit 2b — in the calendar section (both "CURRENT MONTH" and "NEXT MONTH" blocks), `Model.WorkshopsByDate` now yields `(Workshop, Occurrence)` tuples instead of `Workshop` — update both occurrences of this pattern:

Find (appears twice, once per month block):
```html
var ws       = Model.WorkshopsByDate.TryGetValue(date, out var list) ? list : new();
```
and the matching `list2` line — these stay the same (variable still named `ws`, just now a list of tuples). Then find, in both month blocks:
```html
var logoW = ws.FirstOrDefault(w => !string.IsNullOrEmpty(w.LogoUrl));
```
Replace with:
```html
var logoEntry = ws.FirstOrDefault(e => !string.IsNullOrEmpty(e.Workshop.LogoUrl));
```
And the `@if (logoW != null)` block:
```html
@if (logoW != null)
{
    <img src="@logoW.LogoUrl" class="cal-day-logo" alt="" />
}
```
Replace with:
```html
@if (logoEntry.Workshop != null)
{
    <img src="@logoEntry.Workshop.LogoUrl" class="cal-day-logo" alt="" />
}
```
And the day-link href:
```html
<a href="/workshops/@ws.First().Slug" class="cal-day ...">
```
Replace with:
```html
<a href="/workshops/@ws.First().Workshop.Slug" class="cal-day ...">
```
And the event name loop:
```html
@foreach (var w in ws)
{
    <span class="cal-event">
        <span class="cal-event-name">@w.Name</span>
    </span>
}
```
Replace with:
```html
@foreach (var entry in ws)
{
    <span class="cal-event">
        <span class="cal-event-name">@entry.Workshop.Name</span>
    </span>
}
```
Apply this same set of 4 replacements to both the "CURRENT MONTH" and "NEXT MONTH" blocks (8 replacements total — 4 per block).

Edit 2c — move the Newsletter section. Cut the entire `<!-- ============= NEWSLETTER ============= -->` section (currently between Calendar and Mood Split) and paste it back in, unchanged, directly before `<!-- ============= INSTAGRAM CTA ============= -->` (i.e., after the Quad Photo Grid section's closing `</section>`). Final section order: Hero → CTA → Photo Strip → Upcoming Workshops → Calendar → Mood Split → Quad Grid → **Newsletter** → Instagram CTA.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: SUCCESS — this was the last file with compile errors.

- [ ] **Step 4: Manual verification**

Start the dev server, load `/`. Confirm: (a) the newsletter section now appears directly above "Follow Us" / `@workshop.zagreb`, after the 4-photo grid; (b) the "Upcoming Workshops" strip no longer shows the reservable "Rođendanska radionica" mixed in with dated cards — it appears at the end with "Uvijek dostupno" and a working Book link; (c) the calendar still renders correctly with workshop names/logos on the right days.

- [ ] **Step 5: Commit**

```bash
git add "Pages/Index.cshtml.cs" "Pages/Index.cshtml"
git commit -m "Homepage: 2-month upcoming window + reservable appended, newsletter reorder"
```

---

## Task 11: Icons — footer and About page contact rows

**Files:**
- Modify: `Pages/Shared/_Layout.cshtml`
- Modify: `Pages/About.cshtml`
- Modify: `wwwroot/css/site.css`

- [ ] **Step 1: Add a small `.icon-link` style to `wwwroot/css/site.css`**

Append to the end of the file:

```css
.icon-link { display: inline-flex; align-items: center; justify-content: center; color: inherit; opacity: 0.75; transition: opacity 0.15s; }
.icon-link:hover { opacity: 1; }
.footer-social .icon-link { width: 20px; height: 20px; }
```

- [ ] **Step 2: Replace the footer social row in `Pages/Shared/_Layout.cshtml`**

Find:
```html
            <div class="footer-social">
                <a href="https://www.instagram.com/workshop.zagreb/" target="_blank" rel="noopener">Instagram ↗</a>
                <a href="mailto:hello@workshopzagreb.com">hello@workshopzagreb.com</a>
            </div>
```
Replace with:
```html
            <div class="footer-social" style="display:flex;flex-direction:row;gap:14px;">
                <a href="https://www.instagram.com/workshop.zagreb/" target="_blank" rel="noopener" aria-label="Instagram" class="icon-link">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><rect x="2" y="2" width="20" height="20" rx="5" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="12" cy="12" r="4" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="17.5" cy="6.5" r="1.2"/></svg>
                </a>
                <a href="mailto:hello@workshopzagreb.com" aria-label="Email" class="icon-link">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m2 7 10 6 10-6"/></svg>
                </a>
            </div>
```

- [ ] **Step 3: Replace the About page contact rows in `Pages/About.cshtml`**

Find:
```html
                    <div class="info-row">
                        <div>
                            <a href="https://www.instagram.com/workshop.zagreb/" target="_blank"
                               style="color:var(--terracotta)">@@workshop.zagreb</a>
                        </div>
                    </div>
                    <div class="info-row">
                        <div>
                            <a href="mailto:hello@workshopzagreb.com"
                               style="color:var(--terracotta)">hello@workshopzagreb.com</a>
                        </div>
                    </div>
```
Replace with:
```html
                    <div class="info-row">
                        <div style="display:flex;gap:16px;">
                            <a href="https://www.instagram.com/workshop.zagreb/" target="_blank" rel="noopener" aria-label="Instagram" class="icon-link" style="color:var(--terracotta);">
                                <svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor"><rect x="2" y="2" width="20" height="20" rx="5" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="12" cy="12" r="4" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="17.5" cy="6.5" r="1.2"/></svg>
                            </a>
                            <a href="mailto:hello@workshopzagreb.com" aria-label="Email" class="icon-link" style="color:var(--terracotta);">
                                <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m2 7 10 6 10-6"/></svg>
                            </a>
                        </div>
                    </div>
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: SUCCESS (CSS/markup-only change, no C# touched).

- [ ] **Step 5: Manual verification**

Load `/` and `/about` in the browser. Confirm the footer and About contact row now show two small icons (Instagram glyph, envelope glyph) with no visible text, both clickable (Instagram opens in a new tab, email opens a `mailto:` link). Confirm the workshop-detail host Instagram/Website links from Task 9 also render as icons, not text.

- [ ] **Step 6: Commit**

```bash
git add "Pages/Shared/_Layout.cshtml" "Pages/About.cshtml" "wwwroot/css/site.css"
git commit -m "Icon-ify footer, About, and workshop-detail host contact links"
```

---

## Task 12: Full smoke test

**Files:** none (verification only)

- [ ] **Step 1: Fresh-DB smoke test**

```bash
rm -f workshop.db
dotnet run
```
Expected: starts cleanly, no exceptions in the console, seeds 3 sample workshops + 1 reservable workshop.

- [ ] **Step 2: Walk the golden path in the browser**

1. `/` — homepage loads, newsletter section is right before "Follow Us", upcoming strip shows dated workshops then the reservable one, calendar renders.
2. `/workshops` — one card per workshop; reservable card has a working Book button.
3. Click into a regular workshop's detail page — occurrence(s) show correctly; sidebar ticket/calendar actions work.
4. Visit `/workshops/rodendanska-radionica` directly — Book button only, no date/time UI.
5. `/admin` — log in, "Reservable" tab (not "Pinned"), Upcoming tab shows next-occurrence + count.
6. Admin: edit a workshop, add a second date via "+ Novi datum", confirm it appears on both `/admin` and the public site.
7. Admin: delete a workshop, confirm it's gone from all three tabs and from the public site — then restart the dev server (`Ctrl+C`, `dotnet run` again) and confirm it does **not** come back (this is the regression check for the reseed bug).

- [ ] **Step 3: Confirm no leftover references to removed members**

Run: `dotnet build`
Expected: 0 errors, 0 warnings referencing `IsPinned`, `PinnedWorkshop`, or `Workshop.Date`/`.StartTime`/`.EndTime`/`.EntrioUrl`.

- [ ] **Step 4: Final commit (if step 2/3 turned up fixes)**

```bash
git add -A
git commit -m "Fix smoke-test findings"
```
(Skip this commit if nothing needed fixing.)
