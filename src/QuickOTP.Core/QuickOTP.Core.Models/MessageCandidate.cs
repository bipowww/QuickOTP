using System;

namespace QuickOTP.Core.Models;

public sealed class MessageCandidate
{
	public required string Id { get; init; }

	public required string AccountId { get; init; }

	public required MessageSourceKind Source { get; init; }

	public string? Sender { get; init; }

	public string? SenderName { get; init; }

	public string? Subject { get; init; }

	public string? PlainText { get; init; }

	public string? HtmlText { get; init; }

	public DateTimeOffset? ReceivedAt { get; init; }

	public DateTimeOffset? PollStartedAt { get; init; }

	public DateTimeOffset? HistoryResponseAt { get; init; }

	public DateTimeOffset? MessageFetchStartedAt { get; init; }

	public DateTimeOffset? MessageFetchedAt { get; init; }
}
