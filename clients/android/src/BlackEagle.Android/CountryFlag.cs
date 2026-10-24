using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

#if DESKTOP_SHARED
namespace BlackEagle.Client.UI;
#else
namespace BlackEagle.Android;
#endif

internal sealed class CountryFlag : Control
{
    private readonly string country;

    /// <summary>Сохраняет код страны сервера для отрисовки независимого от emoji флага.</summary>
    public CountryFlag(string country) { this.country = country.ToUpperInvariant(); }

    /// <summary>Рисует флаги поддерживаемых локаций; для остальных использует региональный символ Unicode.</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        using var clip = context.PushGeometryClip(new EllipseGeometry(bounds));
        if (country is "DE" or "NL" or "RU" or "FR" or "IT" or "BE" or "AT")
        {
            var colors = country switch
            {
                "DE" => new[] { "#171717", "#E32932", "#FFCF32" },
                "NL" => new[] { "#D9303B", "#FFFFFF", "#225FA9" },
                "RU" => new[] { "#FFFFFF", "#245DC0", "#D8323D" },
                "FR" => new[] { "#245DC0", "#FFFFFF", "#E53945" },
                "IT" => new[] { "#18975B", "#FFFFFF", "#E53945" },
                "BE" => new[] { "#171717", "#FFCF32", "#E53945" },
                _ => new[] { "#D9303B", "#FFFFFF", "#D9303B" }
            };
            for (var i = 0; i < 3; i++)
                context.FillRectangle(new SolidColorBrush(Color.Parse(colors[i])), country is "FR" or "IT" or "BE"
                    ? new Rect(i * bounds.Width / 3, 0, bounds.Width / 3 + 1, bounds.Height)
                    : new Rect(0, i * bounds.Height / 3, bounds.Width, bounds.Height / 3 + 1));
        }
        else if (country is "FI" or "SE" or "DK")
        {
            IBrush background = country == "FI" ? Brushes.White : new SolidColorBrush(Color.Parse(country == "SE" ? "#2467A6" : "#CE273A"));
            var cross = new SolidColorBrush(Color.Parse(country == "FI" ? "#2458A3" : country == "SE" ? "#FFD340" : "#FFFFFF"));
            context.FillRectangle(background, bounds);
            context.FillRectangle(cross, new Rect(bounds.Width * .29, 0, bounds.Width * .19, bounds.Height));
            context.FillRectangle(cross, new Rect(0, bounds.Height * .41, bounds.Width, bounds.Height * .19));
        }
        else
        {
            var valid = country.Length == 2 && country.All(c => c is >= 'A' and <= 'Z');
            var text = valid ? string.Concat(country.Select(c => char.ConvertFromUtf32(0x1F1E6 + c - 'A'))) : "🌐";
            var formatted = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, bounds.Height * .8, Brushes.Gray);
            context.DrawText(formatted, new Point((bounds.Width - formatted.Width) / 2, (bounds.Height - formatted.Height) / 2));
        }
    }
}
