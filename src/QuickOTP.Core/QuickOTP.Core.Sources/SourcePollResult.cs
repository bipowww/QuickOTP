using System;
using System.Collections.Generic;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Sources;

public sealed class SourcePollResult
{
	public static SourcePollResult Empty { get; } = new SourcePollResult
	{
		Messages = Array.Empty<MessageCandidate>()
	};

	public required IReadOnlyList<MessageCandidate> Messages { get; init; }

	public TimeSpan Duration { get; init; }

	public int ApiCalls { get; init; }

	public int HistoryListCalls { get; init; }

	public int MessagesGetCalls { get; init; }

	public bool IsBaseline { get; init; }

	public bool WasHistoryRecovery { get; init; }

	public bool ApiBudgetExceeded { get; init; }

	public DateTimeOffset? PollStartedAt { get; init; }

	public DateTimeOffset? HistoryResponseAt { get; init; }

	public bool HadChanges => Messages.Count > 0;
}
