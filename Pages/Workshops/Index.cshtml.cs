using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Workshops;

public class WorkshopsIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public WorkshopsIndexModel(AppDbContext db) => _db = db;

    public List<Workshop> PinnedWorkshops { get; set; } = new();
    public List<Workshop> Workshops { get; set; } = new();

    public async Task OnGetAsync()
    {
        PinnedWorkshops = await _db.Workshops
            .Where(w => w.IsPinned && !w.IsArchived)
            .OrderBy(w => w.Name)
            .ToListAsync();

        Workshops = await _db.Workshops
            .Include(w => w.Photos)
            .Where(w => !w.IsPinned && !w.IsArchived && w.Date >= DateTime.Today)
            .OrderBy(w => w.Date)
            .ToListAsync();
    }
}
