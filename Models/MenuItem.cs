namespace WorkshopZagreb.Models;

// A single line on the menu, e.g. "Cappuccino — 2,70 €".
// Belongs to one MenuCategory (e.g. "Kava").
public class MenuItem
{
    public int Id { get; set; }

    public int MenuCategoryId { get; set; }
    public MenuCategory? Category { get; set; }

    public required string Name { get; set; }

    public decimal Price { get; set; }

    // Optional list of ingredients/notes, e.g. "Mlijeko, kakao, cimet"
    public string? Ingredients { get; set; }

    // Addons (e.g. "Bademovo mlijeko +0,50 €") are shown smaller, at the
    // bottom of their category, with a "+" in front of the price.
    public bool IsAddon { get; set; }

    public int DisplayOrder { get; set; }
}
