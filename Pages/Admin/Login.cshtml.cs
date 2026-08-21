using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkshopZagreb.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    public LoginModel(IConfiguration config) => _config = config;

    public string? ErrorMessage { get; set; }

    // In-memory per-IP lockout: single admin account, single app instance, so a
    // static dictionary is enough — no need for a DB table or distributed cache.
    // Resets on app restart, which is an acceptable tradeoff for this app's threat model.
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime? LockedUntil)> LoginAttempts = new();

    public IActionResult OnGet()
    {
        // Already logged in? Go straight to admin dashboard
        if (HttpContext.Session.GetString("AdminLoggedIn") == "yes")
            return RedirectToPage("/Admin/Index");
        return Page();
    }

    public IActionResult OnPost(string username, string password)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (LoginAttempts.TryGetValue(clientIp, out var state) && state.LockedUntil is DateTime lockedUntil)
        {
            if (DateTime.UtcNow < lockedUntil)
            {
                var minutesLeft = Math.Ceiling((lockedUntil - DateTime.UtcNow).TotalMinutes);
                ErrorMessage = $"Previše neuspjelih pokušaja. Pokušajte ponovno za {minutesLeft} min.";
                return Page();
            }
            // Lockout expired — reset before evaluating this attempt
            LoginAttempts.TryRemove(clientIp, out _);
        }

        var adminUser = _config["Admin:Username"];
        var adminHash = _config["Admin:PasswordHash"];

        // BCrypt.Verify compares the plain password against the stored hash
        // The hash is set via user secrets locally / app config in Azure — see
        // SetPassword.cshtml. If it's missing or malformed, Verify throws instead
        // of returning false, so guard it explicitly rather than let a config
        // problem surface as an unhandled exception page.
        bool valid = false;
        if (username == adminUser && !string.IsNullOrEmpty(adminHash))
        {
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(password, adminHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                valid = false;
            }
        }

        if (valid)
        {
            LoginAttempts.TryRemove(clientIp, out _);
            // Store login flag in server-side session
            HttpContext.Session.SetString("AdminLoggedIn", "yes");
            return RedirectToPage("/Admin/Index");
        }

        var attempts = LoginAttempts.AddOrUpdate(
            clientIp,
            _ => (1, null),
            (_, existing) => (existing.Attempts + 1, null));

        if (attempts.Attempts >= MaxAttempts)
        {
            LoginAttempts[clientIp] = (attempts.Attempts, DateTime.UtcNow.Add(LockoutDuration));
            ErrorMessage = $"Previše neuspjelih pokušaja. Pokušajte ponovno za {LockoutDuration.TotalMinutes:0} min.";
        }
        else
        {
            ErrorMessage = "Pogrešno korisničko ime ili lozinka.";
        }

        return Page();
    }
}
