# Automatic subscriber emails for date changes and unarchiving

Date: 2026-08-07

## Background

New-workshop announcements already exist and work today: the admin ticks "Pošalji email svim pretplatnicima" on the *new workshop* form (`Pages/Admin/Workshops/Edit.cshtml.cs`), and `EmailService.SendWorkshopAnnouncementAsync` fires an email to every active `Subscriber`. Two things are missing:

1. Editing an existing occurrence's date/time, or adding a new date to a workshop that's already live, sends nothing.
2. Bringing a workshop back from the archive sends nothing, and nothing stops an admin from unarchiving a dated workshop whose only occurrences are in the past — it would go live showing a stale date.

Reviewing the full email path while scoping this also surfaced reliability gaps (fire-and-forget sends with no visibility, one SMTP connection per recipient, unvalidated subscribe input) that are folded into this change since they directly affect whether the new automatic sends can be trusted.

## Trigger 1 — date/time changed or a new date added (workshop already live)

- `OnPostAddOccurrenceAsync` (`Pages/Admin/Workshops/Edit.cshtml.cs`): after saving, if the parent workshop is not archived, send an announcement. Subject: `"Novi termin: {workshop.Name} — Workshop Zagreb"`. Always sends — a new date is inherently announce-worthy.
- `OnPostUpdateOccurrenceAsync`: capture the occurrence's existing `Date`/`StartTime`/`EndTime` *before* applying the incoming values. After saving, send only if at least one of those three changed. Editing just `EntrioUrl` sends nothing. Subject: `"Promjena termina: {workshop.Name} — Workshop Zagreb"`.
- Both are fully automatic — no admin checkbox, unlike the new-workshop flow which keeps its existing opt-in checkbox and editable subject untouched.
- Recipients: all `Subscriber` rows where `IsActive` (same query as today, `ActiveSubscribersAsync()`).

## Trigger 2 — unarchiving

