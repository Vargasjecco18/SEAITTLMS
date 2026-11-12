namespace SEAITT_LMS.Components.Shared;

/// <summary>One part of a pie or one bar of a chart: what it is called, how much it is, and its color (any CSS color, like <c>var(--success)</c> or <c>#16a34a</c>).</summary>
public sealed record ChartSlice(string Label, double Value, string Color, string? Hint = null);

/// <summary>The colors the charts use, so the same thing has the same color on every page.</summary>
public static class ChartColors
{
    public const string Green = "#16a34a";
    public const string Yellow = "#eab308";
    public const string Red = "#dc2626";
    public const string Blue = "#2563eb";
    public const string Violet = "#7c3aed";
    public const string Teal = "#0d9488";
    public const string Orange = "#ea580c";
    public const string Slate = "#64748b";

    public static readonly string[] Cycle = [Blue, Teal, Violet, Orange, Green, Yellow, Red, Slate];

    public static string At(int index) => Cycle[index % Cycle.Length];
}
