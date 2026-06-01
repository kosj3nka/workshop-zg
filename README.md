# Workshop Zagreb — Website

> **"Mornings for coffee, afternoons for creativity!"**
> A Zagreb café with rotating creative afternoon workshops.
> Instagram: [@workshop.zagreb](https://www.instagram.com/workshop.zagreb/)

---

## Project Overview

A public-facing website + owner-managed admin panel for Workshop Zagreb. Owners (Marta & Luka) can independently add, edit, and delete workshops — no developer needed after handoff.

## Quick Start (Claude Code)

```bash
# Prerequisites: .NET 8 SDK, Azure CLI, SQL Server (local or Azure)

cd WorkshopZagreb
dotnet restore
dotnet ef database update          # applies migrations
dotnet run                         # http://localhost:5000
```

## Repo Structure

```
WorkshopZagreb/
├── Pages/
│   ├── Index.cshtml               # Homepage
│   ├── Radionice/                 # Workshops listing + detail
│   │   ├── Index.cshtml
│   │   └── Detail.cshtml
│   ├── Meni.cshtml                # Menu
│   ├── ONama.cshtml               # About Us
│   ├── FAQ.cshtml
│   ├── Galerija.cshtml            # Gallery
│   ├── Suradnja.cshtml            # Collaboration
│   └── Admin/                    # Owner-only CRUD panel
│       ├── Login.cshtml
│       ├── Dashboard.cshtml
│       └── Workshop/
│           ├── Create.cshtml
│           ├── Edit.cshtml
│           └── Delete.cshtml
├── Models/
│   ├── Workshop.cs
│   └── WorkshopOccurrence.cs
├── Data/
│   └── AppDbContext.cs
├── Services/
│   └── BlobStorageService.cs
├── wwwroot/
│   ├── css/site.css
│   ├── js/
│   ├── images/                    # See IMAGES.md for required files
│   └── videos/                    # Hero background clips
├── appsettings.json
└── Program.cs
```

## Key Docs

| File | Purpose |
|------|---------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | All technical decisions & rationale |
| [PROGRESS.md](./PROGRESS.md) | What's built, what's working |
| [TASKS.md](./TASKS.md) | Remaining work, prioritised |
| [IMAGES.md](./IMAGES.md) | Every image file needed + where used |
| [DEPLOYMENT.md](./DEPLOYMENT.md) | Azure setup & CI/CD guide |