- `WorkshopEditModel` gains a computed property `CanUnarchiveDirectly` = `workshop.IsReservable || Occurrences.Any(o => o.Date >= Today)`, set during `OnGetAsync`.
- Edit page archived-workshop banner (`Pages/Admin/Workshops/Edit.cshtml`, currently always shows a "Vrati iz arhive" button):
  - `CanUnarchiveDirectly == true`: unchanged — shows the button. Clicking it flips `IsArchived = false` immediately and sends the announcement (subject `"Radionica je ponovno dostupna: {Name} — Workshop Zagreb"`).
  - `CanUnarchiveDirectly == false` (non-reservable, no future date): button is replaced with inline text telling the admin to add/edit a date to a future one before the workshop can go live again. `OnPostUnarchiveAsync` also re-checks this server-side and no-ops if called directly while the condition is false (defense in depth — the button shouldn't be reachable, but a direct POST could still hit the handler).
- While a workshop is in this blocked state, saving an occurrence (`OnPostAddOccurrenceAsync` or `OnPostUpdateOccurrenceAsync`) that results in the workshop having a future occurrence automatically clears `IsArchived` and fires the same "back from archive" announcement — no second click of "Vrati iz arhive" needed. This reuses the Trigger 1 send path with the unarchive subject/kicker instead of the date-changed one, and only one email goes out (not both).
- Reservable workshops have zero occurrences, so their "back from archive" email can't show a date/time table. `SendWorkshopAnnouncementAsync`'s `occurrence` parameter becomes nullable: when `null`, the date/time rows are omitted and a booking CTA is shown instead, driven by `workshop.BookingType`/`BookingValue` — same `mailto:` vs. direct-link logic already used on `Pages/Workshops/Detail.cshtml:161-164`.

## Reliability changes to `EmailService`

- `SendWorkshopAnnouncementAsync` signature changes from `Task` to `Task<EmailBatchResult>`, where:
  ```csharp
  public record EmailBatchResult(int Sent, int Failed, bool SmtpConfigured);
  ```
- Internally it now opens **one** authenticated `SmtpClient` connection for the whole subscriber batch (connect once → send each message over it → disconnect once), instead of reconnecting per subscriber as it does today. Faster, and less likely to trip SMTP provider rate limits as the list grows.
- SMTP config completeness (`Host`/`From`/`Password` present) is checked once up front. If missing, returns `EmailBatchResult(0, subscribers.Count, false)` immediately without attempting a connection.
- Per-recipient send failures are still caught and logged individually (existing behavior), rolled into `Failed`.
- `SendConfirmationAsync` and `SendInquiryAsync` are single-send methods and are unaffected — they keep using the existing `SendOneAsync` (connect/send/disconnect once, since there's only ever one recipient).

## Admin-visible send feedback

- All three call sites (`OnPostAddOccurrenceAsync`, `OnPostUpdateOccurrenceAsync`, `OnPostUnarchiveAsync`, and the existing new-workshop send) now `await` the result instead of firing-and-forgetting it, and set:
  - `TempData["Flash"]` — the message text
  - `TempData["FlashType"]` — `"success"`, `"warning"`, or `"error"`
- Mapping: `!SmtpConfigured` → error, `"Slanje nije uspjelo — provjeri email postavke."`; `Failed > 0` → warning, `"Email poslan na {Sent} pretplatnika, {Failed} nije uspjelo."`; else if `Sent > 0` → success, `"Email poslan na {Sent} pretplatnika."`; `Sent == 0 && Failed == 0` (no active subscribers) → no flash.
- `Pages/Shared/_AdminLayout.cshtml` renders the flash (if `TempData["Flash"]` is set) once, right above `@RenderBody()`, so it surfaces regardless of which admin page a handler redirects to. Reuses the existing `.login-error` CSS class with inline color overrides per `FlashType`, matching the pattern already used for the amber archived-workshop banner — no new CSS classes.
- This blocks the HTTP response until the batch send completes (no background job/queue — out of scope, see below). Acceptable given the subscriber list is small (café newsletter scale) and the single-connection change already makes batches faster.

## Subscribe validation

- `Pages/Api/Subscribe.cshtml.cs`: validate `payload.Email` with `System.Net.Mail.MailAddress.TryCreate` before the existing-subscriber lookup/insert. Invalid input returns `{ ok: false }` instead of storing/emailing an unparseable address.

## Out of scope (flagged during review, not part of this change)

- True double opt-in and rate-limiting on `/api/subscribe` — today's "confirmation" email is sent immediately with no verification click required (`ConfirmedAt` is set on POST, not on a confirm-link visit). Pre-existing behavior, bigger change than this feature warrants.
- Double-submit protection (e.g. a fast double-click on "Uredi termin" firing two sends) — pre-existing exposure across the admin panel, not introduced by this change.
- Migrating `workshop.db` (SQLite) to Azure SQL — CLAUDE.md lists Azure SQL as the target, but `Program.cs` still wires up `UseSqlite` with a comment marking the switch as future work. Unrelated to this feature; the `Subscriber` table lives wherever `WorkshopDb` currently points.

## Testing

- Manual pass through the admin panel (no automated test suite in this project):
  1. New workshop with notify checkbox on → confirm existing opt-in flow still works, flash message shows a sent count.
  2. Edit an existing occurrence's date → email fires, flash shows count. Edit only its `EntrioUrl` → no email, no flash.
  3. Add a new date to a live workshop → email fires.
  4. Archive a dated workshop, unarchive with a future date already present → button visible, click sends email immediately.
  5. Archive a dated workshop whose only date is in the past → button replaced by the "add a future date" message; edit that date into the future → workshop auto-unarchives and the "back from archive" email fires, `IsArchived` confirmed false in the DB.
  6. Archive and unarchive a reservable workshop → email sends with the booking CTA instead of a date/time table.
  7. Temporarily blank out `Email:Smtp:Password` → trigger a send → confirm the error flash appears instead of a silent no-op.
  8. POST a non-email string to `/api/subscribe` → confirm `{ok:false}` and no DB row/email.
