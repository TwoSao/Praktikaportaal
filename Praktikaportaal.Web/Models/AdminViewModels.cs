using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Web.Models;

public class StatisticsViewModel
{
    public required ApplicationStatisticsDto Stats { get; init; }
    public required StatisticsQueryParameters Query { get; init; }

    /// <summary>Largest value of a series, used to scale the bars (at least 1, never divide by zero).</summary>
    public static int Max(IEnumerable<int> values) => Math.Max(1, values.DefaultIfEmpty(0).Max());

    /// <summary>Bar width in percent as an invariant string ("33.3"), 0 for no data.</summary>
    public static string Percent(int value, int max) =>
        (max <= 0 ? 0 : Math.Round(value * 100.0 / max, 1)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
