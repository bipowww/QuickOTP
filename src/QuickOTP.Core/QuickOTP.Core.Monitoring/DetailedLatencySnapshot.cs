namespace QuickOTP.Core.Monitoring;

public readonly record struct DetailedLatencySnapshot(long TotalPolls, long TotalChanges, long TotalApiCalls, long OtpMeasurements, SeriesStats Total, SeriesStats HistoryApi, SeriesStats MessageFetch, SeriesStats OtpProcessing, SeriesStats Clipboard);
