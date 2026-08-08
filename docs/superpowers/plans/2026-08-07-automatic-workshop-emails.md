# Automatic Workshop Emails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically email subscribers when an existing workshop's date/time changes, a new date is added to a live workshop, or a workshop is brought back from the archive — plus make sends admin-visible instead of silent, reuse one SMTP connection per batch, and validate subscribe input.

**Architecture:** Extends the existing `EmailService`/`WorkshopEditModel` pair — no new services, models, or DB tables. `EmailService.SendWorkshopAnnouncementAsync` becomes batch-aware (one SMTP connection, returns a result record) and gains a nullable occurrence + kicker label. `WorkshopEditModel` gains a shared helper that decides whether an occurrence save should announce a plain date change or trigger an archive comeback, plus a flash-message helper that turns the result into a banner rendered by `_AdminLayout.cshtml`.

**Tech Stack:** ASP.NET Core 8 Razor Pages, EF Core (SQLite), MailKit/MimeKit for SMTP. No test project exists in this repo — verification is `dotnet build` per task plus a manual pass through the admin panel at the end (matches the project's existing testing approach).

## Global Constraints

- All UI strings are Croatian (per CLAUDE.md).
- No new NuGet packages, no JS build step, no Bootstrap/Tailwind (per CLAUDE.md).
- No new CSS classes — reuse the existing `.login-error` class with inline color overrides, matching how the archived-workshop banner already does it (per spec).
- No schema/migration changes — this feature needs none.
- Every automatic send is fire-and-await (not fire-and-forget) so its outcome can be shown to the admin — this intentionally makes the affected POST handlers block until the batch send finishes.

---

### Task 1: `EmailService` — batch-aware announcement send

**Files:**
- Modify: `Services/EmailService.cs:9-14` (interface), `Services/EmailService.cs:55-108` (method body)

**Interfaces:**
- Produces: `public record EmailBatchResult(int Sent, int Failed, bool SmtpConfigured)` in `WorkshopZagreb.Services`.
- Produces: `Task<EmailBatchResult> IEmailService.SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence? occurrence, IList<Subscriber> subscribers, string? subject = null, string kicker = "Nova radionica")` — `occurrence` is now nullable (reservable workshops have none); `kicker` is the small eyebrow label above the workshop name in the email (defaults to today's hardcoded "Nova radionica" text).

- [ ] **Step 1: Replace the interface declaration**

In `Services/EmailService.cs`, replace:
```csharp
public interface IEmailService
{
    Task SendConfirmationAsync(string toEmail, string unsubscribeToken);
    Task SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence occurrence, IList<Subscriber> subscribers, string? subject = null);
    Task SendInquiryAsync(InquiryInput input);
}
```
with:
```csharp
public interface IEmailService
{
    Task SendConfirmationAsync(string toEmail, string unsubscribeToken);
    Task<EmailBatchResult> SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence? occurrence, IList<Subscriber> subscribers, string? subject = null, string kicker = "Nova radionica");
    Task SendInquiryAsync(InquiryInput input);
}

public record EmailBatchResult(int Sent, int Failed, bool SmtpConfigured);
```

- [ ] **Step 2: Replace the method implementation**

Replace the entire `SendWorkshopAnnouncementAsync` method (currently lines 55-108, from `public async Task SendWorkshopAnnouncementAsync(...)` down to its closing `}` right before `public async Task SendInquiryAsync`) with:

```csharp
    public async Task<EmailBatchResult> SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence? occurrence, IList<Subscriber> subscribers, string? subject = null, string kicker = "Nova radionica")
    {
        if (!subscribers.Any()) return new EmailBatchResult(0, 0, true);

        var smtp = _config.GetSection("Email:Smtp");
        var host = smtp["Host"];
        var from = smtp["From"];
        var password = smtp["Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(password))
        {
            _log.LogWarning("Email:Smtp not fully configured (missing Host/From/Password) — skipping announcement batch");
            return new EmailBatchResult(0, subscribers.Count, false);
        }

        var price   = !string.IsNullOrEmpty(workshop.Price) ? workshop.Price : "Besplatno";
        var maxPax  = workshop.MaxParticipants.HasValue
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;width:100px;'>Mjesta</td><td style='font-weight:500;'>max {workshop.MaxParticipants}</td></tr>"
            : "";
        var hostRow = !string.IsNullOrEmpty(workshop.HostName)
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;'>Voditelj</td><td style='font-weight:500;'>{workshop.HostName}</td></tr>"
            : "";

        string dateRows;
        string actionBtn;
        if (occurrence != null)
        {
            var date    = occurrence.Date.ToString("dd. MM. yyyy");
            var time    = occurrence.StartTime.ToString(@"hh\:mm");
            var endTime = occurrence.EndTime.HasValue ? $" – {occurrence.EndTime.Value:hh\\:mm}" : "";
            dateRows = $"""
                <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;width:100px;">Datum</td><td style="font-weight:500;">{date}</td></tr>
                <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;">Vrijeme</td><td style="font-weight:500;">{time}{endTime}</td></tr>
                <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;">Cijena</td><td style="font-weight:500;">{price}</td></tr>
                {maxPax}
                {hostRow}
                """;
            actionBtn = !string.IsNullOrEmpty(occurrence.EntrioUrl)
                ? $"""<p style="margin:28px 0 8px;"><a href="{occurrence.EntrioUrl}" style="background:#c8a96e;color:#fff;padding:12px 32px;text-decoration:none;display:inline-block;font-size:0.9rem;font-weight:600;">Kupi ulaznicu</a></p>"""
                : "";
        }
        else
        {
            dateRows = $"""
                <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;width:100px;">Cijena</td><td style="font-weight:500;">{price}</td></tr>
                {maxPax}
                {hostRow}
                """;
            var bookHref = workshop.BookingType == "email" ? $"mailto:{workshop.BookingValue}" : (workshop.BookingValue ?? "/suradnja#upit");
            actionBtn = $"""<p style="margin:28px 0 8px;"><a href="{bookHref}" style="background:#c8a96e;color:#fff;padding:12px 32px;text-decoration:none;display:inline-block;font-size:0.9rem;font-weight:600;">Rezerviraj</a></p>""";
        }

        var calendarUrl = $"{SiteBase()}/#calendar";
        subject ??= $"Nova radionica: {workshop.Name} — Workshop Zagreb";

        using var smtpClient = new SmtpClient();
        try
        {
            await smtpClient.ConnectAsync(host, int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls);
            await smtpClient.AuthenticateAsync(smtp["Username"], password);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to connect/authenticate SMTP for announcement batch");
            return new EmailBatchResult(0, subscribers.Count, false);
        }

        int sent = 0, failed = 0;
        foreach (var sub in subscribers)
        {
            var unsub = UnsubscribeUrl(sub.Token);
            var html = $"""
                <div style="font-family:Inter,Arial,sans-serif;max-width:540px;margin:0 auto;color:#1a1a1a;padding:32px 0;">
                  <p style="font-size:0.72rem;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;color:#c8a96e;margin-bottom:8px;">{kicker}</p>
                  <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:1.9rem;line-height:1.2;margin:0 0 24px;">{workshop.Name}</h1>

                  <table style="width:100%;border-collapse:collapse;margin-bottom:28px;">
                    {dateRows}
                  </table>

                  <p style="line-height:1.75;margin-bottom:28px;">{workshop.Description}</p>

                  {actionBtn}
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

            try
            {
                var msg = new MimeMessage();
                msg.From.Add(MailboxAddress.Parse(from));
                msg.To.Add(MailboxAddress.Parse(sub.Email));
                msg.Subject = subject;
                msg.Body = new TextPart("html") { Text = html };
                await smtpClient.SendAsync(msg);
                sent++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send announcement to {To}", sub.Email);
                failed++;
            }
        }

        await smtpClient.DisconnectAsync(true);
        return new EmailBatchResult(sent, failed, true);
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded` — existing callers of `SendWorkshopAnnouncementAsync` in `Pages/Admin/Workshops/Edit.cshtml.cs` still compile unchanged, because the call there (`_ = _email.SendWorkshopAnnouncementAsync(workshop, firstOccurrence, newSubs, subject)`) is source-compatible with both the new nullable `occurrence` parameter and the new `kicker` default — a `Task<T>` discards via `_ =` exactly like a `Task` did.

- [ ] **Step 4: Commit**

```bash
git add Services/EmailService.cs
git commit -m "Make SendWorkshopAnnouncementAsync batch-aware with a single SMTP connection"
```

---

### Task 2: Admin flash-message helper + wire into the existing new-workshop send

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs:163-170` (new-workshop notify block), and add a private helper near `ActiveSubscribersAsync()` (currently `Pages/Admin/Workshops/Edit.cshtml.cs:317-320`)

**Interfaces:**
- Consumes: `EmailBatchResult` from Task 1.
- Produces: `private void SetEmailResultFlash(EmailBatchResult result)` — sets `TempData["Flash"]` (string) and `TempData["FlashType"]` (`"success"`/`"warning"`/`"error"`), or sets neither if there was nothing to report (no active subscribers). Later tasks call this after every automatic send.

- [ ] **Step 1: Add the helper**

In `Pages/Admin/Workshops/Edit.cshtml.cs`, add this method next to `ActiveSubscribersAsync()`:

```csharp
    private void SetEmailResultFlash(EmailBatchResult result)
    {
        if (!result.SmtpConfigured)
        {
            TempData["FlashType"] = "error";
            TempData["Flash"] = "Slanje nije uspjelo — provjeri email postavke.";
        }
        else if (result.Failed > 0)
        {
            TempData["FlashType"] = "warning";
            TempData["Flash"] = $"Email poslan na {result.Sent} pretplatnika, {result.Failed} nije uspjelo.";
        }
        else if (result.Sent > 0)
        {
            TempData["FlashType"] = "success";
            TempData["Flash"] = $"Email poslan na {result.Sent} pretplatnika.";
        }
    }
```

- [ ] **Step 2: Wire it into the existing new-workshop notify block**

Replace:
```csharp
            if (Input.NotifySubscribers && firstOccurrence != null)
            {
                var subject = string.IsNullOrWhiteSpace(Input.EmailSubject)
                    ? $"Nova radionica! - {workshop.Name}"
                    : Input.EmailSubject;
                var newSubs = await ActiveSubscribersAsync();
                _ = _email.SendWorkshopAnnouncementAsync(workshop, firstOccurrence, newSubs, subject);
            }
```
with:
```csharp
            if (Input.NotifySubscribers && firstOccurrence != null)
            {
                var subject = string.IsNullOrWhiteSpace(Input.EmailSubject)
                    ? $"Nova radionica! - {workshop.Name}"
                    : Input.EmailSubject;
                var newSubs = await ActiveSubscribersAsync();
                var result = await _email.SendWorkshopAnnouncementAsync(workshop, firstOccurrence, newSubs, subject);
                SetEmailResultFlash(result);
            }
```

- [ ] **Step 3: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add Pages/Admin/Workshops/Edit.cshtml.cs
git commit -m "Await new-workshop announcement send and surface the result via TempData flash"
```

---

### Task 3: `CanUnarchiveDirectly` + archived-workshop banner

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs:41` (property), `Pages/Admin/Workshops/Edit.cshtml.cs:84` (set it in `OnGetAsync`)
- Modify: `Pages/Admin/Workshops/Edit.cshtml:15-23` (banner markup)

**Interfaces:**
- Produces: `public bool CanUnarchiveDirectly { get; set; }` on `WorkshopEditModel`, `true` when the workshop is reservable or already has an occurrence with `Date >= Today`. Read by the Razor page in this task, and by `OnPostUnarchiveAsync` in Task 4 (recomputed there independently against fresh DB state — this property is only for the GET-rendered banner).

- [ ] **Step 1: Add the property**

In `Pages/Admin/Workshops/Edit.cshtml.cs`, next to (after) this existing line:
```csharp
    public bool IsArchivedWorkshop { get; set; }
```
add:
```csharp
    public bool CanUnarchiveDirectly { get; set; }
```

- [ ] **Step 2: Compute it in `OnGetAsync`**

Find this line inside `OnGetAsync` (in the `if (!IsNew && id.HasValue)` branch):
```csharp
            IsArchivedWorkshop = workshop.IsArchived;
```
Replace with:
```csharp
            IsArchivedWorkshop = workshop.IsArchived;
            CanUnarchiveDirectly = workshop.IsReservable || workshop.Occurrences.Any(o => o.Date >= DateTime.Today);
```

- [ ] **Step 3: Update the banner markup**

In `Pages/Admin/Workshops/Edit.cshtml`, replace:
```html
@if (Model.IsArchivedWorkshop)
{
    <div class="login-error" style="background:#FEF9EC; color:#92610A; border-left:4px solid #F6C84B; margin-bottom:24px;">
        Ova radionica je arhivirana.
        <form method="post" asp-page-handler="Unarchive" asp-route-id="@Model.Input.Id" style="display:inline;">
            <button type="submit" class="btn btn-outline btn-sm" style="margin-left:8px;">Vrati iz arhive</button>
        </form>
    </div>
}
```
with:
```html
@if (Model.IsArchivedWorkshop)
{
    <div class="login-error" style="background:#FEF9EC; color:#92610A; border-left:4px solid #F6C84B; margin-bottom:24px;">
        Ova radionica je arhivirana.
        @if (Model.CanUnarchiveDirectly)
        {
            <form method="post" asp-page-handler="Unarchive" asp-route-id="@Model.Input.Id" style="display:inline;">
                <button type="submit" class="btn btn-outline btn-sm" style="margin-left:8px;">Vrati iz arhive</button>
            </form>
        }
        else
        {
            <span>Dodaj budući datum ispod kako bi radionica postala ponovno vidljiva.</span>
        }
    </div>
}
```

- [ ] **Step 4: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add Pages/Admin/Workshops/Edit.cshtml.cs Pages/Admin/Workshops/Edit.cshtml
git commit -m "Hide the direct unarchive button when a workshop has no future date"
```

---

### Task 4: `OnPostUnarchiveAsync` — block/send logic

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs:272-278` (`OnPostUnarchiveAsync`)

**Interfaces:**
- Consumes: `SetEmailResultFlash` (Task 2), `EmailBatchResult`/`SendWorkshopAnnouncementAsync` (Task 1).

- [ ] **Step 1: Replace the handler**

Replace:
```csharp
    public async Task<IActionResult> OnPostUnarchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = false; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { action = "edit", id });
    }
