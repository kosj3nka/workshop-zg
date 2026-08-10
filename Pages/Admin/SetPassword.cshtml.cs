using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkshopZagreb.Pages.Admin;

// Requires an existing admin session (see CheckAuth below) so this can't be used
// by anyone who isn't already logged in to generate a hash and social-engineer
// their way into having it pasted into appsettings.json.
public class SetPasswordModel : PageModel
{
    public string? Hash { get; set; }

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public IActionResult OnGet()
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        return Page();
    }

    public IActionResult OnPost(string password)
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        Hash = BCrypt.Net.BCrypt.HashPassword(password);
        return Page();
    }
}
