using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using WorkshopZagreb.Data;

namespace WorkshopZagreb.Pages;

public class SitemapModel : PageModel
{
    private readonly AppDbContext _db;
    public SitemapModel(AppDbContext db) => _db = db;

    public async Task<IActionResult> OnGetAsync()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var staticPages = new[]
        {
            ("",           "1.0", "weekly"),
            ("/workshops", "0.9", "daily"),
            ("/about",     "0.7", "monthly"),
            ("/suradnja",  "0.7", "monthly"),
            ("/menu",      "0.6", "monthly"),
            ("/faq",       "0.6", "monthly"),
            ("/gallery",   "0.6", "monthly"),
        };

        var workshops = await _db.Workshops
            .Where(w => !w.IsArchived && !w.IsPinned && w.Date >= DateTime.Today)
            .Select(w => new { w.Slug, w.Date })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var (path, priority, freq) in staticPages)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}{path}</loc>");
            sb.AppendLine($"    <lastmod>{today}</lastmod>");
            sb.AppendLine($"    <changefreq>{freq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");
            sb.AppendLine("  </url>");
        }

        foreach (var w in workshops)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}/workshops/{w.Slug}</loc>");
            sb.AppendLine($"    <lastmod>{w.Date:yyyy-MM-dd}</lastmod>");
            sb.AppendLine($"    <changefreq>monthly</changefreq>");
            sb.AppendLine($"    <priority>0.8</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}