```
with:
```csharp
    public async Task<IActionResult> OnPostUnarchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var workshop = await _db.Workshops.Include(w => w.Occurrences).FirstOrDefaultAsync(w => w.Id == id);
        if (workshop == null) return RedirectToPage("/Admin/Index");

        var futureOccurrence = workshop.Occurrences.Where(o => o.Date >= DateTime.Today).OrderBy(o => o.Date).FirstOrDefault();
        var canUnarchive = workshop.IsReservable || futureOccurrence != null;

        if (!canUnarchive)
        {
            TempData["FlashType"] = "warning";
            TempData["Flash"] = "Radionica ima samo prošle datume — dodaj budući datum prije nego je vratiš iz arhive.";
            return RedirectToPage(new { action = "edit", id });
        }

        workshop.IsArchived = false;
        await _db.SaveChangesAsync();

        var subject = $"Radionica je ponovno dostupna: {workshop.Name} — Workshop Zagreb";
        var subs = await ActiveSubscribersAsync();
        var result = await _email.SendWorkshopAnnouncementAsync(workshop, futureOccurrence, subs, subject, "Ponovno dostupno");
        SetEmailResultFlash(result);

        return RedirectToPage(new { action = "edit", id });
    }
```

This covers both branches from the spec: `canUnarchive` true (reservable, or already has a future date) unarchives immediately and announces with `futureOccurrence` (which is `null` for reservable workshops — Task 1's nullable-occurrence handling renders the booking CTA instead of a date/time table); `canUnarchive` false leaves the workshop archived and flashes a warning instead (defense-in-depth — the edit page's banner from Task 3 shouldn't offer this button in that state, but a direct POST could still hit the handler).

- [ ] **Step 2: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add Pages/Admin/Workshops/Edit.cshtml.cs
git commit -m "Block direct unarchive without a future date, announce on successful unarchive"
```

