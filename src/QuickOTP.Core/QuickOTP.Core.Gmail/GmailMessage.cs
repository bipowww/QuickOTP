using System;

namespace QuickOTP.Core.Gmail;

public sealed class GmailMessage
{
	public required string Id { get; init; }

	public string? ThreadId { get; init; }

	public string? Subject { get; init; }

	public string? From { get; init; }

	public string? PlainBody { get; init; }

	public string? HtmlBody { get; init; }

	public DateTimeOffset? InternalDate { get; init; }

	public DateTimeOffset? FetchStartedAt { get; init; }

	public DateTimeOffset? FetchEndedAt { get; init; }
}
