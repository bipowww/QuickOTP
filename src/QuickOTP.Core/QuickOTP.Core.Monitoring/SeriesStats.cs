namespace QuickOTP.Core.Monitoring;

public readonly record struct SeriesStats(double? AverageMs, double? P50Ms, double? P90Ms, double? P95Ms, double? P99Ms, double? MinMs, double? MaxMs, int Count)
{
	public static SeriesStats Empty { get; } = new SeriesStats(null, null, null, null, null, null, null, 0);
}
