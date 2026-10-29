using System;

namespace QuickOTP.Core.Models;

public sealed class HistoryEntry
{
	public required DateTimeOffset Timestamp { get; init; }

	public required string Code { get; init; }

	public string? Sender { get; init; }

	public string? Service { get; init; }

	public string? MessageId { get; init; }

	public string? AccountId { get; init; }

	public string? AccountEmail { get; init; }

	public MessageSourceKind Source { get; init; }

	public DateTimeOffset? ExpiresAt { get; init; }

	public bool MarkedFalsePositive { get; set; }
}
