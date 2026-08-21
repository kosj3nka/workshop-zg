using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using WorkshopZagreb.Services;

namespace WorkshopZagreb.Pages.Admin.Promos;

public class PromoIndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileService _files;

    public PromoIndexModel(AppDbContext db, IFileService files)
    {
        _db = db;
        _files = files;
    }

    public List<Promo> Promos { get; set; } = new();

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        Promos = await _db.Promos.OrderBy(p => p.DisplayOrder).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMoveAsync(int id, string direction)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var siblings = await _db.Promos.OrderBy(p => p.DisplayOrder).ToListAsync();
        int idx = siblings.FindIndex(p => p.Id == id);
        int swapIdx = direction == "up" ? idx - 1 : idx + 1;

        if (idx >= 0 && swapIdx >= 0 && swapIdx < siblings.Count)
        {
            (siblings[idx].DisplayOrder, siblings[swapIdx].DisplayOrder) =
                (siblings[swapIdx].DisplayOrder, siblings[idx].DisplayOrder);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var promo = await _db.Promos.FindAsync(id);
        if (promo != null)
        {
            _files.DeleteImage(promo.ImageUrl);
            _db.Promos.Remove(promo);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
