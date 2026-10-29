using System;

namespace QuickOTP.Core.Rules;

public sealed class FalsePositiveRecord
{
	public string? Sender { get; set; }

	public string? Domain { get; set; }

	public string? CodePattern { get; set; }

	public string? ContextSnippet { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