---

### Task 5: Announce on occurrence add/update, with the archive-comeback path

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs:210-228` (`OnPostAddOccurrenceAsync`), `Pages/Admin/Workshops/Edit.cshtml.cs:230-248` (`OnPostUpdateOccurrenceAsync`), and add a new private helper near them.

**Interfaces:**
- Consumes: `SetEmailResultFlash` (Task 2), `SendWorkshopAnnouncementAsync`/`EmailBatchResult` (Task 1).
- Produces: `private async Task NotifyForOccurrenceChangeAsync(int workshopId, WorkshopOccurrence occurrence, string liveKicker)` — shared by both handlers below.

- [ ] **Step 1: Add the shared helper**

Add this method to `WorkshopEditModel` (e.g. directly above `SetEmailResultFlash`):

```csharp
    // Called after an occurrence is added or its date/time actually changed. If the workshop
    // was archived and this occurrence gives it a future date, brings it back live and sends
    // the "back from archive" announcement instead of a plain date-change one.
    private async Task NotifyForOccurrenceChangeAsync(int workshopId, WorkshopOccurrence occurrence, string liveKicker)
    {
        var workshop = await _db.Workshops.FindAsync(workshopId);
        if (workshop == null) return;

        var hasFuture = await _db.WorkshopOccurrences
            .AnyAsync(o => o.WorkshopId == workshopId && o.Date >= DateTime.Today);

        string subject;
        string kicker;

        if (workshop.IsArchived && !workshop.IsReservable && hasFuture)
        {
            workshop.IsArchived = false;
            await _db.SaveChangesAsync();
            subject = $"Radionica je ponovno dostupna: {workshop.Name} — Workshop Zagreb";
            kicker = "Ponovno dostupno";
        }
        else if (!workshop.IsArchived)
        {
            subject = $"{liveKicker}: {workshop.Name} — Workshop Zagreb";
            kicker = liveKicker;
        }
        else
        {
            return; // still archived, still no future date — nothing to announce
        }

        var subs = await ActiveSubscribersAsync();
        var result = await _email.SendWorkshopAnnouncementAsync(workshop, occurrence, subs, subject, kicker);
        SetEmailResultFlash(result);
    }
