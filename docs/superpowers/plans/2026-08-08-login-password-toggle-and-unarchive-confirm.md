# Login Password Toggle + Ask-Before-Send Unarchive Email Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a show/hide toggle to the admin login password field, and change both ways a workshop comes back from archive (the explicit "Vrati iz arhive" button, and automatically when an archived workshop gains a future occurrence date) so the admin is asked via a `confirm()` dialog whether to email subscribers — the workshop always un-archives either way, only the email becomes optional.

**Architecture:** Pure additive changes to two existing files (`Pages/Admin/Login.cshtml`, `Pages/Admin/Workshops/Edit.cshtml` + its code-behind) plus two new CSS rules. No new files, no schema changes, no new services. The unarchive/revival handlers gain a `bool sendEmail = true` parameter threaded from a hidden form field that a button's `onclick` overwrites with the result of `confirm()` before submitting — the form always submits either way.

**Tech Stack:** ASP.NET Core 8 Razor Pages, EF Core (SQLite), vanilla CSS/JS (no build step, no npm — per CLAUDE.md). No test project exists in this repo — verification is `dotnet build` per task plus a manual pass through the admin panel at the end (matches the project's existing testing approach, see `docs/superpowers/plans/2026-08-07-automatic-workshop-emails.md` for precedent).

## Global Constraints

- All UI strings are Croatian (per CLAUDE.md).
- No new NuGet packages, no JS build step, no Bootstrap/Tailwind (per CLAUDE.md).
- No schema/migration changes — this feature needs none.
- Reuse existing feather-style inline SVG icon conventions (stroke-based, `viewBox="0 0 24 24"`, `stroke-width="2"`) already used for delete buttons in `Edit.cshtml`.
- The confirm-then-submit pattern (`onclick="...; return true;"`) matches the existing delete-confirm pattern in `Edit.cshtml` (`onclick="return confirm('Obriši ovaj termin?')"`), except here the form must always submit regardless of the confirm result — only a hidden field's value changes.
- Declining the email must never block the unarchive/revival itself — per spec, Cancel only skips the send.
- Live-workshop (non-archived) date-change emails must remain fully automatic, untouched by this change — only the archive-revival email becomes optional.

---

### Task 1: Password visibility toggle on the login page

**Files:**
- Modify: `wwwroot/css/site.css:665` (insert after this line)
- Modify: `Pages/Admin/Login.cshtml:36-39` (password field), `Pages/Admin/Login.cshtml:43` (before `</body>`, add script)

**Interfaces:**
- Produces: `#password-input` (the existing password `<input>`, given an id), `#password-toggle` (new button), `#eye-icon` (new `<svg>` whose inner markup is swapped by the script). Self-contained — no other task depends on these.

- [ ] **Step 1: Add the CSS rules**

In `wwwroot/css/site.css`, immediately after line 665 (`.login-form .form-group { text-align: left; }`), insert:

```css
.password-field-wrap { position: relative; }
.password-field-wrap .form-control { padding-right: 44px; }
.password-toggle-btn {
    position: absolute; right: 10px; top: 50%; transform: translateY(-50%);
    background: none; border: none; cursor: pointer; color: var(--charcoal);
    opacity: 0.5; padding: 4px; display: flex;
}
.password-toggle-btn:hover { opacity: 0.8; }
```

- [ ] **Step 2: Wrap the password field and add the toggle button**

In `Pages/Admin/Login.cshtml`, replace:
```html
            <div class="form-group">
                <label class="form-label">Lozinka</label>
                <input type="password" name="Password" class="form-control" autocomplete="current-password" required />
            </div>
```
with:
```html
            <div class="form-group">
                <label class="form-label">Lozinka</label>
                <div class="password-field-wrap">
                    <input type="password" name="Password" id="password-input" class="form-control" autocomplete="current-password" required />
                    <button type="button" class="password-toggle-btn" id="password-toggle" aria-label="Prikaži lozinku">
                        <svg id="eye-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="width:18px;height:18px;">
                            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>
                        </svg>
                    </button>
                </div>
            </div>
```

- [ ] **Step 3: Add the toggle script**

In `Pages/Admin/Login.cshtml`, immediately before `</body>` (currently line 44), insert:
```html
<script>
document.getElementById('password-toggle').addEventListener('click', function () {
    var input = document.getElementById('password-input');
    var icon = document.getElementById('eye-icon');
    var isHidden = input.type === 'password';
    input.type = isHidden ? 'text' : 'password';
    this.setAttribute('aria-label', isHidden ? 'Sakrij lozinku' : 'Prikaži lozinku');
    icon.innerHTML = isHidden
        ? '<path d="M17.94 17.94A10.94 10.94 0 0 1 12 20c-7 0-11-8-11-8a18.5 18.5 0 0 1 5.06-5.94M9.9 4.24A10.94 10.94 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/>'
        : '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>';
});
</script>
```

- [ ] **Step 4: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded` (this task touches no `.cs` files, but build confirms the `.cshtml` still parses).

- [ ] **Step 5: Manual check**

Run: `dotnet run`, open `/admin/login` in a browser.
Expected: type a password → masked as normal. Click the eye icon → text becomes readable, icon swaps to an eye-with-a-slash-through-it, `aria-label` becomes "Sakrij lozinku". Click again → masked again, icon reverts, `aria-label` reverts. Submit a correct login with the field left visible (type="text" at submit time) → still logs in successfully (server reads the value regardless of the input's `type`). Stop the app afterward.

- [ ] **Step 6: Commit**

```bash
git add wwwroot/css/site.css Pages/Admin/Login.cshtml
git commit -m "Add show/hide toggle to the admin login password field"
```

---

### Task 2: Ask before sending the unarchive email (explicit "Vrati iz arhive" button)

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs:288-315` (`OnPostUnarchiveAsync`)
- Modify: `Pages/Admin/Workshops/Edit.cshtml:19-24` (unarchive button/form)

**Interfaces:**
- Produces: `OnPostUnarchiveAsync(int id, bool sendEmail = true)` — the `sendEmail` parameter is bound from a form field named `sendEmail`. Independent of Task 3 (different handler, no shared code).

- [ ] **Step 1: Add the `sendEmail` parameter and branch on it**

In `Pages/Admin/Workshops/Edit.cshtml.cs`, replace:
```csharp
    public async Task<IActionResult> OnPostUnarchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var workshop = await _db.Workshops.Include(w => w.Occurrences).FirstOrDefaultAsync(w => w.Id == id);
        if (workshop == null) return RedirectToPage("/Admin/Index");
        if (!workshop.IsArchived) return RedirectToPage(new { action = "edit", id });

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
with:
```csharp
    public async Task<IActionResult> OnPostUnarchiveAsync(int id, bool sendEmail = true)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var workshop = await _db.Workshops.Include(w => w.Occurrences).FirstOrDefaultAsync(w => w.Id == id);
        if (workshop == null) return RedirectToPage("/Admin/Index");
        if (!workshop.IsArchived) return RedirectToPage(new { action = "edit", id });

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

        if (sendEmail)
        {
            var subject = $"Radionica je ponovno dostupna: {workshop.Name} — Workshop Zagreb";
            var subs = await ActiveSubscribersAsync();
            var result = await _email.SendWorkshopAnnouncementAsync(workshop, futureOccurrence, subs, subject, "Ponovno dostupno");
            SetEmailResultFlash(result);
        }
        else
        {
            TempData["FlashType"] = "success";
            TempData["Flash"] = "Radionica je vraćena iz arhive.";
        }

        return RedirectToPage(new { action = "edit", id });
    }
