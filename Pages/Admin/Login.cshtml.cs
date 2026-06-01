using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkshopZagreb.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    public LoginModel(IConfiguration config) => _config = config;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        // Already logged in? Go straight to admin dashboard
        if (HttpContext.Session.GetString("AdminLoggedIn") == "yes")
            return RedirectToPage("/Admin/Index");
        return Page();
    }

    public IActionResult OnPost(string username, string password)
    {
        var adminUser = _config["Admin:Username"];
        var adminHash = _config["Admin:PasswordHash"];

        // BCrypt.Verify compares the plain password against the stored hash
        // The hash in appsettings.json is generated once with BCrypt.HashPassword()
        bool valid = username == adminUser
                     && BCrypt.Net.BCrypt.Verify(password, adminHash);

        if (valid)
        {
            // Store login flag in server-side session
            HttpContext.Session.SetString("AdminLoggedIn", "yes");
            return RedirectToPage("/Admin/Index");
        }

        ErrorMessage = "Pogrešno korisničko ime ili lozinka.";
        return Page();
    }
}
