namespace WorkshopZagreb.Models;

// One slide in the homepage promo carousel — a banner photo with an optional
// centered heading. DisplayOrder controls the slide sequence in the carousel.
public class Promo
{
    public int Id { get; set; }

    public required string ImageUrl { get; set; }
    public bool IsVideo { get; set; }
    public string? Heading { get; set; }
    public string? Subheading { get; set; }

    // Where the crop is centered when the banner's wide aspect ratio cuts off
    // part of the photo — 0-100, CSS object-position percentages. Admin sets
    // this by dragging the photo in the preview; defaults to dead center.
    public double FocalX { get; set; } = 50;
    public double FocalY { get; set; } = 50;

    public int DisplayOrder { get; set; }
}
