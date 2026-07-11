namespace WorkshopZagreb.Models;

// This is the data model — EF Core turns this class into a database table.
// Workshop is the shared template: name, description, images, host, price.
// Specific dates live on WorkshopOccurrence (see that file).
public class Workshop
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? BannerUrl { get; set; }       // main card/hero image (required via form)
    public string? LogoUrl { get; set; }         // small square profile icon (optional)
    public required string InstagramPostUrl { get; set; }

    public string? HostName { get; set; }
    public string? HostInstagram { get; set; }
    public string? HostWebsite { get; set; }
    // Free text, not a number — lets admins enter "25 €", "20-35 €", "Po dogovoru", etc.
    public string? Price { get; set; }
    public int? MaxParticipants { get; set; }

    // Slug is the URL-friendly version of the name: "Watercolour Basics" -> "watercolour-basics"
    // Used in the URL: /workshops/watercolour-basics
    public required string Slug { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Manually hidden by admin — stays in archive tab regardless of dates
    public bool IsArchived { get; set; }

    // Reservable workshops are booked as a group for no fixed date (e.g. a
    // birthday party) — shown with a single Book button instead of a date/time,
    // and have zero WorkshopOccurrence rows. BookingType is "email" or "webpage".
    public bool IsReservable { get; set; }
    public string? BookingType { get; set; }
    public string? BookingValue { get; set; }

    // A workshop can have multiple photos and multiple dates (occurrences)
    public List<WorkshopPhoto> Photos { get; set; } = new();
    public List<WorkshopOccurrence> Occurrences { get; set; } = new();
}
