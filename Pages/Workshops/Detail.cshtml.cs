using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Workshops;

public class WorkshopDetailModel : PageModel
{
    private readonly AppDbContext _db;
    public WorkshopDetailModel(AppDbContext db) => _db = db;

    public Workshop? Workshop { get; set; }
    public List<WorkshopOccurrence> UpcomingOccurrences { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Workshop = await _db.Workshops
            .Include(w => w.Photos)
            .Include(w => w.Occurrences)
            .FirstOrDefaultAsync(w => w.Slug == slug && !w.IsArchived);

        if (Workshop == null)
            return NotFound();

        UpcomingOccurrences = Workshop.Occurrences
            .Where(o => o.Date >= DateTime.Today)
            .OrderBy(o => o.Date)
            .ToList();

        return Page();
    }
}
