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
    public List<Workshop> PastWorkshops { get; set; } = new();

    // Helper that every admin page calls to check session before doing anything
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
        UpcomingWorkshops = all.Where(w => w.IsUpcoming).ToList();
        PastWorkshops     = all.Where(w => !w.IsUpcoming).OrderByDescending(w => w.Date).ToList();
        return Page();
    }

    // Handles the delete button form submission (asp-page-handler="Delete")
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var redirect = CheckAuth();
        if (redirect != null) return redirect;

        var workshop = await _db.Workshops
            .Include(w => w.Photos)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workshop != null)
        {
            _db.Workshops.Remove(workshop); // Cascade deletes photos too
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
