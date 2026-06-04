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

    public WorkshopEditModel(AppDbContext db, IFileService files)
    {
        _db = db;
        _files = files;
    }

    // -------------------------------------------------------
    // InputModel: holds all form field values.
    // [BindProperty] means ASP.NET automatically fills this
    // from the POST form data — no manual parsing needed.
    // -------------------------------------------------------
    [BindProperty]
    public WorkshopInputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerFile { get; set; }

    [BindProperty]
    public IFormFile? LogoFile { get; set; }

    [BindProperty]
    public List<IFormFile> PhotoFiles { get; set; } = new();

    public List<WorkshopPhoto> ExistingPhotos { get; set; } = new();
    public bool IsNew { get; set; }
    public bool IsArchivedWorkshop { get; set; }
    public string? StatusMessage { get; set; }

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    // GET: load the form (empty for new, pre-filled for edit)
    public async Task<IActionResult> OnGetAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        IsNew = action == "new";

        if (!IsNew && id.HasValue)
        {
            var workshop = await _db.Workshops
                .Include(w => w.Photos)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workshop == null) return NotFound();

            // Pre-fill the form with existing values
            Input = new WorkshopInputModel
            {
                Id = workshop.Id,
                Name = workshop.Name,
                Date = workshop.Date,
                StartTime = workshop.StartTime,
                EndTime = workshop.EndTime,
                Description = workshop.Description,
                Price = workshop.Price,
                MaxParticipants = workshop.MaxParticipants,
                InstagramPostUrl = workshop.InstagramPostUrl,
                EntrioUrl = workshop.EntrioUrl,
                HostName = workshop.HostName,
                HostInstagram = workshop.HostInstagram,
                HostWebsite = workshop.HostWebsite,
                ExistingLogoUrl   = workshop.LogoUrl,
                ExistingBannerUrl = workshop.BannerUrl,
            };
            ExistingPhotos = workshop.Photos.OrderBy(p => p.Order).ToList();
            IsArchivedWorkshop = workshop.Date < DateTime.Today;
        }
        else
        {
            Input.Date = DateTime.Today.AddDays(7); // sensible default
        }

        return Page();
    }

    // POST: save the workshop (create or update)
    public async Task<IActionResult> OnPostAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        IsNew = action == "new";

        // Re-load existing photos for display in case of validation error
        if (!IsNew && Input.Id > 0)
            ExistingPhotos = await _db.WorkshopPhotos
                .Where(p => p.WorkshopId == Input.Id)
                .ToListAsync();

        if (!ModelState.IsValid)
            return Page();

        if (IsNew)
        {
            // --- CREATE ---
            if (BannerFile == null)
            {
                ModelState.AddModelError("BannerFile", "Banner image is required.");
                return Page();
            }

            var bannerUrl = await _files.SaveImageAsync(BannerFile, "workshops");
            var logoUrl   = LogoFile != null
                ? await _files.SaveImageAsync(LogoFile, "workshops")
                : null;

            var workshop = new Workshop
            {
                Name = Input.Name,
                Date = Input.Date,
                StartTime = Input.StartTime,
                EndTime = Input.EndTime,
                Description = Input.Description,
                Price = Input.Price,
                MaxParticipants = Input.MaxParticipants,
                InstagramPostUrl = Input.InstagramPostUrl ?? "",
                EntrioUrl = Input.EntrioUrl,
                HostName = Input.HostName,
                HostInstagram = Input.HostInstagram,
                HostWebsite = Input.HostWebsite,
                BannerUrl = bannerUrl,
                LogoUrl   = logoUrl,
                Slug = await GenerateUniqueSlugAsync(Input.Name),
            };

            _db.Workshops.Add(workshop);
            await _db.SaveChangesAsync();

            // Save additional photos
            await SavePhotosAsync(workshop.Id);
        }
        else
        {
            // --- UPDATE ---
            var workshop = await _db.Workshops.FindAsync(Input.Id);
            if (workshop == null) return NotFound();

            workshop.Name = Input.Name;
            workshop.Date = Input.Date;
            workshop.StartTime = Input.StartTime;
            workshop.EndTime = Input.EndTime;
            workshop.Description = Input.Description;
            workshop.Price = Input.Price;
            workshop.MaxParticipants = Input.MaxParticipants;
            workshop.InstagramPostUrl = Input.InstagramPostUrl ?? "";
            workshop.EntrioUrl = Input.EntrioUrl;
            workshop.HostName = Input.HostName;
            workshop.HostInstagram = Input.HostInstagram;
            workshop.HostWebsite = Input.HostWebsite;

            // Replace banner if a new file was uploaded
            if (BannerFile != null)
            {
                if (workshop.BannerUrl != null) _files.DeleteImage(workshop.BannerUrl);
                workshop.BannerUrl = await _files.SaveImageAsync(BannerFile, "workshops");
            }

            // Replace logo if a new file was uploaded
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

    // DELETE a single photo (the ✕ button on each photo thumbnail)
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

        // Reload the edit form for the same workshop
        return RedirectToPage(new { action = "edit", id = id });
    }

    // Helper: saves uploaded photo files and links them to a workshop
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

    // Helper: makes "Akvarel za početnike" → "akvarel-za-pocetnike"
    // and adds "-2" if that slug is already taken
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

// Flat DTO that matches the HTML form fields 1:1
// No complex binding — just plain properties
public class WorkshopInputModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new TimeSpan(14, 0, 0);
    public TimeSpan? EndTime { get; set; }
    public string Description { get; set; } = "";
    public decimal? Price { get; set; }
    public int? MaxParticipants { get; set; }
    public string InstagramPostUrl { get; set; } = "";
    public string? EntrioUrl { get; set; }
    public string? HostName { get; set; }
    public string? HostInstagram { get; set; }
    public string? HostWebsite { get; set; }
    public string? ExistingLogoUrl   { get; set; }
    public string? ExistingBannerUrl { get; set; }
}
