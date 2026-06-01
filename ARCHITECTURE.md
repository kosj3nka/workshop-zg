# Architecture & Technical Decisions

## Stack Choice

### Why ASP.NET Core (.NET 8) — not .NET Framework
- **.NET Framework** is the legacy Windows-only stack, last major release 2019. **Do not use.**
- **.NET 8** is the modern, cross-platform successor. Azure App Service is optimised for it; deployment is one-click from GitHub Actions.
- All new Microsoft docs, tutorials, and NuGet packages target .NET 6+.

### Why Razor Pages — not MVC or Blazor
- The admin panel is pure CRUD (create/edit/delete workshops). Razor Pages maps one `.cshtml` file to one route — no controller/action overhead.
- Owners get a simple form. Razor Pages `OnPostAsync` handles validation and saves in ~20 lines.
- Blazor would be overkill; no real-time interactivity needed.
- MVC would work but adds indirection (controllers) that serves no benefit here.

### Why Azure
- Native .NET support: `az webapp deploy` or GitHub Actions with `azure/webapps-deploy` just works.
- **Azure Blob Storage** for all uploaded media (workshop icons, gallery images, hero video clips). Owners upload → file gets a CDN URL → stored in DB. No filesystem hacks.
- **Azure SQL** (Standard S0 tier) for structured data. EF Core handles migrations.
- **Azure App Service** B1 tier (~$13/mo) is sufficient for this traffic level.

### Why NOT a CMS (WordPress, Contentful, etc.)
- The only dynamic content is workshops (add/edit/delete + schedule dates). A full CMS is heavyweight for this.
- Owners are non-technical — a custom admin form with only the fields they need is better UX than any generic CMS.

---

## Data Model

```csharp
// Workshop = the template (e.g. "Linocut Printing")
public class Workshop
{
    public int Id { get; set; }
    public string Name { get; set; }           // "Linocut Printing"
    public string Slug { get; set; }           // "linocut-printing" (URL)
    public string Description { get; set; }
    public string ShortDescription { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string IconFileName { get; set; }   // stored in Blob Storage
    public bool IsActive { get; set; }
    public ICollection<WorkshopOccurrence> Occurrences { get; set; }
}

// WorkshopOccurrence = a specific date/time instance
public class WorkshopOccurrence
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; }
    public DateTime StartsAt { get; set; }
    public int MaxParticipants { get; set; }
    public string? BookingUrl { get; set; }    // external Eventbrite/Google Form link
}
```

**Rationale**: Splitting Workshop from WorkshopOccurrence lets owners reuse the same workshop definition across many dates. The calendar shows occurrences; clicking navigates to the parent Workshop detail page.

---

## Calendar Implementation

- **Server-rendered** Razor, no JavaScript required.
- `Index.cshtml.cs` calculates current month + next month, queries `WorkshopOccurrences` for that date range, builds a `Dictionary<DateTime, List<WorkshopOccurrence>>` keyed by date.
- The Razor template iterates weeks/days and injects workshop icons where matches exist.
- **Mobile**: CSS-only pill toggle (`#month-toggle`) shows/hides the two month columns. No JS.
- Each workshop icon is a `<a href="/radionice/{slug}">` — direct navigation, no modal.

---

## Media Handling

```
Owner uploads file (admin form)
        ↓
BlobStorageService.UploadAsync()
        ↓
Azure Blob Storage container: "workshop-media"
        ↓
Public CDN URL saved to DB (Workshop.IconFileName or Gallery table)
        ↓
Razor template renders <img src="@workshop.IconUrl">
```

- Hero videos live in `wwwroot/videos/` (checked into repo or deployed separately). They are static — owners don't change these.
- Workshop icons and gallery photos are owner-managed via Blob Storage.

---

## Authentication (Admin Panel)

- **ASP.NET Core Identity** with a single admin account (seeded on first run).
- No registration — owners log in at `/admin/login`, session cookie lasts 30 days.
- All `/Admin/*` pages require `[Authorize]`.
- No third-party OAuth needed (site has one owner pair, not many users).

---

## CSS Architecture

- Single `wwwroot/css/site.css` — no build pipeline, no npm.
- CSS custom properties for brand colours:
  ```css
  :root {
    --clr-cream: #f5f0e8;
    --clr-dark: #1a1a1a;
    --clr-accent: #c8a96e;   /* warm gold */
    --font-display: 'Playfair Display', serif;
    --font-body: 'Inter', sans-serif;
  }
  ```
- Layout utility: `.container` (max 1200px) and `.container-narrow` (max 1000px, 32px padding).
- No Tailwind, no Bootstrap — keeps the stylesheet lean and brand-specific.

---

## Localisation

- Site language: **Croatian**.
- No i18n framework. All strings are hardcoded in Croatian in `.cshtml` files.
- English-language technical fields (slugs, file names) remain in English internally.

---

## Booking Flow

- Workshop.Zagreb does not take payments on-site.
- Each `WorkshopOccurrence` has an optional `BookingUrl` (Eventbrite, Google Forms, WhatsApp link, etc.).
- The "Book" button is a plain `<a href="@occurrence.BookingUrl" target="_blank">` — no payment integration needed.
- This can be upgraded to in-house booking later (see TASKS.md).

---

## Performance Notes

- Hero video uses `autoplay muted loop playsinline` with multiple `<source>` tags (`.webm` first, `.mp4` fallback).
- `hero-poster.jpg` shown while video loads — critical for mobile.
- All images should be provided at 2× max display size and compressed (WebP preferred, JPEG acceptable).
- No heavy JS bundles. Page weight target: < 500 KB initial load (excluding video).
