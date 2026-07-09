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
    public List<Workshop> ReservableWorkshops { get; set; } = new();

    // For the "next occurrence" line + "+N termina" count in the Upcoming tab
    public Dictionary<int, WorkshopOccurrence> NextOccurrenceByWorkshopId { get; set; } = new();
    public Dictionary<int, int> UpcomingOccurrenceCountByWorkshopId { get; set; } = new();

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

        var today = DateTime.Today;
        var all = await _db.Workshops.Include(w => w.Occurrences).ToListAsync();

        ReservableWorkshops = all.Where(w => !w.IsArchived && w.IsReservable).ToList();

        var nonReservable = all.Where(w => !w.IsReservable).ToList();
        UpcomingWorkshops = nonReservable
            .Where(w => !w.IsArchived && w.Occurrences.Any(o => o.Date >= today))
            .OrderBy(w => w.Occurrences.Where(o => o.Date >= today).Min(o => o.Date))
            .ToList();
        PastWorkshops = nonReservable
            .Where(w => w.IsArchived || !w.Occurrences.Any(o => o.Date >= today))
            .OrderByDescending(w => w.Occurrences.Any() ? w.Occurrences.Max(o => o.Date) : DateTime.MinValue)
            .ToList();

        foreach (var w in UpcomingWorkshops)
        {
            var upcoming = w.Occurrences.Where(o => o.Date >= today).OrderBy(o => o.Date).ToList();
            NextOccurrenceByWorkshopId[w.Id] = upcoming.First();
            UpcomingOccurrenceCountByWorkshopId[w.Id] = upcoming.Count;
        }

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

        var workshop = await _db.Workshops.Include(w => w.Photos).Include(w => w.Occurrences).FirstOrDefaultAsync(w => w.Id == id);
        if (workshop != null) { _db.Workshops.Remove(workshop); await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }
}
