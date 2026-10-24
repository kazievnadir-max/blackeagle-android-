using System.Globalization;
using System.Text.RegularExpressions;

namespace BlackEagle.Android;

internal static class PingOutput
{
    /// <summary>Вычисляет среднее время только из фактически полученных ICMP-ответов.</summary>
    public static long? AverageMilliseconds(string output)
    {
        var samples = Regex.Matches(output, @"\bicmp_seq=\d+[^\r\n]*\btime[=<]\s*(\d+(?:[.,]\d+)?)\s*ms\b",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(match => double.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture)).ToArray();
        return samples.Length == 0 ? null : (long)Math.Round(samples.Average());
    }
}
