using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkshopZagreb.Pages.Admin;

// IMPORTANT: Delete this page or restrict it after setting the password once.
// It only exists to make the first-time setup easy.
public class SetPasswordModel : PageModel
{
    public string? Hash { get; set; }

    public void OnGet() { }

    public void OnPost(string password)
    {
        Hash = BCrypt.Net.BCrypt.HashPassword(password);
    }
}
