using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using System.Text.Json;

namespace WorkshopZagreb.Pages.Api;

[IgnoreAntiforgeryToken]
public class SubscribeModel : PageModel
{
    private readonly AppDbContext _db;
    public SubscribeModel(AppDbContext db) => _db = db;

    public async Task<IActionResult> OnPostAsync()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<SubscribeRequest>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            return new JsonResult(new { ok = false });

        var email = payload.Email.Trim().ToLowerInvariant();
        var existing = await _db.Subscribers.FirstOrDefaultAsync(s => s.Email == email);
        if (existing != null)
            return new JsonResult(new { ok = true });

        _db.Subscribers.Add(new Subscriber
        {
            Email = email,
            Token = Guid.NewGuid().ToString(),
            ConfirmedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true });
    }
}

public record SubscribeRequest(string Email);
