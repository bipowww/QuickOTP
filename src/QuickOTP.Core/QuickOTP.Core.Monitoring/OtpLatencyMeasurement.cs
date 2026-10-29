using System;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Monitoring;

public sealed class OtpLatencyMeasurement
{
	public MessageSourceKind Source { get; init; }

	public string AccountKey { get; init; } = "";

	public DateTimeOffset? EmailReceivedAt { get; set; }

	public DateTimeOffset? PollStartedAt { get; set; }

	public DateTimeOffset? HistoryResponseAt { get; set; }

	public DateTimeOffset? MessageFetchStartedAt { get; set; }

	public DateTimeOffset? MessageFetchedAt { get; set; }

	public DateTimeOffset? OtpDetectedAt { get; set; }

	public DateTimeOffset? ClipboardCopiedAt { get; set; }

	public TimeSpan? GmailExposureLatency => Diff(HistoryResponseAt, EmailReceivedAt);

	public TimeSpan? HistoryApiLatency => Diff(HistoryResponseAt, PollStartedAt);

	public TimeSpan? MessageFetchLatency => Diff(MessageFetchedAt, MessageFetchStartedAt);

	public TimeSpan? OtpProcessingLatency => Diff(OtpDetectedAt, MessageFetchedAt);

	public TimeSpan? ClipboardLatency => Diff(ClipboardCopiedAt, OtpDetectedAt);

	public TimeSpan? TotalDetectionLatency => Diff(ClipboardCopiedAt, EmailReceivedAt);

	private static TimeSpan? Diff(DateTimeOffset? end, DateTimeOffset? start)
	{
		if (!end.HasValue || !start.HasValue)
		{
			return null;
		}
		TimeSpan timeSpan = end.Value - start.Value;
		if (!(timeSpan < TimeSpan.Zero))
		{
			return timeSpan;
		}
		return null;
	}

	public static string AnonymizeAccountId(string accountId)
	{
		if (string.IsNullOrEmpty(accountId))
		{
			return "acc_unknown";
		}
		int hashCode = accountId.GetHashCode(StringComparison.Ordinal);
		return "acc_" + Math.Abs(hashCode % 10000).ToString("D4");
	}
}
