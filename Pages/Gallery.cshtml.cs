using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages;

public class GalleryModel : PageModel
{
    private readonly AppDbContext _db;
    public GalleryModel(AppDbContext db) => _db = db;

    // Show all workshop photos across all workshops
    public List<WorkshopPhoto> Photos { get; set; } = new();

    public async Task OnGetAsync()
    {
        Photos = await _db.WorkshopPhotos
            .OrderByDescending(p => p.Id)
            .ToListAsync();
    }
}
