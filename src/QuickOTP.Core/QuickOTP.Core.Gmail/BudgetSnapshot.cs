using System;

namespace QuickOTP.Core.Gmail;

public readonly record struct BudgetSnapshot(int HistoryListLastMinute, int MessagesGetLastMinute, int TotalLastMinute, int TotalLastHour, bool InCooldown, TimeSpan? CooldownRemaining);
