# Tasks — Remaining Work

Priority: 🔴 Blocker · 🟡 Important · 🟢 Nice-to-have

---

## Immediate — Needs Owner Input Before Building

- 🔴 **Collect all images** from owners (full list in IMAGES.md). Nothing renders correctly without them.
- 🔴 **Menu content** — what coffee/food/drinks do they serve? Needed for `Meni` page.
- 🔴 **FAQ content** — 6–10 questions to draft with owners.
- 🟡 **Real workshop data** — at least 3 real workshop names, descriptions, prices, icons for seeding.
- 🟡 **Booking method** — confirm: Eventbrite link? Google Form? WhatsApp? One URL per occurrence.
- 🟡 **Map embed** — exact address and Google Maps embed URL for O nama and footer.

---

## Backend Tasks

### Database
- 🔴 Create `AppDbContext.cs` with `DbSet<Workshop>` and `DbSet<WorkshopOccurrence>`
- 🔴 Run `dotnet ef migrations add InitialCreate` and `dotnet ef database update`
- 🟡 Seed 3–5 workshops + occurrences for dev testing
- 🟡 Add `Gallery` table if gallery images are stored in DB (vs. just Blob Storage folder)

### Admin Panel (owner-facing CRUD)
All admin routes must be behind `[Authorize]`.

- 🔴 **Login page** (`/admin/login`) — Identity login form, remember-me 30 days
- 🔴 **Workshop Create** form fields:
  - Name (text)
  - Short description (textarea, ~100 chars)
  - Full description (textarea or simple rich text)
  - Price (decimal, HRK/EUR)
  - Duration in minutes (number)
  - Icon image (file upload → Blob Storage)
  - Active toggle (show/hide on public site)
- 🔴 **Workshop Edit** — same form, pre-populated
- 🔴 **Workshop Delete** — confirmation step ("Are you sure?")
- 🔴 **Occurrence Create** — pick workshop from dropdown, date picker, time picker, max participants, booking URL
- 🔴 **Occurrence Edit / Delete**
- 🟡 **Dashboard** — table of upcoming occurrences (next 60 days), quick-edit links
- 🟡 **Gallery Upload** — owner uploads photo → goes to Blob → appears in Galerija

### Blob Storage
- 🔴 `BlobStorageService.cs`:
  ```csharp
  Task<string> UploadAsync(IFormFile file, string containerName);
  Task DeleteAsync(string fileName, string containerName);
  string GetUrl(string fileName, string containerName);
  ```
- 🔴 Add `"AzureBlobStorage": { "ConnectionString": "", "AccountName": "" }` to `appsettings.json`
- 🟡 Use Azure Key Vault for connection string in production (never commit secrets)

---

## Frontend Tasks

### Radionice (Workshops) Pages
- 🔴 **Workshop listing page** — grid of cards: icon, name, short description, price, "Saznaj više" button
- 🔴 **Workshop detail page** — full description, images, upcoming occurrences table with Book buttons
- 🟡 Workshop detail: breadcrumb nav (Home > Radionice > Linocut)

### Meni Page
- 🟡 Design: two-column layout (drinks left, food right) or tab-based on mobile
- 🟡 Mark allergens if relevant

### FAQ Page
- 🟡 Accordion component (CSS-only `<details>/<summary>` is fine, no JS needed)
- 🟡 6–10 items drafted with owners

### Galerija Page
- 🟡 CSS masonry grid (CSS `columns: 3` trick, no JS library)
- 🟡 Lightbox on image click — vanilla JS, ~30 lines, no library
- 🟡 Images fetched from Blob Storage via Gallery DB table or static folder

### Navbar
- 🟡 Add **Suradnja** link — was added as a page but may not be in navbar yet; confirm placement
- 🟡 Mobile hamburger menu — CSS-only or minimal JS toggle
- 🟢 Sticky navbar with scroll-triggered background opacity change

### General Polish
- 🟢 Page transition fade (CSS `@keyframes` on `<body>`)
- 🟢 Smooth scroll to anchor links
- 🟢 404 custom page

---

## Deployment Tasks

### Azure Setup
- 🔴 Create Resource Group: `workshop-zagreb-rg`
- 🔴 Create App Service Plan (B1, Linux)
- 🔴 Create Web App: `workshop-zagreb` (stack: .NET 8)
- 🔴 Create Azure SQL Server + Database (Standard S0)
- 🔴 Create Storage Account + Blob container `workshop-media` (public access: blob)
- 🔴 Set App Service environment variables:
  - `ConnectionStrings__DefaultConnection` → Azure SQL connection string
  - `AzureBlobStorage__ConnectionString` → Storage account connection string
  - `AdminPassword` → hashed or via Identity seed

### CI/CD
- 🟡 GitHub Actions workflow: `dotnet build` → `dotnet test` → `az webapp deploy`
- 🟡 Separate staging slot on App Service (test before swapping to production)

### Domain & HTTPS
- 🟡 Custom domain (e.g. `workshopzagreb.hr` or `workshop.zagreb`) — owners need to purchase
- 🟡 Managed certificate via App Service (free, auto-renews)

---

## Inquiry Forms (Suradnja page)

Both forms live on the existing `/suradnja` page — it's already the destination for people who want to collaborate or host. Add two clearly separated sections below the current content, letting visitors toggle between them (or just scroll to the right one).

### Private event reservation form
- 🟡 Fields: Name, Email, Phone, Date (date picker), Estimated guests (number), Message/details
- 🟡 On submit: send email to `hello@workshopzagreb.com` via the existing `EmailService` (Google Workspace SMTP)
- 🟡 Show inline success message; no redirect

### Host a workshop / event form
- 🟡 Fields: Name, Email, Organisation/brand (optional), Event type (dropdown: Radionica / Privatni event / Brand suradnja / Ostalo), Preferred dates (text), Message
- 🟡 Same email delivery as above
- 🟡 Show inline success message

### Implementation notes
- Add a `ContactModel` (Razor Page handler or API endpoint) — no DB needed, just fire-and-forget email
- Reuse `IEmailService.SendOneAsync` (make it internal or add `SendContactAsync`)
- Add honeypot hidden field for basic spam protection
- GDPR note under submit button (same pattern as newsletter popup)

---

## Future / Out of Scope for V1

- 🟢 **Online payments** — Stripe integration for paid bookings directly on site
- 🟢 **Email notifications** — owner gets email when someone books; participant gets confirmation
- 🟢 **Waitlist** — if occurrence is full, join waitlist
- 🟢 **Croatian/English toggle** — if international guests become significant
- 🟢 **Analytics** — add Plausible or Simple Analytics (privacy-first, no GDPR banner needed)
- 🟢 **Instagram feed embed** — live feed on homepage instead of static CTA strip
