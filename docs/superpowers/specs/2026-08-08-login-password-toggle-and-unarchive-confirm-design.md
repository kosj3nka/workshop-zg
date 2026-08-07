# Password visibility toggle + ask-before-send on unarchive

Date: 2026-08-08

## Background

Two small gaps in the admin panel:

1. The login password field (`Pages/Admin/Login.cshtml`) has no way to reveal what was typed — plain `<input type="password">`, no toggle.
2. Archive/unarchive already exists and works (`Workshop.IsArchived`, `OnPostArchiveAsync`/`OnPostUnarchiveAsync` in `Pages/Admin/Workshops/Edit.cshtml.cs`, plus the auto-revival path in `NotifyForOccurrenceChangeAsync` when an archived workshop gains a future date). Both ways a workshop comes back from archive currently email every subscriber automatically, with no confirmation step. This changes that: unarchiving should always happen, but sending the email becomes something the admin is asked about first.

Neither change touches the `Workshop`/`Subscriber` schema or adds new DB tables.

## Part 1 — Password visibility toggle

- `Pages/Admin/Login.cshtml`: wrap the password `<input>` in a `<div class="password-field-wrap">`, add a `<button type="button" class="password-toggle-btn" id="pw-toggle">` positioned inside the field, containing an inline "eye" SVG (open-eye by default, matching the stroke style of the existing delete-icon SVGs elsewhere in the admin panel).
- Inline `<script>` at the bottom of the page (same pattern as `toggleReservable()` in `Edit.cshtml`): on click, flips the input's `type` between `password`/`text` and swaps the SVG between an open-eye and an eye-with-slash icon. No library, no build step.
- `wwwroot/css/site.css`: two new rules — `.password-field-wrap { position: relative; }` and `.password-toggle-btn { position: absolute; right: 12px; top: 50%; transform: translateY(-50%); background: none; border: none; cursor: pointer; opacity: 0.5; padding: 4px; }` (`:hover` bumps opacity). The existing `.form-control` right padding needs to grow slightly (e.g. `padding-right: 44px` only on this field) so typed text doesn't run under the button.
- No server-side change — `LoginModel.OnPostAsync` still just reads `Request.Form["Password"]`/binds `Password`, unaffected by the input's `type` at submit time.

## Part 2 — Ask before sending the unarchive email

Both paths decouple **unarchiving** (always happens once the existing eligibility check passes) from **sending the announcement email** (now opt-in per action via a native `confirm()` dialog, matching the existing `onclick="return confirm(...)"` pattern used for delete buttons in `Edit.cshtml`). Declining the email never blocks the unarchive — Cancel only skips the send.

### Path A — explicit "Vrati iz arhive" button

- `Edit.cshtml`, inside the archived-workshop banner's unarchive `<form>`:
  - Add `<input type="hidden" name="sendEmail" id="unarchive-send-email" value="true" />`.
  - Button's `onclick` becomes: `document.getElementById('unarchive-send-email').value = confirm('Poslati email pretplatnicima da je radionica ponovno dostupna?'); return true;` — always lets the form submit; only overwrites the hidden value first.
- `OnPostUnarchiveAsync(int id, bool sendEmail = true)`: unchanged eligibility check (`canUnarchive`) and unchanged `workshop.IsArchived = false; await _db.SaveChangesAsync();`. After that:
  - `sendEmail == true`: send as today (`SendWorkshopAnnouncementAsync` + `SetEmailResultFlash`).
  - `sendEmail == false`: skip the send entirely; set `TempData["FlashType"] = "success"; TempData["Flash"] = "Radionica je vraćena iz arhive.";` so the admin still gets confirmation the state changed.

### Path B — automatic revival via adding/editing an occurrence date

Only wired up when `Model.IsArchivedWorkshop` is true — for a live workshop, the "Spremi"/"+ Novi datum" buttons and their date-change emails are untouched (out of scope; this feature is specifically about the archive-revival email, not live-workshop date-change emails).

- `Edit.cshtml`, only inside `@if (Model.IsArchivedWorkshop)`: each per-occurrence `occ-edit-@occ.Id` form and the `occ-add` form gain a hidden `sendEmail` input (default `"true"`), and their submit buttons ("Spremi", "+ Novi datum") gain the same `confirm()`-then-set-hidden-field `onclick`, with the message: `"Ako ovaj datum vrati radionicu iz arhive, poslati email pretplatnicima?"`. This fires on every save while the workshop is archived, regardless of whether the entered date actually ends up in the future — the wording is phrased as a conditional so it isn't misleading when it doesn't (the server-side `sendEmail` flag is simply unused whenever no revival happens, since only the archive-revival branch reads it). When the workshop is not archived, none of this markup changes.
- `OnPostAddOccurrenceAsync` / `OnPostUpdateOccurrenceAsync` gain a `bool sendEmail = true` parameter, threaded through to `NotifyForOccurrenceChangeAsync(workshopId, occurrence, liveKicker, hadFutureOccurrenceBefore, sendEmail)`.
- Inside `NotifyForOccurrenceChangeAsync`, `sendEmail` is consulted **only** in the archive-revival branch (`workshop.IsArchived && !workshop.IsReservable && !hadFutureOccurrenceBefore && hasFuture`):
  - `true` (default): unchanged — unarchive, send, flash as today.
  - `false`: still set `workshop.IsArchived = false; await _db.SaveChangesAsync();`, but skip the send and set the same no-email success flash as Path A ("Radionica je vraćena iz arhive.").
  - The `else if (!workshop.IsArchived)` branch (regular live-workshop date-change announce) ignores the parameter entirely and always sends, exactly as it does today.

Reservable workshops have no occurrences, so they only ever revive via Path A — Path B's changes don't affect them.

## Out of scope

- Live-workshop date-change emails (adding/editing an occurrence on a workshop that is *not* archived) — these keep sending fully automatically, unchanged. Only the archive-revival email becomes optional.
- New-workshop creation's existing "Pošalji email svim pretplatnicima" opt-in checkbox — already a confirmation step, untouched.
- Any change to `EmailService`, `Subscriber`, or the DB schema.

## Testing

Manual pass through the admin panel (no automated test suite in this project), `dotnet build` after each change:

1. **Login page**: load `/admin/login`, type a password, click the eye icon → text becomes visible, icon swaps to eye-slash; click again → masked again, icon swaps back. Submit still logs in correctly regardless of toggle state.
2. **Path A, accept**: archive a workshop with a future date, reopen its edit page, click "Vrati iz arhive", accept the confirm dialog → workshop un-archived, success flash shows a sent count (or nothing if there are zero active subscribers).
3. **Path A, decline**: repeat, but click Cancel on the confirm dialog → workshop still un-archived (confirm in DB / admin listing), flash reads "Radionica je vraćena iz arhive." with no send count, no email actually sent.
4. **Path B, accept**: archive a dated workshop whose only occurrence is in the past (banner shows the "add a future date" message, no button). Edit that occurrence to a future date, accept the confirm dialog on save → workshop auto-unarchives, announcement sent, flash shows a sent count.
5. **Path B, decline**: repeat step 4 but click Cancel → workshop still auto-unarchives (confirm in admin listing it now appears under "Upcoming"), but no email sent, flash reads "Radionica je vraćena iz arhive."
6. **Live workshop unaffected**: on a non-archived workshop, add a new date or edit an existing one → email still sends automatically with no confirm dialog, exactly as before.
7. **Reservable workshop**: archive a reservable workshop, unarchive via the button, both accept and decline the email prompt → confirm both cases behave like Path A (button-only revival, no occurrence forms involved).
