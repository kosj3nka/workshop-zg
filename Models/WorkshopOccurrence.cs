namespace WorkshopZagreb.Models;

// One specific date/time a workshop runs on. A regular workshop can have
// several of these (e.g. the same class repeated on different weeks).
// Reservable workshops have none — they're dateless by definition.
public class WorkshopOccurrence
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }

    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? EntrioUrl { get; set; }      // link to Entrio ticket page for this date

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsUpcoming => Date >= DateTime.Today;
}
