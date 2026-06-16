using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using WorkshopZagreb.Services;

namespace WorkshopZagreb.Pages.Admin.Pinned;

public class PinnedEditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileService _files;

    public PinnedEditModel(AppDbContext db, IFileService files)
    {
        _db = db;
        _files = files;
    }

    [BindProperty]
    public PinnedInputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerFile { get; set; }

    public bool IsNew { get; set; }

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
            var pinned = await _db.PinnedWorkshops.FindAsync(id);
            if (pinned == null) return NotFound();

            Input = new PinnedInputModel
            {
                Id = pinned.Id,
                Name = pinned.Name,
                Subtitle = pinned.Subtitle ?? "",
                Description = pinned.Description,
                StartingPrice = pinned.StartingPrice,
                IsActive = pinned.IsActive,
                DisplayOrder = pinned.DisplayOrder,
                ExistingBannerUrl = pinned.BannerUrl,
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        IsNew = action == "new";

        if (!ModelState.IsValid) return Page();

        if (IsNew)
        {
            var bannerUrl = BannerFile != null
                ? await _files.SaveImageAsync(BannerFile, "pinned")
                : null;

            _db.PinnedWorkshops.Add(new PinnedWorkshop
            {
                Name = Input.Name,
                Subtitle = string.IsNullOrWhiteSpace(Input.Subtitle) ? null : Input.Subtitle,
                Description = Input.Description,
                StartingPrice = Input.StartingPrice,
                IsActive = Input.IsActive,
                DisplayOrder = Input.DisplayOrder,
                BannerUrl = bannerUrl,
            });
            await _db.SaveChangesAsync();
        }
        else
        {
            var pinned = await _db.PinnedWorkshops.FindAsync(Input.Id);
            if (pinned == null) return NotFound();

            pinned.Name = Input.Name;
            pinned.Subtitle = string.IsNullOrWhiteSpace(Input.Subtitle) ? null : Input.Subtitle;
            pinned.Description = Input.Description;
            pinned.StartingPrice = Input.StartingPrice;
            pinned.IsActive = Input.IsActive;
            pinned.DisplayOrder = Input.DisplayOrder;

            if (BannerFile != null)
            {
                if (pinned.BannerUrl != null) _files.DeleteImage(pinned.BannerUrl);
                pinned.BannerUrl = await _files.SaveImageAsync(BannerFile, "pinned");
            }

            await _db.SaveChangesAsync();
        }

        return RedirectToPage("/Admin/Index");
    }
}

public class PinnedInputModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal? StartingPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? ExistingBannerUrl { get; set; }
}
