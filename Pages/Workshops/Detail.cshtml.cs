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

    // {slug} comes from the route defined in the .cshtml: @page "/workshops/{slug}"
    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Workshop = await _db.Workshops
            .Include(w => w.Photos)
            .FirstOrDefaultAsync(w => w.Slug == slug);

        if (Workshop == null)
            return NotFound();

        return Page();
    }
}
