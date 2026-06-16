using System.Net;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Helpers;

public static class GoogleCalendarHelper
{
    public static string BuildAddToCalendarUrl(Workshop w)
    {
        var start = w.Date.Date + w.StartTime;
        var end = w.Date.Date + (w.EndTime ?? w.StartTime.Add(TimeSpan.FromHours(2)));

        string Fmt(DateTime dt) => dt.ToString("yyyyMMddTHHmmss");

        var query = $"action=TEMPLATE" +
                    $"&text={WebUtility.UrlEncode(w.Name)}" +
                    $"&dates={Fmt(start)}/{Fmt(end)}" +
                    $"&details={WebUtility.UrlEncode(w.Description)}" +
                    $"&location={WebUtility.UrlEncode("Workshop Zagreb")}" +
                    $"&ctz=Europe/Zagreb";

        return $"https://www.google.com/calendar/render?{query}";
    }
}
