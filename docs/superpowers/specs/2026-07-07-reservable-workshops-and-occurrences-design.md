# Reservable workshops, multi-date occurrences, homepage layout, contact icons

Date: 2026-07-07

## Background

Codebase health scan while picking up this request surfaced two problems that shape the design:

1. **Dead code**: `Models/PinnedWorkshop.cs` + `PinnedWorkshops` table + `Pages/Admin/Pinned/*` is a second, unused implementation of "pinned workshop." The live mechanism is `Workshop.IsPinned` on the main `Workshop` table. Approved for removal as part of this change.
2. **No occurrence model**: each `Workshop` row *is* one date (`Date`, `StartTime`, `EndTime` live directly on it). Pinned workshops fake "no date" with `Date = 2099-01-01`. Adding a second date for the same workshop today means re-entering the entire form from scratch. This also causes a live bug: the homepage "Upcoming Workshops" strip does not filter out pinned workshops, so the 2099 placeholder can appear in the strip with a nonsense date/time.

This change fixes both, adds group-booking support for reservable workshops, and does two homepage layout fixes plus a small icon pass on contact/social links.

## Data model

**`Workshop`** (template — shared content, edited once) keeps:
`Id, Name, Description, BannerUrl, LogoUrl, InstagramPostUrl, HostName, HostInstagram, HostWebsite, Price, MaxParticipants, Slug, CreatedAt, IsArchived, Photos[]`

