using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using WorkshopZagreb.Services;

namespace WorkshopZagreb.Pages.Admin.Workshops;

public class WorkshopEditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileService _files;
    private readonly IEmailService _email;

    public WorkshopEditModel(AppDbContext db, IFileService files, IEmailService email)
    {
        _db = db;
        _files = files;
        _email = email;
    }

    [BindProperty]
    public WorkshopInputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerFile { get; set; }

    [BindProperty]
    public IFormFile? LogoFile { get; set; }

    [BindProperty]
    public List<IFormFile> PhotoFiles { get; set; } = new();

    [BindProperty]
    public OccurrenceInputModel NewOccurrence { get; set; } = new();

    public List<WorkshopPhoto> ExistingPhotos { get; set; } = new();
    public List<WorkshopOccurrence> Occurrences { get; set; } = new();
    public bool IsNew { get; set; }
    public bool IsArchivedWorkshop { get; set; }
    public bool CanUnarchiveDirectly { get; set; }

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        IsNew = action == "new";

        if (!IsNew && id.HasValue)
        {
            var workshop = await _db.Workshops
                .Include(w => w.Photos)
                .Include(w => w.Occurrences)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workshop == null) return NotFound();

            Input = new WorkshopInputModel
            {
                Id = workshop.Id,
                Name = workshop.Name,
                IsReservable = workshop.IsReservable,
                BookingType = workshop.BookingType ?? "webpage",
                BookingValue = workshop.BookingValue ?? "",
                Description = workshop.Description,
                Price = workshop.Price,
                MaxParticipants = workshop.MaxParticipants,
                InstagramPostUrl = workshop.InstagramPostUrl,
                HostName = workshop.HostName,
                HostInstagram = workshop.HostInstagram,
                HostWebsite = workshop.HostWebsite,
                ExistingLogoUrl   = workshop.LogoUrl,
                ExistingBannerUrl = workshop.BannerUrl,
            };
            ExistingPhotos = workshop.Photos.OrderBy(p => p.Order).ToList();
            Occurrences = workshop.Occurrences.OrderBy(o => o.Date).ToList();
            IsArchivedWorkshop = workshop.IsArchived;
            CanUnarchiveDirectly = workshop.IsReservable || workshop.Occurrences.Any(o => o.Date >= DateTime.Today);
        }
        else
        {
            NewOccurrence.Date = DateTime.Today.AddDays(7); // sensible default
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        IsNew = action == "new";

        if (!IsNew && Input.Id > 0)
        {
            ExistingPhotos = await _db.WorkshopPhotos.Where(p => p.WorkshopId == Input.Id).ToListAsync();
            Occurrences = await _db.WorkshopOccurrences.Where(o => o.WorkshopId == Input.Id).OrderBy(o => o.Date).ToListAsync();
        }

        if (!ModelState.IsValid)
            return Page();

        if (IsNew)
        {
            if (BannerFile == null)
            {
                ModelState.AddModelError("BannerFile", "Banner image is required.");
                return Page();
            }
            if (!Input.IsReservable && NewOccurrence.Date == default)
            {
                ModelState.AddModelError("NewOccurrence.Date", "Date is required for a non-reservable workshop.");
                return Page();
            }

            var bannerUrl = await _files.SaveImageAsync(BannerFile, "workshops");
            var logoUrl   = LogoFile != null
                ? await _files.SaveImageAsync(LogoFile, "workshops")
                : null;

            var workshop = new Workshop
            {
                Name = Input.Name,
                IsReservable = Input.IsReservable,
                BookingType = Input.IsReservable ? Input.BookingType : null,
                BookingValue = Input.IsReservable ? Input.BookingValue : null,
                Description = Input.Description,
                Price = Input.Price,
                MaxParticipants = Input.MaxParticipants,
                InstagramPostUrl = Input.InstagramPostUrl ?? "",
                HostName = Input.HostName,
                HostInstagram = Input.HostInstagram,
                HostWebsite = Input.HostWebsite,
                BannerUrl = bannerUrl,
                LogoUrl   = logoUrl,
                Slug = await GenerateUniqueSlugAsync(Input.Name),
            };

            _db.Workshops.Add(workshop);
            await _db.SaveChangesAsync();
            await SavePhotosAsync(workshop.Id);

            WorkshopOccurrence? firstOccurrence = null;
            if (!Input.IsReservable)
            {
                firstOccurrence = new WorkshopOccurrence
                {
                    WorkshopId = workshop.Id,
                    Date = NewOccurrence.Date,
                    StartTime = NewOccurrence.StartTime,
                    EndTime = NewOccurrence.EndTime,
                    EntrioUrl = NewOccurrence.EntrioUrl,
                };
                _db.WorkshopOccurrences.Add(firstOccurrence);
                await _db.SaveChangesAsync();
            }

            if (Input.NotifySubscribers && firstOccurrence != null)
            {
                var subject = string.IsNullOrWhiteSpace(Input.EmailSubject)
                    ? $"Nova radionica! - {workshop.Name}"
                    : Input.EmailSubject;
                var newSubs = await ActiveSubscribersAsync();
                var result = await _email.SendWorkshopAnnouncementAsync(workshop, firstOccurrence, newSubs, subject);
                SetEmailResultFlash(result);
            }
        }
        else
        {
            var workshop = await _db.Workshops.FindAsync(Input.Id);
            if (workshop == null) return NotFound();

            workshop.Name = Input.Name;
            workshop.IsReservable = Input.IsReservable;
            workshop.BookingType = Input.IsReservable ? Input.BookingType : null;
            workshop.BookingValue = Input.IsReservable ? Input.BookingValue : null;
            workshop.Description = Input.Description;
            workshop.Price = Input.Price;
            workshop.MaxParticipants = Input.MaxParticipants;
            workshop.InstagramPostUrl = Input.InstagramPostUrl ?? "";
            workshop.HostName = Input.HostName;
            workshop.HostInstagram = Input.HostInstagram;
            workshop.HostWebsite = Input.HostWebsite;

            if (BannerFile != null)
            {
                if (workshop.BannerUrl != null) _files.DeleteImage(workshop.BannerUrl);
                workshop.BannerUrl = await _files.SaveImageAsync(BannerFile, "workshops");
            }

            if (LogoFile != null)
            {
                if (workshop.LogoUrl != null) _files.DeleteImage(workshop.LogoUrl);
                workshop.LogoUrl = await _files.SaveImageAsync(LogoFile, "workshops");
            }

            await _db.SaveChangesAsync();
            await SavePhotosAsync(workshop.Id);
        }

        return RedirectToPage("/Admin/Index");
    }

    // Adds one more date to an existing (non-reservable) workshop — this is the
    // "New date" feature: reuse the workshop's content, just pick a new date.
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

    public async Task<IActionResult> OnPostDeleteOccurrenceAsync(int occurrenceId, int workshopId)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var occurrence = await _db.WorkshopOccurrences.FirstOrDefaultAsync(o => o.Id == occurrenceId && o.WorkshopId == workshopId);
        if (occurrence != null)
        {
            _db.WorkshopOccurrences.Remove(occurrence);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { action = "edit", id = workshopId });
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = true; await _db.SaveChangesAsync(); }
        return RedirectToPage("/Admin/Index");
    }

    public async Task<IActionResult> OnPostUnarchiveAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = false; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { action = "edit", id });
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int photoId, string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var photo = await _db.WorkshopPhotos.FindAsync(photoId);
        if (photo != null)
        {
            _files.DeleteImage(photo.Url);
            _db.WorkshopPhotos.Remove(photo);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { action = "edit", id = id });
    }

    private async Task SavePhotosAsync(int workshopId)
    {
        int order = await _db.WorkshopPhotos
            .Where(p => p.WorkshopId == workshopId)
            .CountAsync();

        foreach (var file in PhotoFiles)
        {
            if (file.Length > 0)
            {
                var url = await _files.SaveImageAsync(file, "workshops");
                _db.WorkshopPhotos.Add(new WorkshopPhoto
                {
                    WorkshopId = workshopId,
                    Url = url,
                    Order = order++
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    private Task<List<Subscriber>> ActiveSubscribersAsync() =>
        _db.Subscribers
            .Where(s => s.ConfirmedAt != null && s.UnsubscribedAt == null)
            .ToListAsync();

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

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var base_slug = SlugHelper.Generate(name);
        var slug = base_slug;
        int i = 2;
        while (await _db.Workshops.AnyAsync(w => w.Slug == slug))
        {
            slug = $"{base_slug}-{i++}";
        }
        return slug;
    }
}

public class WorkshopInputModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsReservable { get; set; }
    public string BookingType { get; set; } = "webpage";
    public string BookingValue { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Price { get; set; }
    public int? MaxParticipants { get; set; }
    public string InstagramPostUrl { get; set; } = "";
    public string? HostName { get; set; }
    public string? HostInstagram { get; set; }
    public string? HostWebsite { get; set; }
    public string? ExistingLogoUrl   { get; set; }
    public string? ExistingBannerUrl { get; set; }
    public bool NotifySubscribers { get; set; }
    public string EmailSubject { get; set; } = "";
}

public class OccurrenceInputModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new TimeSpan(14, 0, 0);
    public TimeSpan? EndTime { get; set; }
    public string? EntrioUrl { get; set; }
}
