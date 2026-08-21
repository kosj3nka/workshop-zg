using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using WorkshopZagreb.Services;

namespace WorkshopZagreb.Pages.Admin.Promos;

public class PromoEditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileService _files;

    public PromoEditModel(AppDbContext db, IFileService files)
    {
        _db = db;
        _files = files;
    }

    [BindProperty]
    public PromoInputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? ImageFile { get; set; }

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
            var promo = await _db.Promos.FindAsync(id.Value);
            if (promo == null) return NotFound();

            Input = new PromoInputModel
            {
                Id = promo.Id,
                Heading = promo.Heading,
                Subheading = promo.Subheading,
                ExistingImageUrl = promo.ImageUrl,
                ExistingIsVideo = promo.IsVideo,
                FocalX = promo.FocalX,
                FocalY = promo.FocalY,
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string action, int? id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        IsNew = action == "new";

        if (!ModelState.IsValid)
            return Page();

        bool? uploadIsVideo = null;
        if (ImageFile != null)
        {
            if (_files.IsSupportedVideo(ImageFile)) uploadIsVideo = true;
            else if (_files.IsSupportedImage(ImageFile)) uploadIsVideo = false;
            else
            {
                ModelState.AddModelError("ImageFile", "Unsupported file type. Use JPG, PNG, WEBP, GIF, MP4, WEBM or MOV.");
                return Page();
            }
        }

        if (IsNew)
        {
            if (ImageFile == null)
            {
                ModelState.AddModelError("ImageFile", "Photo or video is required.");
                return Page();
            }

            var maxOrder = await _db.Promos.Select(p => (int?)p.DisplayOrder).MaxAsync() ?? -1;

            var promo = new Promo
            {
                ImageUrl = await _files.SaveMediaAsync(ImageFile, "promos"),
                IsVideo = uploadIsVideo!.Value,
                Heading = string.IsNullOrWhiteSpace(Input.Heading) ? null : Input.Heading.Trim(),
                Subheading = string.IsNullOrWhiteSpace(Input.Subheading) ? null : Input.Subheading.Trim(),
                FocalX = Math.Clamp(Input.FocalX, 0, 100),
                FocalY = Math.Clamp(Input.FocalY, 0, 100),
                DisplayOrder = maxOrder + 1,
            };
            _db.Promos.Add(promo);
            await _db.SaveChangesAsync();
        }
        else
        {
            var promo = await _db.Promos.FindAsync(Input.Id);
            if (promo == null) return NotFound();

            promo.Heading = string.IsNullOrWhiteSpace(Input.Heading) ? null : Input.Heading.Trim();
            promo.Subheading = string.IsNullOrWhiteSpace(Input.Subheading) ? null : Input.Subheading.Trim();
            promo.FocalX = Math.Clamp(Input.FocalX, 0, 100);
            promo.FocalY = Math.Clamp(Input.FocalY, 0, 100);

            if (ImageFile != null)
            {
                _files.DeleteImage(promo.ImageUrl);
                promo.ImageUrl = await _files.SaveMediaAsync(ImageFile, "promos");
                promo.IsVideo = uploadIsVideo!.Value;
            }

            await _db.SaveChangesAsync();
        }

        return RedirectToPage("/Admin/Promos/Index");
    }
}

public class PromoInputModel
{
    public int Id { get; set; }
    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? ExistingImageUrl { get; set; }
    public bool ExistingIsVideo { get; set; }
    public double FocalX { get; set; } = 50;
    public double FocalY { get; set; } = 50;
}
