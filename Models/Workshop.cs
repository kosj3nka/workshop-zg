namespace WorkshopZagreb.Models;

// This is the data model — EF Core turns this class into a database table.
// Every property becomes a column. [Required] ones can't be null in the DB.
public class Workshop
{
    public int Id { get; set; }

    // --- Required fields (owner must fill in) ---
    public required string Name { get; set; }
    public DateTime Date { get; set; }          // The date of the workshop
    public TimeSpan StartTime { get; set; }     // e.g. 14:00
    public TimeSpan? EndTime { get; set; }      // optional
    public required string Description { get; set; }
    public string? BannerUrl { get; set; }       // main card/hero image (required via form)
    public string? LogoUrl { get; set; }         // small square profile icon (optional)
    public required string InstagramPostUrl { get; set; }

    // --- Optional fields ---
    public string? HostName { get; set; }
    public string? HostInstagram { get; set; }
    public string? HostWebsite { get; set; }
    public string? EntrioUrl { get; set; }      // link to Entrio ticket page
    public decimal? Price { get; set; }
    public int? MaxParticipants { get; set; }

    // Slug is the URL-friendly version of the name: "Watercolour Basics" -> "watercolour-basics"
    // Used in the URL: /workshops/watercolour-basics
    public required string Slug { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Manually hidden by admin — stays in archive tab regardless of date
    public bool IsArchived { get; set; }

    // A workshop can have multiple photos
    public List<WorkshopPhoto> Photos { get; set; } = new();

    // Helper: is this workshop visible as upcoming?
    public bool IsUpcoming => !IsArchived && Date >= DateTime.Today;
}
