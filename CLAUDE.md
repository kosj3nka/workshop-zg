# CLAUDE.md — Context for Claude Code

This file tells Claude Code everything it needs to pick up this project immediately.

## What This Is

Website for **Workshop Zagreb** — a Zagreb café that hosts creative afternoon workshops.
- Public site: home, workshops, menu, about, FAQ, gallery, collaboration
- Admin panel: owners (Marta & Luka) manage workshops and schedule dates themselves

## Tech Stack

| Layer | Choice |
|-------|--------|
| Framework | **ASP.NET Core .NET 8** (NOT .NET Framework) |
| UI Pattern | **Razor Pages** (not MVC, not Blazor) |
| Database | **Azure SQL** via **Entity Framework Core** |
| Media | **Azure Blob Storage** for all uploads |
| Auth | **ASP.NET Core Identity** (single admin account) |
| CSS | Vanilla CSS, single `site.css`, no build step, no npm |
| JS | Minimal / none. Exceptions: hamburger menu toggle, lightbox |
| Host | **Azure App Service B1** |

## Brand Colours (CSS variables in `site.css`)

```css
--clr-cream: #f5f0e8;
--clr-dark:  #1a1a1a;
--clr-accent: #c8a96e;
--font-display: 'Playfair Display', serif;
--font-body: 'Inter', sans-serif;
```

## What's Already Built

- ✅ Homepage (full): video hero, photo strip, journal calendar, mood split, quad grid, Instagram CTA
- ✅ O nama page (full): hero, founders story, concept cards, photo sets, press quote, map
- ✅ Suradnja page (full): dark hero, workshop hosting section, brand placement section

## What Needs Building Next (in order)

1. `AppDbContext.cs` + EF Core migrations
2. `BlobStorageService.cs`
3. Admin login + Workshop CRUD forms
4. Radionice listing + detail pages (they query the DB)
5. Meni, FAQ, Galerija pages (static/semi-static)
6. Deploy to Azure

## Key Files to Know

```
Pages/Index.cshtml          — Homepage
Pages/ONama.cshtml          — About
Pages/Suradnja.cshtml       — Collaboration (new page)
Pages/Radionice/Index.cshtml — Workshop listing (scaffolded, incomplete)
wwwroot/css/site.css        — All styles
Models/Workshop.cs          — Workshop template model
Models/WorkshopOccurrence.cs — Specific date instance
Data/AppDbContext.cs        — (TO BE CREATED)
Services/BlobStorageService.cs — (TO BE CREATED)
```

## Important Rules / Decisions

- **Do not add Bootstrap or Tailwind.** Styles are handcrafted and brand-specific.
- **Do not add npm or a JS build pipeline.** Keep it simple for a small site.
- **Do not add payment processing in V1.** Booking = external URL link per occurrence.
- **Language**: all UI strings in **Croatian**. Code/files in English.
- **Calendar**: server-rendered, no JS framework. Mobile uses CSS pill toggle.
- **Footer logo**: uses CSS `filter: invert(1)` to go white on dark background.
- **Admin**: single account, seeded on first run. No self-registration.

## Data Model Summary

```
Workshop (template)
├── Id, Name, Slug, Description, ShortDescription
├── Price, DurationMinutes, IconFileName, IsActive
└── ICollection<WorkshopOccurrence>

WorkshopOccurrence (specific event)
├── Id, WorkshopId → Workshop
├── StartsAt (DateTime), MaxParticipants
└── BookingUrl (nullable — external link)
```

## See Also

- `PROGRESS.md` — detailed status of every component
- `TASKS.md` — all remaining work, prioritised 🔴🟡🟢
- `ARCHITECTURE.md` — rationale for every tech decision
- `IMAGES.md` — every image file needed and where it goes
- `DEPLOYMENT.md` — Azure setup commands
