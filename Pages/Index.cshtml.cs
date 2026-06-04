using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Data;
using WorkshopZagreb.Models;
using System.Globalization;

namespace WorkshopZagreb.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Workshop> UpcomingWorkshops { get; set; } = new();

    // Calendar data — keyed by date for O(1) lookup in the Razor template
    public Dictionary<DateTime, List<Workshop>> WorkshopsByDate { get; set; } = new();
    public Dictionary<DateTime, ReservedDay>    ReservedDays    { get; set; } = new();
    public HashSet<DateTime>                    Holidays        { get; set; } = new();

    // Current month metadata
    public DateTime CurrentMonthStart { get; set; }
    public int      CurrentMonthDays  { get; set; }
    public string   CurrentMonthName  { get; set; } = "";
    public int      CurrentYear       { get; set; }

    // Next month metadata
    public DateTime NextMonthStart { get; set; }
    public int      NextMonthDays  { get; set; }
    public string   NextMonthName  { get; set; } = "";
    public int      NextYear       { get; set; }

    public async Task OnGetAsync()
    {
        var en = new CultureInfo("en-US");

        // Current month
        var today = DateTime.Today;
        CurrentMonthStart = new DateTime(today.Year, today.Month, 1);
        CurrentMonthDays  = DateTime.DaysInMonth(today.Year, today.Month);
        CurrentMonthName  = en.DateTimeFormat.GetMonthName(today.Month);
        CurrentYear       = today.Year;

        // Next month
        NextMonthStart = CurrentMonthStart.AddMonths(1);
        NextMonthDays  = DateTime.DaysInMonth(NextMonthStart.Year, NextMonthStart.Month);
        NextMonthName  = en.DateTimeFormat.GetMonthName(NextMonthStart.Month);
        NextYear       = NextMonthStart.Year;

        var rangeEnd = NextMonthStart.AddMonths(1);

        // Croatian public holidays for both months (may span a year boundary)
        Holidays = CroatianHolidays(CurrentMonthStart.Year);
        if (NextMonthStart.Year != CurrentMonthStart.Year)
            foreach (var h in CroatianHolidays(NextMonthStart.Year))
                Holidays.Add(h);

        // Workshops in the two-month window
        var workshops = (await _db.Workshops
            .Include(w => w.Photos)
            .Where(w => !w.IsArchived && w.Date >= CurrentMonthStart && w.Date < rangeEnd)
            .OrderBy(w => w.Date)
            .ToListAsync())
            .OrderBy(w => w.Date).ThenBy(w => w.StartTime)
            .ToList();

        WorkshopsByDate = workshops
            .GroupBy(w => w.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Admin-reserved days in the two-month window
        ReservedDays = (await _db.ReservedDays
            .Where(r => r.Date >= CurrentMonthStart && r.Date < rangeEnd)
            .ToListAsync())
            .ToDictionary(r => r.Date.Date);

        // Scroll strip — upcoming only, max 8
        UpcomingWorkshops = await _db.Workshops
            .Include(w => w.Photos)
            .Where(w => !w.IsArchived && w.Date >= today)
            .OrderBy(w => w.Date)
            .Take(8)
            .ToListAsync();
    }

    // ----------------------------------------------------------------
    // Croatian public holidays for a given year.
    // Fixed dates + Easter-based moveable feasts.
    // Source: Zakon o blagdanima, spomendanima i neradnim danima (NN 33/96)
    // ----------------------------------------------------------------
    private static HashSet<DateTime> CroatianHolidays(int year)
    {
        var easter = CalculateEaster(year);

        return new HashSet<DateTime>
        {
            new(year, 1,  1),          // Nova godina
            new(year, 1,  6),          // Bogojavljenje / Tri kralja
            easter,                    // Uskrs (nedjelja)
            easter.AddDays(1),         // Uskrsni ponedjeljak
            new(year, 5,  1),          // Međunarodni praznik rada
            new(year, 5, 30),          // Dan državnosti
            easter.AddDays(60),        // Tijelovo (Corpus Christi)
            new(year, 6, 22),          // Dan antifašističke borbe
            new(year, 8,  5),          // Dan pobjede i domovinske zahvalnosti
            new(year, 8, 15),          // Velika Gospa (Uznesenje Blažene Djevice Marije)
            new(year, 11, 1),          // Svi sveti
            new(year, 11, 18),         // Dan sjećanja na žrtve Domovinskog rata
            new(year, 12, 25),         // Božić
            new(year, 12, 26),         // Sveti Stjepan
        };
    }

    // Meeus / Jones / Butcher algorithm — accurate for 1900–2099
    private static DateTime CalculateEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day   = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
