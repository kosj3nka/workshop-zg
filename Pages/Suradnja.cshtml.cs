using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkshopZagreb.Services;

namespace WorkshopZagreb.Pages;

public class SuradnjaModel : PageModel
{
    private readonly IEmailService _email;
    public SuradnjaModel(IEmailService email) => _email = email;

    public bool Sent { get; set; }

    [BindProperty]
    public InquiryInput Input { get; set; } = new();

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Input.Type))
        {
            ModelState.AddModelError("Input.Type", "Odaberite vrstu upita.");
            return Page();
        }
        if (!ModelState.IsValid) return Page();

        _ = _email.SendInquiryAsync(Input);
        Sent = true;
        return Page();
    }
}

public class InquiryInput
{
    // Common
    public string Name    { get; set; } = "";
    public string Email   { get; set; } = "";
    public string Type    { get; set; } = "";

    // Workshop hosting
    public string? WorkshopTopic        { get; set; }
    public string? WorkshopBio          { get; set; }
    public string? WorkshopSchedule     { get; set; }
    public string? WorkshopParticipants { get; set; }

    // Private event
    public string? EventKind    { get; set; }
    public string? EventDate    { get; set; }
    public string? EventGuests  { get; set; }
    public string? EventNotes   { get; set; }

    // Brand / marketing
    public string? BrandName       { get; set; }
    public string? BrandPlacements { get; set; }
    public string? BrandMessage    { get; set; }

    // Other
    public string? OtherMessage { get; set; }
}
