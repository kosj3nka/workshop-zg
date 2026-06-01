namespace WorkshopZagreb.Models;

// A specific calendar date marked as reserved / unavailable.
// Admin creates these — they show as lightly tinted cells in the public calendar.
public class ReservedDay
{
    public int Id { get; set; }

    // The date (time part is ignored — whole day is reserved)
    public DateTime Date { get; set; }

    // Short label shown inside the calendar cell, e.g. "Private event", "Closed"
    public string? Label { get; set; }
}
