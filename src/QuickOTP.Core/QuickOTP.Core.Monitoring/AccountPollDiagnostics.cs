using System;

namespace QuickOTP.Core.Monitoring;

public readonly record struct AccountPollDiagnostics(string AccountKey, string SourceKind, string Mode, TimeSpan CurrentInterval, int RequestsLastMinute, int RequestsLastHour, int HistoryListLastMinute, int MessagesGetLastMinute, bool InBudgetCooldown);
