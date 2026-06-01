namespace WorkshopZagreb.Models;

// One workshop can have many photos — this is a separate table linked by WorkshopId
public class WorkshopPhoto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public required string Url { get; set; }
    public int Order { get; set; }  // display order

    public Workshop? Workshop { get; set; }
}

// Newsletter subscribers
public class Subscriber
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }       // set after double opt-in click
    public DateTime? UnsubscribedAt { get; set; }
    public required string Token { get; set; }       // GUID used in confirm/unsubscribe links

    public bool IsActive => ConfirmedAt != null && UnsubscribedAt == null;
}
