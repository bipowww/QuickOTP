using System;

namespace QuickOTP.Core.Subscription;

public sealed class SubscriptionStatus
{
	public SubscriptionState State { get; init; } = SubscriptionState.Trial;

	public string? PlanId { get; init; }

	public DateTimeOffset? ExpiresAt { get; init; }

	public DateTimeOffset LastCheckedAt { get; init; } = DateTimeOffset.UtcNow;

	public bool IsEntitled
	{
		get
		{
			SubscriptionState state = State;
			if ((uint)state <= 3u)
			{
				return true;
			}
			return false;
		}
	}
}
