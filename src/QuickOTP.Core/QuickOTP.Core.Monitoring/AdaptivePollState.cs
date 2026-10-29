using System;
using QuickOTP.Core.Gmail;

namespace QuickOTP.Core.Monitoring;

public sealed class AdaptivePollState
{
	public string AccountId { get; init; } = "";

	public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset? FastModeUntil { get; set; }

	public FastBoostReason FastBoostReason { get; set; }

	public DateTimeOffset? RecentActivityUntil { get; set; }

	public DateTimeOffset? BackoffUntil { get; set; }

	public DateTimeOffset? BudgetCooldownUntil { get; set; }

	public TimeSpan CurrentBackoff { get; set; }

	public int ConsecutiveErrors { get; set; }

	public bool ForceImmediate { get; set; }

	public DateTimeOffset NextDueAt { get; set; } = DateTimeOffset.UtcNow;

	public long ApiCalls { get; set; }

	public GmailApiBudget Budget { get; } = new GmailApiBudget();

	public PollingMode LastResolvedMode { get; set; } = PollingMode.Normal;
}
