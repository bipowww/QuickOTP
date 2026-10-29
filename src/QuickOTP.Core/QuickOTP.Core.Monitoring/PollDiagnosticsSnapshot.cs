using System.Collections.Generic;

namespace QuickOTP.Core.Monitoring;

public readonly record struct PollDiagnosticsSnapshot(IReadOnlyList<AccountPollDiagnostics> Accounts, DetailedLatencySnapshot Latency);
