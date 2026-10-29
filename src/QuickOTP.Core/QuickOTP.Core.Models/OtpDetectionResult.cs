using System;
using QuickOTP.Core.Monitoring;

namespace QuickOTP.Core.Models;

public sealed class OtpDetectionResult
{
	public required string Code { get; init; }

	public required int Confidence { get; init; }

	public string? Service { get; init; }

	public string? Sender { get; init; }

	public required string MessageId { get; init; }

	public required string AccountId { get; init; }

	public required MessageSourceKind Source { get; init; }

	public required DateTimeOffset ReceivedAt { get; init; }

	public DateTimeOffset? ExpiresAt { get; init; }

	public string? AccountEmail { get; init; }

	public string? Subject { get; init; }

	public OtpLatencyMeasurement? Latency { get; set; }

	public bool IsExpired
	{
		get
		{
			if (ExpiresAt.HasValue)
			{
				DateTimeOffset now = DateTimeOffset.Now;
				DateTimeOffset? expiresAt = ExpiresAt;
				return now >= expiresAt;
			}
			return false;
		}
	}

	public bool IsValid
	{
		get
		{
			if (!IsExpired)
			{
				return Confidence >= 60;
			}
			return false;
		}
	}
}