Renamed/added:
- `IsPinned` → **`IsReservable`** (rename, not just a label change — this is the same pass touching every reference to the field, so it's renamed at the source for clarity: a "reservable" workshop is one you book as a group, with no fixed date).
- New: `BookingType` (string: `"email"` or `"webpage"`) and `BookingValue` (string: the email address or URL). Only meaningful when `IsReservable == true`. Drive a single "Book" button — the button label never changes, only its destination (`mailto:` vs a link).

Removed from `Workshop` (move to occurrences): `Date`, `StartTime`, `EndTime`, `EntrioUrl`.

**`WorkshopOccurrence`** (new table — one row per date):
`Id, WorkshopId (FK), Date, StartTime, EndTime?, EntrioUrl?, CreatedAt`

- Reservable workshops have **zero** occurrences (they're dateless by definition — a group books the workshop itself, not a specific session).
- `Price`/`MaxParticipants` stay on `Workshop`, shared across all its dates — not asked to vary per date, so no override field is added (YAGNI).
- "Upcoming" (for a workshop) = not archived and has ≥1 occurrence with `Date >= Today`.
- "Archived" applies to the whole workshop and all its dates at once (confirmed) — no per-occurrence archive flag.

Deleted entirely: `Models/PinnedWorkshop.cs`, its `DbSet`, its `CREATE TABLE`/seed block in `Program.cs`, `Pages/Admin/Pinned/`.

## Migration approach

This project doesn't use `dotnet ef` migrations in practice — schema evolution happens via raw SQL guarded by try/catch in `Program.cs` (see the `IsArchived`/`IsPinned` column additions already there). This change follows the same convention:

1. `CREATE TABLE IF NOT EXISTS WorkshopOccurrences (...)`.
2. One-time backfill (guarded by "table is empty"): for every existing non-pinned `Workshop` row, insert one `WorkshopOccurrence` copying its `Date`/`StartTime`/`EndTime`/`EntrioUrl`.
3. The now-unused `Date`/`StartTime`/`EndTime`/`EntrioUrl`/`IsPinned` columns stay in the underlying SQLite table (harmless, unmapped) since the C# model simply stops declaring them — additive-only, nothing is dropped, matching existing convention.
4. Rename is handled by adding a new `IsReservable` column (copied from the old `IsPinned` value during the same backfill) rather than renaming the physical column — SQLite `ALTER ... RENAME COLUMN` works but the codebase's existing pattern never renames, only adds, so this stays consistent.
5. The seeded pinned/reservable "Rođendanska radionica" workshop's fake `Date = 2099-01-01` hack goes away entirely; it seeds with `IsReservable = true`, `BookingType = "webpage"`, `BookingValue = "/suradnja#upit"` (preserves current behavior until the owners configure a real one).

## Admin UX

- **Workshop edit form**: loses Date/Time/Entrio fields from the main form. Gains an inline "Termini" section: a small table of existing occurrences (date, time, ticket link, per-row edit/delete) plus a "+ Novi datum" add-row form at the bottom of the same page — no extra navigation.
- Creating a new workshop still asks for one date up front (becomes its first occurrence).
- **Reservable workshop edit form**: the "Pinned event" checkbox becomes "Reservable" (checkbox label + admin tab + empty-state text, per approved wording). Checking it hides the date/time fields (as today) and shows a Book method choice (Email / Webpage) + the corresponding value field, instead of the date/time inputs.
- Admin listing (`/admin`): "Upcoming" row shows the next occurrence's date/time, plus `· +N termina` if the workshop has more than one upcoming date. "Pinned" tab and button labels become "Reservable".

## Public site

- **`/workshops` listing**: one card per workshop (not per date). Regular workshops show their next upcoming date, or "Više termina" if more than one upcoming. Reservable workshops show the "Book" button directly on the card (driven by `BookingType`/`BookingValue`), replacing today's hardcoded link to `/suradnja#upit`.
- **`/workshops/{slug}` detail**: shared content (banner, logo, description, host, photos) shown once. Regular workshops get a table of all upcoming occurrences, each with its own date/time and ticket/register action. Reservable workshops show the single Book button instead of any date/ticket UI — this also fixes the bug where visiting a reservable workshop's detail page directly currently shows the fake 2099-01-01 date and a nonsensical "Add to Google Calendar" / ticket button.
- `Helpers/GoogleCalendarHelper.BuildAddToCalendarUrl` changes signature to take a `WorkshopOccurrence` (date/time is no longer on `Workshop`).

## Homepage (`Pages/Index.cshtml`)

- **Newsletter section** moves from its current position (between the Calendar and Mood Split sections) to directly before the Instagram CTA section — i.e. after the Quad Photo Grid, right before "Follow Us" / `@workshop.zagreb`. New section order: Hero → CTA → Photo Strip → Upcoming Workshops → Calendar → Mood Split → Quad Grid → **Newsletter** → Instagram CTA.
- **"Upcoming Workshops" strip**: currently queries `Date >= Today`, takes 8, with no month cap and no reservable-workshop exclusion (this is the live bug — a reservable workshop can appear here with its placeholder date). Fixed to: regular workshops with an occurrence in the next 2 months (chronological, capped at 8 — same limit as today), followed by the active reservable workshops appended at the end of the same strip.
- Calendar section's date-keyed dictionary (`WorkshopsByDate`) now joins through `WorkshopOccurrence` instead of reading `Workshop.Date` directly. Reservable workshops naturally never appear on the calendar (no occurrences to join against) — no extra filter needed.

## Contact/social icons

Icon-only (no visible text), inline SVG matching the site's existing zero-dependency icon pattern (the calendar/clock/people glyphs already inline in `Workshops/Detail.cshtml`) — no icon font, no npm, per CLAUDE.md.

Scope (confirmed):
- Site footer (`Pages/Shared/_Layout.cshtml`): Instagram link + email `mailto:` link → icon-only.
- About page contact rows (`Pages/About.cshtml`): same two links → icon-only.
- Workshop detail sidebar host links (`Pages/Workshops/Detail.cshtml`): the small "Instagram ↗" / "Website ↗" links next to the host name → icon-only.

Out of scope (left as text, deliberate marketing CTAs): "Open Instagram ↗" homepage CTA button, "View on Instagram ↗" / "Register via Instagram" workshop detail buttons, the `/workshops` listing empty-state Instagram link.

## Files touched (summary)

- `Models/Workshop.cs`, new `Models/WorkshopOccurrence.cs`, delete `Models/PinnedWorkshop.cs`
- `Data/AppDbContext.cs`, `Program.cs`
- `Helpers/GoogleCalendarHelper.cs`
- `Pages/Admin/Workshops/Edit.cshtml(.cs)`, `Pages/Admin/Index.cshtml(.cs)`
- Delete `Pages/Admin/Pinned/`
- `Pages/Workshops/Index.cshtml(.cs)`, `Pages/Workshops/Detail.cshtml(.cs)`
- `Pages/Index.cshtml(.cs)`
- `Pages/Shared/_Layout.cshtml`, `Pages/About.cshtml`

## Out of scope / explicitly not doing

- Per-occurrence pricing or capacity overrides.
- Per-occurrence archiving (archiving is whole-workshop, per prior decision).
- Real `dotnet ef` migrations (this app doesn't use them; staying consistent with the raw-SQL convention already in place).
- Converting the big Instagram CTA buttons to icons.
