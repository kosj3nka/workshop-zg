using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopZagreb.Services;

public static class SlugHelper
{
    // Turns "Akvarel za početnike!" into "akvarel-za-pocetnike"
    // Handles Croatian characters (č,ć,š,ž,đ) correctly
    public static string Generate(string title)
    {
        var str = title.ToLowerInvariant().Trim();

        // Replace Croatian characters
        str = str.Replace('č', 'c').Replace('ć', 'c')
                 .Replace('š', 's').Replace('ž', 'z')
                 .Replace('đ', 'd');

        // Replace spaces and special chars with hyphens
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"[\s]+", "-");
        str = str.Trim('-');

        return str;
    }
}
