using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Workshops;

public class WorkshopsIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public WorkshopsIndexModel(AppDbContext db) => _db = db;

    public List<Workshop> ReservableWorkshops { get; set; } = new();
    public List<Workshop> Workshops { get; set; } = new();
    public Dictionary<int, WorkshopOccurrence> NextOccurrenceByWorkshopId { get; set; } = new();
    public Dictionary<int, int> UpcomingOccurrenceCountByWorkshopId { get; set; } = new();

    public async Task OnGetAsync()
    {
        var today = DateTime.Today;

        ReservableWorkshops = await _db.Workshops
            .Where(w => w.IsReservable && !w.IsArchived)
            .OrderBy(w => w.Name)
            .ToListAsync();

        var candidates = await _db.Workshops
            .Include(w => w.Photos)
            .Include(w => w.Occurrences)
            .Where(w => !w.IsReservable && !w.IsArchived)
            .ToListAsync();

        Workshops = candidates
            .Where(w => w.Occurrences.Any(o => o.Date >= today))
            .OrderBy(w => w.Occurrences.Where(o => o.Date >= today).Min(o => o.Date))
            .ToList();

        foreach (var w in Workshops)
        {
            var upcoming = w.Occurrences.Where(o => o.Date >= today).OrderBy(o => o.Date).ToList();
            NextOccurrenceByWorkshopId[w.Id] = upcoming.First();
            UpcomingOccurrenceCountByWorkshopId[w.Id] = upcoming.Count;
        }
    }
}
