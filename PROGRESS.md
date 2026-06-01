# Progress Log

Status legend: ✅ Done · 🔧 Partial · ⬜ Not started

---

## Phase 1 — Design & Planning ✅

- [x] Tech stack decided: ASP.NET Core .NET 8, Razor Pages, Azure
- [x] Brand direction established: cream/dark/gold palette, Playfair Display + Inter
- [x] Page list finalised: Home, Radionice, Meni, O nama, FAQ, Galerija, Suradnja, Admin
- [x] Data model designed (Workshop + WorkshopOccurrence split)
- [x] Image file list created (see IMAGES.md)

---

## Phase 2 — Frontend Pages

### Homepage (`Pages/Index.cshtml`) ✅
- [x] Navbar — logo (`logoName.png`), links: Radionice, Meni, O nama, FAQ, Galerija
- [x] Hero — fullscreen video loop, `justLogo.png` centred over video, tagline "Mornings for coffee, afternoons for creativity!"
- [x] CTA section — "Book a workshop" + "Find us" buttons
- [x] Photo strip — 3 vibe images (`vibe1–3.jpg`)
- [x] Journal-style calendar — current + next month, workshop icons on dates, mobile pill toggle
- [x] Mood split — full-width editorial photo (`mood1.jpg`) with overlaid text
- [x] Quad photo grid — 4-column mosaic (`quad1–4.jpg`)
- [x] Instagram CTA strip — link to @workshop.zagreb
- [x] Footer — logo, nav links, address, social

### O nama (`Pages/ONama.cshtml`) ✅
- [x] Hero banner (`about-hero.jpg`)
- [x] Founders section — Marta & Luka story, 3LHD background, Ribnjak concept (`about-founders.jpg`)
- [x] Concept cards — kava / radionice dual card layout
- [x] Photo trio (`about-space1–3.jpg`)
- [x] Full-width photo (`about-wide.jpg`)
- [x] 4-photo detail grid (`about-detail1–4.jpg`)
- [x] Press quote block (Journal.hr)
- [x] Info + map section
- [x] `.container-narrow` (max 1000px, 32px padding) applied for tighter feel

### Suradnja (`Pages/Suradnja.cshtml`) ✅ (new page, added late)
- [x] Dark hero section
- [x] "Vodite radionicu" — what Workshop offers to guest hosts (`collab-workshop.jpg`)
- [x] Point-of-sale marketing & brand placement section (`collab-marketing.jpg`)
- [x] CTA at bottom

### Radionice listing (`Pages/Radionice/Index.cshtml`) 🔧
- [x] Page scaffolded
- [ ] Workshop card grid (name, icon, short description, price)
- [ ] Filter by category (if categories added)

### Radionice detail (`Pages/Radionice/Detail.cshtml`) ⬜
- [ ] Full description, images
- [ ] Upcoming occurrences list with "Book" buttons

### Meni (`Pages/Meni.cshtml`) ⬜
- [ ] Design + content (coffee, drinks, light food)

### FAQ (`Pages/FAQ.cshtml`) ⬜
- [ ] Accordion component
- [ ] Content written with owners

### Galerija (`Pages/Galerija.cshtml`) ⬜
- [ ] Masonry/grid layout
- [ ] Images served from Blob Storage
- [ ] Lightbox on click (vanilla JS, no library)

---

## Phase 3 — Backend & Database

### Data Layer ⬜
- [ ] `AppDbContext.cs` with DbSets
- [ ] EF Core migrations (`dotnet ef migrations add InitialCreate`)
- [ ] Seed: 3–5 example workshops for dev/demo

### Admin Panel ⬜
- [ ] `/Admin/Login` — ASP.NET Identity login form
- [ ] `/Admin/Dashboard` — list of all workshops + occurrences
- [ ] `/Admin/Workshop/Create` — form: name, description, price, duration, icon upload, active toggle
- [ ] `/Admin/Workshop/Edit/{id}` — same form, pre-filled
- [ ] `/Admin/Workshop/Delete/{id}` — confirmation page
- [ ] `/Admin/Occurrence/Create` — pick workshop, set date/time, capacity, booking URL
- [ ] `/Admin/Occurrence/Edit/{id}`
- [ ] `/Admin/Occurrence/Delete/{id}`

### Blob Storage Service ⬜
- [ ] `BlobStorageService.cs` — upload, delete, get URL
- [ ] Connection string via `appsettings.json` / Azure Key Vault

---

## Phase 4 — Deployment ⬜

- [ ] Azure App Service created (B1 tier)
- [ ] Azure SQL database provisioned
- [ ] Azure Blob Storage container created (`workshop-media`, public read)
- [ ] GitHub Actions CI/CD pipeline
- [ ] Custom domain + HTTPS
- [ ] Admin account seeded in production

---

## Known Issues / Design Decisions Made Mid-Build

| Issue | Resolution |
|-------|-----------|
| Owner asked for .NET Framework | Switched to .NET 8 — explained why |
| Calendar needs no fixed schedule | WorkshopOccurrence model; calendar queries by date range |
| Footer logo should invert on dark background | CSS `filter: invert(1) brightness(2)` on `logoName.png` |
| Mobile calendar — two months too wide | CSS pill toggle (`#month-toggle`) hides one month at a time |
| Suradnja page not in original brief | Added after content discussion, now in navbar |
