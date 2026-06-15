using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Pages.Admin.Menu;

// Admin page for managing the Meni (menu) page content.
//
// Structure:
//   MainMenuCategory.Pica / .Hrana  -- the two fixed top-level tabs, can't be added/removed
//     -> MenuCategory                -- subcategories owners create themselves, e.g. "Kava", "Sokovi"
//          -> MenuItem                -- individual menu lines: name, price, optional ingredients
public class MenuAdminModel : PageModel
{
    private readonly AppDbContext _db;
    public MenuAdminModel(AppDbContext db) => _db = db;

    public List<MenuCategory> DrinkCategories { get; set; } = new();
    public List<MenuCategory> FoodCategories { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "pica";

    private IActionResult? CheckAuth()
    {
        if (HttpContext.Session.GetString("AdminLoggedIn") != "yes")
            return RedirectToPage("/Admin/Login");
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var auth = CheckAuth(); if (auth != null) return auth;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
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

    // ================== CATEGORIES ==================

    public async Task<IActionResult> OnPostAddCategoryAsync(MainMenuCategory main, string name, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (!string.IsNullOrWhiteSpace(name))
        {
            var maxOrder = await _db.MenuCategories
                .Where(c => c.MainCategory == main)
                .Select(c => (int?)c.DisplayOrder)
                .MaxAsync() ?? -1;

            _db.MenuCategories.Add(new MenuCategory
            {
                Name = name.Trim(),
                MainCategory = main,
                DisplayOrder = maxOrder + 1
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostRenameCategoryAsync(int categoryId, string name, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var cat = await _db.MenuCategories.FindAsync(categoryId);
        if (cat != null && !string.IsNullOrWhiteSpace(name))
        {
            cat.Name = name.Trim();
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(int categoryId, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var cat = await _db.MenuCategories
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (cat != null)
        {
            _db.MenuCategories.Remove(cat); // cascade deletes its items too
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostMoveCategoryAsync(int categoryId, string direction, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var moving = await _db.MenuCategories.FindAsync(categoryId);
        if (moving != null)
        {
            var siblings = await _db.MenuCategories
                .Where(c => c.MainCategory == moving.MainCategory)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            int idx = siblings.FindIndex(c => c.Id == categoryId);
            int swapIdx = direction == "up" ? idx - 1 : idx + 1;

            if (idx >= 0 && swapIdx >= 0 && swapIdx < siblings.Count)
            {
                (siblings[idx].DisplayOrder, siblings[swapIdx].DisplayOrder) =
                    (siblings[swapIdx].DisplayOrder, siblings[idx].DisplayOrder);
                await _db.SaveChangesAsync();
            }
        }

        return RedirectToPage(new { tab });
    }

    // ================== ITEMS ==================

    public async Task<IActionResult> OnPostAddItemAsync(int categoryId, string name, decimal price, string? ingredients, bool isAddon, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        if (!string.IsNullOrWhiteSpace(name))
        {
            var maxOrder = await _db.MenuItems
                .Where(i => i.MenuCategoryId == categoryId)
                .Select(i => (int?)i.DisplayOrder)
                .MaxAsync() ?? -1;

            _db.MenuItems.Add(new MenuItem
            {
                MenuCategoryId = categoryId,
                Name = name.Trim(),
                Price = price,
                Ingredients = string.IsNullOrWhiteSpace(ingredients) ? null : ingredients.Trim(),
                IsAddon = isAddon,
                DisplayOrder = maxOrder + 1
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostEditItemAsync(int itemId, string name, decimal price, string? ingredients, bool isAddon, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var item = await _db.MenuItems.FindAsync(itemId);
        if (item != null && !string.IsNullOrWhiteSpace(name))
        {
            item.Name = name.Trim();
            item.Price = price;
            item.Ingredients = string.IsNullOrWhiteSpace(ingredients) ? null : ingredients.Trim();
            item.IsAddon = isAddon;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostDeleteItemAsync(int itemId, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var item = await _db.MenuItems.FindAsync(itemId);
        if (item != null)
        {
            _db.MenuItems.Remove(item);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostMoveItemAsync(int itemId, string direction, string tab)
    {
        var auth = CheckAuth(); if (auth != null) return auth;

        var moving = await _db.MenuItems.FindAsync(itemId);
        if (moving != null)
        {
            var siblings = await _db.MenuItems
                .Where(i => i.MenuCategoryId == moving.MenuCategoryId)
                .OrderBy(i => i.DisplayOrder)
                .ToListAsync();

            int idx = siblings.FindIndex(i => i.Id == itemId);
            int swapIdx = direction == "up" ? idx - 1 : idx + 1;

            if (idx >= 0 && swapIdx >= 0 && swapIdx < siblings.Count)
            {
                (siblings[idx].DisplayOrder, siblings[swapIdx].DisplayOrder) =
                    (siblings[swapIdx].DisplayOrder, siblings[idx].DisplayOrder);
                await _db.SaveChangesAsync();
            }
        }

        return RedirectToPage(new { tab });
    }
}
