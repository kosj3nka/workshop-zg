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

    // Splitting by category count alone can badly unbalance the two columns when
    // categories have very different item counts (e.g. "Kava" with 14 items vs
    // "Voda" with 2). Instead, greedily assign each category to whichever column
    // currently has fewer items, so both columns end up roughly the same height.
    public static (List<MenuCategory> Left, List<MenuCategory> Right) SplitIntoBalancedColumns(List<MenuCategory> categories)
    {
        var left = new List<MenuCategory>();
        var right = new List<MenuCategory>();
        int leftWeight = 0, rightWeight = 0;

        foreach (var cat in categories)
        {
            int weight = cat.Items.Count + 1; // +1 for the category title/header
            if (leftWeight <= rightWeight)
            {
                left.Add(cat);
                leftWeight += weight;
            }
            else
            {
                right.Add(cat);
                rightWeight += weight;
            }
        }

        return (left, right);
    }
}
