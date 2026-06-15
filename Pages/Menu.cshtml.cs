using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages;

public class MenuModel : PageModel
{
    private readonly AppDbContext _db;
    public MenuModel(AppDbContext db) => _db = db;

    public List<MenuCategory> DrinkCategories { get; set; } = new();
    public List<MenuCategory> FoodCategories { get; set; } = new();

    public async Task OnGetAsync()
    {
        var categories = await _db.MenuCategories
            .Include(c => c.Items)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        foreach (var c in categories)
            c.Items = c.Items.OrderBy(i => i.DisplayOrder).ToList();

        DrinkCategories = categories.Where(c => c.MainCategory == MainMenuCategory.Pica).ToList();
        FoodCategories  = categories.Where(c => c.MainCategory == MainMenuCategory.Hrana).ToList();
    }
}
