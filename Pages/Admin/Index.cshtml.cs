using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Admin;

public class AdminIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public AdminIndexModel(AppDbContext db) => _db = db;

    public List<Workshop> UpcomingWorkshops { get; set; } = new();
    public List<Workshop> PastWorkshops     { get; set; } = new();
    public List<Workshop> PinnedWorkshops   { get; set; } = new();

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var all = await _db.Workshops.OrderBy(w => w.Date).ToListAsync();
        PinnedWorkshops   = all.Where(w => !w.IsArchived && w.IsPinned).ToList();
        UpcomingWorkshops = all.Where(w => w.IsUpcoming).ToList();
        PastWorkshops     = all.Where(w => !w.IsPinned && !w.IsUpcoming).OrderByDescending(w => w.Date).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id)
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var workshop = await _db.Workshops.FindAsync(id);
        if (workshop != null) { workshop.IsArchived = true; await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var workshop = await _db.Workshops.Include(w => w.Photos).FirstOrDefaultAsync(w => w.Id == id);
        if (workshop != null) { _db.Workshops.Remove(workshop); await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    // Kept for backward compat in case any old link still calls it
    public Task<IActionResult> OnPostDeletePinnedAsync(int id) => OnPostDeleteAsync(id);
}
