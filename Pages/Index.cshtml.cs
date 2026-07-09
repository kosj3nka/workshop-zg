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
    public List<Workshop> ReservableWorkshops { get; set; } = new();
    public Dictionary<int, WorkshopOccurrence> NextOccurrenceByWorkshopId { get; set; } = new();

    public Dictionary<DateTime, List<(Workshop Workshop, WorkshopOccurrence Occurrence)>> WorkshopsByDate { get; set; } = new();
    public Dictionary<DateTime, ReservedDay> ReservedDays { get; set; } = new();
    public HashSet<DateTime> Holidays { get; set; } = new();

    public DateTime CurrentMonthStart { get; set; }
    public int      CurrentMonthDays  { get; set; }
    public string   CurrentMonthName  { get; set; } = "";
    public int      CurrentYear       { get; set; }

    public DateTime NextMonthStart { get; set; }
    public int      NextMonthDays  { get; set; }
    public string   NextMonthName  { get; set; } = "";
    public int      NextYear       { get; set; }

    public async Task OnGetAsync()
    {
        var en = new CultureInfo("en-US");
        var today = DateTime.Today;

        CurrentMonthStart = new DateTime(today.Year, today.Month, 1);
        CurrentMonthDays  = DateTime.DaysInMonth(today.Year, today.Month);
        CurrentMonthName  = en.DateTimeFormat.GetMonthName(today.Month);
        CurrentYear       = today.Year;

        NextMonthStart = CurrentMonthStart.AddMonths(1);
        NextMonthDays  = DateTime.DaysInMonth(NextMonthStart.Year, NextMonthStart.Month);
        NextMonthName  = en.DateTimeFormat.GetMonthName(NextMonthStart.Month);
        NextYear       = NextMonthStart.Year;

        var rangeEnd = NextMonthStart.AddMonths(1);

        Holidays = CroatianHolidays(CurrentMonthStart.Year);
        if (NextMonthStart.Year != CurrentMonthStart.Year)
            foreach (var h in CroatianHolidays(NextMonthStart.Year))
                Holidays.Add(h);

        // Calendar grid: non-reservable workshops with an occurrence in the visible 2-month window
        var workshopsWithOccurrences = await _db.Workshops
            .Include(w => w.Photos)
            .Include(w => w.Occurrences)
            .Where(w => !w.IsArchived && !w.IsReservable)
            .ToListAsync();

        var calendarEntries = workshopsWithOccurrences
            .SelectMany(w => w.Occurrences
                .Where(o => o.Date >= CurrentMonthStart && o.Date < rangeEnd)
                .Select(o => (Workshop: w, Occurrence: o)))
            .OrderBy(e => e.Occurrence.Date).ThenBy(e => e.Occurrence.StartTime)
            .ToList();

        WorkshopsByDate = calendarEntries
            .GroupBy(e => e.Occurrence.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        ReservedDays = (await _db.ReservedDays
            .Where(r => r.Date >= CurrentMonthStart && r.Date < rangeEnd)
            .ToListAsync())
            .ToDictionary(r => r.Date.Date);

        // Upcoming Workshops strip: non-reservable workshops with a date in the next 2 months, capped at 8
        var twoMonthsOut = today.AddMonths(2);
        UpcomingWorkshops = workshopsWithOccurrences
            .Where(w => w.Occurrences.Any(o => o.Date >= today && o.Date < twoMonthsOut))
            .OrderBy(w => w.Occurrences.Where(o => o.Date >= today && o.Date < twoMonthsOut).Min(o => o.Date))
            .Take(8)
            .ToList();

        foreach (var w in UpcomingWorkshops)
        {
            NextOccurrenceByWorkshopId[w.Id] = w.Occurrences
                .Where(o => o.Date >= today)
                .OrderBy(o => o.Date)
                .First();
        }

        // Reservable workshops appended after the dated ones in the same strip
        ReservableWorkshops = await _db.Workshops
            .Where(w => w.IsReservable && !w.IsArchived)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    private static HashSet<DateTime> CroatianHolidays(int year)
    {
        var easter = CalculateEaster(year);

        return new HashSet<DateTime>
        {
            new(year, 1,  1),
            new(year, 1,  6),
            easter,
            easter.AddDays(1),
            new(year, 5,  1),
            new(year, 5, 30),
            easter.AddDays(60),
            new(year, 6, 22),
            new(year, 8,  5),
            new(year, 8, 15),
            new(year, 11, 1),
            new(year, 11, 18),
            new(year, 12, 25),
            new(year, 12, 26),
        };
    }

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
