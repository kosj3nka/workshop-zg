using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Admin;

public class ReservedModel : PageModel
{
    private readonly AppDbContext _db;
    public ReservedModel(AppDbContext db) => _db = db;

    public List<ReservedDay> Reserved { get; set; } = new();

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        Reserved = await _db.ReservedDays
            .OrderBy(r => r.Date)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(DateTime date, string? label)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        // Avoid duplicates for the same date
        bool exists = await _db.ReservedDays.AnyAsync(r => r.Date.Date == date.Date);
        if (!exists)
        {
            _db.ReservedDays.Add(new ReservedDay
            {
                Date  = date.Date,
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var day = await _db.ReservedDays.FindAsync(id);
        if (day != null)
        {
            _db.ReservedDays.Remove(day);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
