namespace QuickOTP.Core.Monitoring;

public readonly record struct LatencySnapshot(long TotalPolls, long TotalChanges, long TotalApiCalls, double? AverageMs, double? P50Ms, double? P95Ms);
