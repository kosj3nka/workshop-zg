using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;

namespace WorkshopZagreb.Pages.Newsletter;

public class UnsubscribeModel : PageModel
{
    private readonly AppDbContext _db;

    public bool Success { get; private set; }
    public bool AlreadyDone { get; private set; }

    public UnsubscribeModel(AppDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return Page();

        var sub = await _db.Subscribers.FirstOrDefaultAsync(s => s.Token == token);
        if (sub == null)
            return Page();

        if (sub.UnsubscribedAt != null)
        {
            AlreadyDone = true;
            return Page();
        }

        sub.UnsubscribedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Success = true;
        return Page();
    }
}
