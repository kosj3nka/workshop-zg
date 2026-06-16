namespace WorkshopZagreb.Models;

public class PinnedWorkshop
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? Subtitle { get; set; }
    public string? BannerUrl { get; set; }
    public decimal? StartingPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
