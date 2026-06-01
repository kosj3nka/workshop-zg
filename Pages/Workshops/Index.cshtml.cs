using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Workshops;

public class WorkshopsIndexModel : PageModel
{
    private readonly AppDbContext _db;
    public WorkshopsIndexModel(AppDbContext db) => _db = db;

    public List<Workshop> Workshops { get; set; } = new();

    public async Task OnGetAsync()
    {
        Workshops = await _db.Workshops
            .Include(w => w.Photos)
            .Where(w => w.Date >= DateTime.Today)
            .OrderBy(w => w.Date)
            .ToListAsync();
    }
}
