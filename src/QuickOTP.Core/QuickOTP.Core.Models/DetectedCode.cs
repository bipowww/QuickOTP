using System;

namespace QuickOTP.Core.Models;

public sealed class DetectedCode
{
	public required string Code { get; init; }

	public required string MessageId { get; init; }

	public required DateTimeOffset DetectedAt { get; init; }

	public string? Sender { get; init; }

	public string? Service { get; init; }

	public string? Subject { get; init; }

	public int Score { get; init; }

	public string? AccountId { get; init; }

	public MessageSourceKind Source { get; init; }

	public DateTimeOffset? ExpiresAt { get; init; }

	public string? AccountEmail { get; init; }

	public static DetectedCode From(OtpDetectionResult r)
	{
		return new DetectedCode
		{
			Code = r.Code,
			MessageId = r.MessageId,
			DetectedAt = r.ReceivedAt,
			Sender = r.Sender,
			Service = r.Service,
			Subject = r.Subject,
			Score = r.Confidence,
			AccountId = r.AccountId,
			Source = r.Source,
			ExpiresAt = r.ExpiresAt,
			AccountEmail = r.AccountEmail
		};
	}
}