```

- [ ] **Step 2: Wire the confirm dialog into the unarchive button**

In `Pages/Admin/Workshops/Edit.cshtml`, replace:
```html
        @if (Model.CanUnarchiveDirectly)
        {
            <form method="post" asp-page-handler="Unarchive" asp-route-id="@Model.Input.Id" style="display:inline;">
                <button type="submit" class="btn btn-outline btn-sm" style="margin-left:8px;">Vrati iz arhive</button>
            </form>
        }
```
with:
```html
        @if (Model.CanUnarchiveDirectly)
        {
            <form method="post" asp-page-handler="Unarchive" asp-route-id="@Model.Input.Id" style="display:inline;">
                <input type="hidden" name="sendEmail" id="unarchive-send-email" value="true" />
                <button type="submit" class="btn btn-outline btn-sm" style="margin-left:8px;"
                        onclick="document.getElementById('unarchive-send-email').value = confirm('Poslati email pretplatnicima da je radionica ponovno dostupna?'); return true;">
                    Vrati iz arhive
                </button>
            </form>
        }
```

- [ ] **Step 3: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 4: Manual check**

Run: `dotnet run`. In the admin panel, archive a workshop that already has a future occurrence (or is reservable), then open its edit page.
Expected: "Vrati iz arhive" button is visible. Click it, then click **OK** on the confirm dialog ("Poslati email pretplatnicima da je radionica ponovno dostupna?") → redirected to the edit page, the archived banner is gone, and a flash banner shows a sent count (or nothing if there are zero active subscribers). Re-archive the same workshop, click "Vrati iz arhive" again, this time click **Cancel** → redirected to the edit page, archived banner still gone (workshop is un-archived either way), flash reads "Radionica je vraćena iz arhive." with no send-count language, and no email is actually sent (check logs/inbox if SMTP is configured). Stop the app afterward.

- [ ] **Step 5: Commit**

```bash
git add Pages/Admin/Workshops/Edit.cshtml.cs Pages/Admin/Workshops/Edit.cshtml
git commit -m "Ask before sending the unarchive email from the explicit 'Vrati iz arhive' button"
```

---

### Task 3: Ask before sending the unarchive email (automatic revival via a new/edited date)

**Files:**
- Modify: `Pages/Admin/Workshops/Edit.cshtml.cs:213-237` (`OnPostAddOccurrenceAsync`), `Pages/Admin/Workshops/Edit.cshtml.cs:239-264` (`OnPostUpdateOccurrenceAsync`), `Pages/Admin/Workshops/Edit.cshtml.cs:359-393` (`NotifyForOccurrenceChangeAsync`)
- Modify: `Pages/Admin/Workshops/Edit.cshtml:83` (per-occurrence "Spremi" button), `Pages/Admin/Workshops/Edit.cshtml:117` ("+ Novi datum" button), `Pages/Admin/Workshops/Edit.cshtml:402-418` (the hidden `occ-edit-*`/`occ-add` forms at the bottom of the page)

**Interfaces:**
- Consumes: none from Task 2 (separate handler methods; `Task 2` only touched `OnPostUnarchiveAsync`).
- Produces: `NotifyForOccurrenceChangeAsync(int workshopId, WorkshopOccurrence occurrence, string liveKicker, bool hadFutureOccurrenceBefore, bool sendEmail = true)` — the extra parameter is consulted **only** in the archive-revival branch; the "still-live" branch ignores it and always sends, unchanged from today.

- [ ] **Step 1: Add the `sendEmail` parameter to `NotifyForOccurrenceChangeAsync` and branch on it in the revival case only**

In `Pages/Admin/Workshops/Edit.cshtml.cs`, replace:
```csharp
    private async Task NotifyForOccurrenceChangeAsync(int workshopId, WorkshopOccurrence occurrence, string liveKicker, bool hadFutureOccurrenceBefore)
    {
        var workshop = await _db.Workshops.FindAsync(workshopId);
        if (workshop == null) return;

        var hasFuture = await _db.WorkshopOccurrences
            .AnyAsync(o => o.WorkshopId == workshopId && o.Date >= DateTime.Today);

        string subject;
        string kicker;

        if (workshop.IsArchived && !workshop.IsReservable && !hadFutureOccurrenceBefore && hasFuture)
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
with:
```csharp
    private async Task NotifyForOccurrenceChangeAsync(int workshopId, WorkshopOccurrence occurrence, string liveKicker, bool hadFutureOccurrenceBefore, bool sendEmail = true)
    {
        var workshop = await _db.Workshops.FindAsync(workshopId);
        if (workshop == null) return;

        var hasFuture = await _db.WorkshopOccurrences
            .AnyAsync(o => o.WorkshopId == workshopId && o.Date >= DateTime.Today);

        string subject;
        string kicker;

        if (workshop.IsArchived && !workshop.IsReservable && !hadFutureOccurrenceBefore && hasFuture)
        {
            workshop.IsArchived = false;
            await _db.SaveChangesAsync();

            if (!sendEmail)
            {
                TempData["FlashType"] = "success";
                TempData["Flash"] = "Radionica je vraćena iz arhive.";
                return;
            }

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

Note: `sendEmail` is only read inside the first branch (revival). The `else if (!workshop.IsArchived)` branch (a plain live-workshop date change) always sends regardless of `sendEmail` — this is intentional, matching the spec's scope (only the archive-revival email becomes optional).

- [ ] **Step 2: Thread `sendEmail` through `OnPostAddOccurrenceAsync`**

In `Pages/Admin/Workshops/Edit.cshtml.cs`, replace:
```csharp
    public async Task<IActionResult> OnPostAddOccurrenceAsync(int workshopId)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (NewOccurrence.Date == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

        var hadFutureOccurrenceBefore = await _db.WorkshopOccurrences
            .AnyAsync(o => o.WorkshopId == workshopId && o.Date >= DateTime.Today);

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

        await NotifyForOccurrenceChangeAsync(workshopId, newOccurrence, "Novi termin", hadFutureOccurrenceBefore);

        return RedirectToPage(new { action = "edit", id = workshopId });
    }
```
with:
```csharp
    public async Task<IActionResult> OnPostAddOccurrenceAsync(int workshopId, bool sendEmail = true)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (NewOccurrence.Date == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

        var hadFutureOccurrenceBefore = await _db.WorkshopOccurrences
            .AnyAsync(o => o.WorkshopId == workshopId && o.Date >= DateTime.Today);

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

        await NotifyForOccurrenceChangeAsync(workshopId, newOccurrence, "Novi termin", hadFutureOccurrenceBefore, sendEmail);

        return RedirectToPage(new { action = "edit", id = workshopId });
    }
```

- [ ] **Step 3: Thread `sendEmail` through `OnPostUpdateOccurrenceAsync`**

In `Pages/Admin/Workshops/Edit.cshtml.cs`, replace:
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
            var hadFutureOccurrenceBefore = occurrence.Date >= DateTime.Today || await _db.WorkshopOccurrences
                .AnyAsync(o => o.WorkshopId == workshopId && o.Id != occurrenceId && o.Date >= DateTime.Today);

            occurrence.Date = occDate;
            occurrence.StartTime = occStartTime;
            occurrence.EndTime = occEndTime;
            occurrence.EntrioUrl = occEntrioUrl;
            await _db.SaveChangesAsync();

            if (dateTimeChanged)
                await NotifyForOccurrenceChangeAsync(workshopId, occurrence, "Promjena termina", hadFutureOccurrenceBefore);
        }

        return RedirectToPage(new { action = "edit", id = workshopId });
    }
```
with:
```csharp
    public async Task<IActionResult> OnPostUpdateOccurrenceAsync(int occurrenceId, int workshopId, DateTime occDate, TimeSpan occStartTime, TimeSpan? occEndTime, string? occEntrioUrl, bool sendEmail = true)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (occDate == default)
            return RedirectToPage(new { action = "edit", id = workshopId });

        var occurrence = await _db.WorkshopOccurrences.FirstOrDefaultAsync(o => o.Id == occurrenceId && o.WorkshopId == workshopId);
        if (occurrence != null)
        {
            var dateTimeChanged = occurrence.Date != occDate || occurrence.StartTime != occStartTime || occurrence.EndTime != occEndTime;
            var hadFutureOccurrenceBefore = occurrence.Date >= DateTime.Today || await _db.WorkshopOccurrences
                .AnyAsync(o => o.WorkshopId == workshopId && o.Id != occurrenceId && o.Date >= DateTime.Today);

            occurrence.Date = occDate;
            occurrence.StartTime = occStartTime;
            occurrence.EndTime = occEndTime;
            occurrence.EntrioUrl = occEntrioUrl;
            await _db.SaveChangesAsync();

            if (dateTimeChanged)
                await NotifyForOccurrenceChangeAsync(workshopId, occurrence, "Promjena termina", hadFutureOccurrenceBefore, sendEmail);
        }

        return RedirectToPage(new { action = "edit", id = workshopId });
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 5: Add hidden `sendEmail` fields to the occurrence forms, only while archived**

In `Pages/Admin/Workshops/Edit.cshtml`, replace the hidden-forms block near the bottom of the file:
```html
@if (!isNew && !Model.Input.IsReservable)
{
    @foreach (var occ in Model.Occurrences)
    {
        <form id="occ-edit-@occ.Id" method="post" asp-page-handler="UpdateOccurrence" style="display:none;">
            <input type="hidden" name="occurrenceId" value="@occ.Id" />
            <input type="hidden" name="workshopId" value="@Model.Input.Id" />
        </form>
        <form id="occ-delete-@occ.Id" method="post" asp-page-handler="DeleteOccurrence" style="display:none;">
            <input type="hidden" name="occurrenceId" value="@occ.Id" />
            <input type="hidden" name="workshopId" value="@Model.Input.Id" />
        </form>
    }
    <form id="occ-add" method="post" asp-page-handler="AddOccurrence" style="display:none;">
        <input type="hidden" name="workshopId" value="@Model.Input.Id" />
    </form>
}
```
with:
```html
@if (!isNew && !Model.Input.IsReservable)
{
    @foreach (var occ in Model.Occurrences)
    {
        <form id="occ-edit-@occ.Id" method="post" asp-page-handler="UpdateOccurrence" style="display:none;">
            <input type="hidden" name="occurrenceId" value="@occ.Id" />
            <input type="hidden" name="workshopId" value="@Model.Input.Id" />
            @if (Model.IsArchivedWorkshop)
            {
                <input type="hidden" name="sendEmail" id="occ-edit-@(occ.Id)-send-email" value="true" />
            }
        </form>
        <form id="occ-delete-@occ.Id" method="post" asp-page-handler="DeleteOccurrence" style="display:none;">
            <input type="hidden" name="occurrenceId" value="@occ.Id" />
            <input type="hidden" name="workshopId" value="@Model.Input.Id" />
        </form>
    }
    <form id="occ-add" method="post" asp-page-handler="AddOccurrence" style="display:none;">
        <input type="hidden" name="workshopId" value="@Model.Input.Id" />
        @if (Model.IsArchivedWorkshop)
        {
            <input type="hidden" name="sendEmail" id="occ-add-send-email" value="true" />
        }
    </form>
}
```

- [ ] **Step 6: Wire the confirm dialog into the "Spremi" (per-occurrence save) button, only while archived**

In `Pages/Admin/Workshops/Edit.cshtml`, replace:
```html
                        <button type="submit" form="occ-edit-@occ.Id" class="btn btn-outline btn-sm">Spremi</button>
```
with:
```html
                        @if (Model.IsArchivedWorkshop)
                        {
                            <button type="submit" form="occ-edit-@occ.Id" class="btn btn-outline btn-sm"
                                    onclick="document.getElementById('occ-edit-@(occ.Id)-send-email').value = confirm('Ako ovaj datum vrati radionicu iz arhive, poslati email pretplatnicima?'); return true;">
                                Spremi
                            </button>
                        }
                        else
                        {
                            <button type="submit" form="occ-edit-@occ.Id" class="btn btn-outline btn-sm">Spremi</button>
                        }
```

- [ ] **Step 7: Wire the confirm dialog into the "+ Novi datum" button, only while archived**

In `Pages/Admin/Workshops/Edit.cshtml`, replace:
```html
                <button type="submit" id="add-occ-btn" form="occ-add" class="btn btn-primary btn-sm" disabled>+ Novi datum</button>
```
with:
```html
                @if (Model.IsArchivedWorkshop)
                {
                    <button type="submit" id="add-occ-btn" form="occ-add" class="btn btn-primary btn-sm" disabled
                            onclick="document.getElementById('occ-add-send-email').value = confirm('Ako ovaj datum vrati radionicu iz arhive, poslati email pretplatnicima?'); return true;">
                        + Novi datum
                    </button>
                }
                else
                {
                    <button type="submit" id="add-occ-btn" form="occ-add" class="btn btn-primary btn-sm" disabled>+ Novi datum</button>
                }
```

The existing script right below (`document.getElementById('new-occ-date').addEventListener(...)`) references `add-occ-btn` by id, which both branches keep, so the disabled/enabled-on-date-picked behavior keeps working unchanged.

- [ ] **Step 8: Build**

Run: `dotnet build --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 9: Manual check**

Run: `dotnet run`.

*Revival via editing an existing date, accept:* Archive a dated (non-reservable) workshop whose only occurrence is in the past (edit page shows the "Dodaj budući datum..." message, no button). Edit that occurrence's date to a future date and click "Spremi" → confirm dialog appears ("Ako ovaj datum vrati radionicu iz arhive, poslati email pretplatnicima?") → click **OK** → redirected back to the edit page, archived banner is gone, flash shows a sent count.

*Revival via editing an existing date, decline:* Repeat, but click **Cancel** on the confirm dialog → workshop still un-archives (confirm on `/admin` it now appears under "Upcoming", not "Past"/archived), but flash reads "Radionica je vraćena iz arhive." with no send count, and no email is actually sent.

*Revival via "+ Novi datum", accept/decline:* Archive another dated workshop with only past dates, use "+ Novi datum" to add a future date, confirm both the accept and decline paths behave the same way as above.

*Live workshop unaffected:* On a workshop that is **not** archived, edit an occurrence's date or add a new date → no confirm dialog appears at all, the email sends automatically exactly as it did before this change.

*Reservable workshop unaffected:* Confirm reservable workshops have no occurrence forms at all (this task's changes only touch the dated-workshop occurrence editor) — their archive/unarchive still only goes through Task 2's button.

Stop the app afterward.

- [ ] **Step 10: Commit**

```bash
git add Pages/Admin/Workshops/Edit.cshtml.cs Pages/Admin/Workshops/Edit.cshtml
git commit -m "Ask before sending the unarchive email when a workshop auto-revives via a new/edited date"
```

---

### Task 4: Full manual end-to-end verification

**Files:** none — this task only runs the app and exercises it through a browser. No code changes.

- [ ] **Step 1: Start the app**

Run: `dotnet run`. Confirm it starts without errors and note the local URL it prints.

- [ ] **Step 2: Re-walk every scenario from the spec's Testing section in one continuous session**

Using `docs/superpowers/specs/2026-08-08-login-password-toggle-and-unarchive-confirm-design.md`'s "Testing" list as the checklist, confirm all 7 scenarios in order (login toggle; Path A accept; Path A decline; Path B accept; Path B decline; live workshop unaffected; reservable workshop). This is a re-confirmation pass after all three tasks are integrated together, not a repeat of any single task's isolated check.

- [ ] **Step 3: Stop the app**

Stop the `dotnet run` process once all scenarios are confirmed.
