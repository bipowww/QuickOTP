using System;
using System.Collections.Generic;

namespace QuickOTP.Core.Gmail;

public sealed record GmailPollResult(IReadOnlyList<GmailMessage> Messages, ulong HistoryId, bool IsBaseline, TimeSpan Duration = default(TimeSpan), int ApiCalls = 0, bool WasHistoryRecovery = false, DateTimeOffset PollStartedAt = default(DateTimeOffset), DateTimeOffset? HistoryResponseAt = null, int HistoryListCalls = 0, int MessagesGetCalls = 0);
