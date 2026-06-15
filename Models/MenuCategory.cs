namespace WorkshopZagreb.Models;

// The two top-level menu sections. Fixed on purpose — admins can add/edit/delete
// the categories *inside* each of these (e.g. "Kava", "Sokovi", "Burgeri"...),
// but cannot rename or remove "Pića" / "Hrana" themselves.
public enum MainMenuCategory
{
    Pica = 0,
    Hrana = 1
}

// A menu subcategory shown as one block on the Meni page, e.g. "Kava / Broom Coffee Roasters".
// Always belongs to one of the two fixed MainMenuCategory values above.
public class MenuCategory
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public MainMenuCategory MainCategory { get; set; }

    // Controls the order categories appear on the page / in the admin list
    public int DisplayOrder { get; set; }

    public List<MenuItem> Items { get; set; } = new();
}