```

- [ ] **Step 2: Wire it into `OnPostAddOccurrenceAsync`**

Replace:
```csharp
    public async Task<IActionResult> OnPostAddOccurrenceAsync(int workshopId)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (NewOccurrence.Date == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

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
```
with:
```csharp
    public async Task<IActionResult> OnPostAddOccurrenceAsync(int workshopId)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (NewOccurrence.Date == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

        var newOccurrence = new WorkshopOccurrence
        {
            WorkshopId = workshopId,
            Date = NewOccurrence.Date,
            StartTime = NewOccurrence.StartTime,
            EndTime = NewOccurrence.EndTime,
            EntrioUrl = NewOccurrence.EntrioUrl,
        };
        _db.WorkshopOccurrences.Add(newOccurrence);
        await _db.SaveChangesAsync();

        await NotifyForOccurrenceChangeAsync(workshopId, newOccurrence, "Novi termin");

        return RedirectToPage(new { action = "edit", id = workshopId });
    }
```

- [ ] **Step 3: Wire it into `OnPostUpdateOccurrenceAsync`, gated on an actual date/time change**

Replace:
```csharp
    public async Task<IActionResult> OnPostUpdateOccurrenceAsync(int occurrenceId, int workshopId, DateTime occDate, TimeSpan occStartTime, TimeSpan? occEndTime, string? occEntrioUrl)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (occDate == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

        var occurrence = await _db.WorkshopOccurrences.FirstOrDefaultAsync(o => o.Id == occurrenceId && o.WorkshopId == workshopId);
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
```
with:
```csharp
    public async Task<IActionResult> OnPostUpdateOccurrenceAsync(int occurrenceId, int workshopId, DateTime occDate, TimeSpan occStartTime, TimeSpan? occEndTime, string? occEntrioUrl)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (occDate == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

        var occurrence = await _db.WorkshopOccurrences.FirstOrDefaultAsync(o => o.Id == occurrenceId && o.WorkshopId == workshopId);
        if (occurrence != null)
        {
            var dateTimeChanged = occurrence.Date != occDate || occurrence.StartTime != occStartTime || occurrence.EndTime != occEndTime;

            occurrence.Date = occDate;
            occurrence.StartTime = occStartTime;
            occurrence.EndTime = occEndTime;
            occurrence.EntrioUrl = occEntrioUrl;
            await _db.SaveChangesAsync();

            if (dateTimeChanged)
                await NotifyForOccurrenceChangeAsync(workshopId, occurrence, "Promjena termina");
        }

        return RedirectToPage(new { action = "edit", id = workshopId });
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add Pages/Admin/Workshops/Edit.cshtml.cs
git commit -m "Announce new/changed occurrence dates, bringing archived workshops back live when they gain a future date"
```

---

### Task 6: Flash banner in `_AdminLayout.cshtml`

**Files:**
- Modify: `Pages/Shared/_AdminLayout.cshtml:44-46`

**Interfaces:**
- Consumes: `TempData["Flash"]` (string) and `TempData["FlashType"]` (`"success"`/`"warning"`/`"error"`), set by `SetEmailResultFlash` (Task 2) and `OnPostUnarchiveAsync` (Task 4).

- [ ] **Step 1: Render the banner above `@RenderBody()`**

In `Pages/Shared/_AdminLayout.cshtml`, replace:
```html
    <div class="admin-container">
        @RenderBody()
    </div>
```
with:
```html
    <div class="admin-container">
        @{
            var flash = TempData["Flash"] as string;
            var flashType = TempData["FlashType"] as string ?? "success";
            var flashStyle = flashType switch
            {
                "error"   => "background:#FEE2E2; color:#DC2626; border-left:4px solid #DC2626;",
                "warning" => "background:#FEF9EC; color:#92610A; border-left:4px solid #F6C84B;",
                _         => "background:#EAF7EE; color:#1E7B3E; border-left:4px solid #34C471;",
            };
        }
        @if (!string.IsNullOrEmpty(flash))
        {
            <div class="login-error" style="@flashStyle margin-bottom:24px;">@flash</div>
        }
        @RenderBody()
    </div>
```

- [ ] **Step 2: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add Pages/Shared/_AdminLayout.cshtml
git commit -m "Render the email-send flash banner on every admin page"
```

---

### Task 7: Validate email format on `/api/subscribe`

**Files:**
- Modify: `Pages/Api/Subscribe.cshtml.cs:1-9` (usings), `Pages/Api/Subscribe.cshtml.cs:30-37` (validation)

**Interfaces:**
- None consumed from other tasks — independent of Tasks 1-6.

- [ ] **Step 1: Add the `System.Net.Mail` using**

In `Pages/Api/Subscribe.cshtml.cs`, add to the top of the using block:
```csharp
using System.Net.Mail;
```

- [ ] **Step 2: Validate before the existing-subscriber lookup**

Replace:
```csharp
        if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            return new JsonResult(new { ok = false });

        var email = payload.Email.Trim().ToLowerInvariant();
        var existing = await _db.Subscribers.FirstOrDefaultAsync(s => s.Email == email);
```
with:
```csharp
        if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            return new JsonResult(new { ok = false });

        var email = payload.Email.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
            return new JsonResult(new { ok = false });

        var existing = await _db.Subscribers.FirstOrDefaultAsync(s => s.Email == email);
```

- [ ] **Step 3: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 4: Manual check**

Run: `dotnet run` (in a separate terminal/background), then from another shell:
```bash
curl -s -X POST http://localhost:5000/api/subscribe -H "Content-Type: application/json" -d "{\"email\":\"not-an-email\"}"
```
Expected: `{"ok":false}` and no new row in the `Subscribers` table. Adjust the port to whatever `dotnet run` prints if it differs from 5000. Stop the app afterward.

- [ ] **Step 5: Commit**

```bash
git add Pages/Api/Subscribe.cshtml.cs
git commit -m "Reject non-email input on the subscribe endpoint"
```

---

### Task 8: Manual end-to-end verification

**Files:** none — this task only runs the app and exercises it through a browser/the admin panel. No code changes.

- [ ] **Step 1: Start the app**

Run: `dotnet run`
Confirm it starts without errors and note the local URL it prints.

- [ ] **Step 2: Confirm SMTP is configured for this pass**

Since sends are now awaited (not fire-and-forget), a real send requires `Email:Smtp:Password` to be set. Locally this is typically via `dotnet user-secrets` (see `DEPLOYMENT.md` section 4b). If it's unset, sends will still work correctly from a code standpoint (they'll return `SmtpConfigured: false` and show the error flash) — that's expected and covered in Step 8 below, but do at least one pass with real credentials configured first to see actual delivery.

- [ ] **Step 3: New workshop with notify checkbox**

In `/admin/workshops/new`, create a workshop, tick "Pošalji email svim pretplatnicima", save.
Expected: redirected to `/admin`, a green flash banner reading "Email poslan na N pretplatnika" (or nothing if there are zero active subscribers in the local DB).

- [ ] **Step 4: Edit an existing occurrence's date**

Open the workshop just created, change one occurrence's date, save.
Expected: green flash with a sent count. Then edit only that occurrence's `Entrio URL` field (leave date/time untouched) and save again.
Expected: no flash this time — no email should have gone out for a non-date-affecting edit.

- [ ] **Step 5: Add a new date to a live workshop**

On the same workshop, add a new date via "+ Novi datum".
Expected: green flash with a sent count.

- [ ] **Step 6: Archive → unarchive with a future date already present**

Archive the workshop, then reopen its edit page.
Expected: the "Vrati iz arhive" button is visible (it still has a future occurrence). Click it.
Expected: redirected back to the edit page, workshop is no longer archived, green flash shown.

- [ ] **Step 7: Archive → blocked unarchive → auto-unarchive on future date**

Edit the workshop's only occurrence(s) so all dates are in the past, then archive it, then reopen its edit page.
Expected: the banner shows the "Dodaj budući datum ispod..." message instead of a button. Edit one occurrence to a future date and save.
Expected: redirected back to the edit page; the archived banner is now gone (workshop is live again); green flash shown; confirm in the admin listing (`/admin`) that this workshop now appears under "Upcoming", not "Past".

- [ ] **Step 8: Reservable workshop archive/unarchive**

Create (or use the seeded) reservable workshop, archive it, then unarchive it from its edit page.
Expected: "Vrati iz arhive" is directly clickable (reservable workshops bypass the future-date requirement); after clicking, green flash shown; if you have SMTP access to check the inbox, confirm the email shows a "Rezerviraj" button instead of a date/time table.

- [ ] **Step 9: SMTP failure path**

Temporarily clear `Email:Smtp:Password` (e.g. `dotnet user-secrets remove "Email:Smtp:Password"`, or edit `appsettings.Development.json` if that's where it's set locally) and restart the app. Trigger any of the sends above (e.g. add a new date to a live workshop).
Expected: red flash reading "Slanje nije uspjelo — provjeri email postavke." instead of a silent no-op. Restore the password afterward (`dotnet user-secrets set ...` or revert the config edit) and restart the app again.

- [ ] **Step 10: Subscribe validation (covered in Task 7, re-confirm here in context)**

With the app running, POST a garbage string to `/api/subscribe` as in Task 7 Step 4, and separately POST a real-looking email address.
Expected: garbage → `{"ok":false}`, no DB row; real address → `{"ok":true}`, one new `Subscribers` row, and (if SMTP is configured) a welcome email received.

- [ ] **Step 11: Stop the app**

Stop the `dotnet run` process once all scenarios above are confirmed.
