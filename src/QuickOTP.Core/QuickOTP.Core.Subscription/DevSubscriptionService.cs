using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuickOTP.Core.Subscription;

public sealed class DevSubscriptionService : ISubscriptionService
{
	private SubscriptionStatus _cache = new SubscriptionStatus
	{
		State = SubscriptionState.Trial,
		PlanId = "trial",
		ExpiresAt = DateTimeOffset.UtcNow.AddDays(14.0)
	};

	public Task<SubscriptionStatus> GetStatusAsync(CancellationToken ct = default(CancellationToken))
	{
		return Task.FromResult(_cache);
	}

	public Task<SubscriptionStatus> RefreshAsync(CancellationToken ct = default(CancellationToken))
	{
		_cache = new SubscriptionStatus
		{
			State = _cache.State,
			PlanId = _cache.PlanId,
			ExpiresAt = _cache.ExpiresAt,
			LastCheckedAt = DateTimeOffset.UtcNow
		};
		return Task.FromResult(_cache);
	}

	public Task StartCheckoutAsync(string planId, CancellationToken ct = default(CancellationToken))
	{
		_cache = new SubscriptionStatus
		{
			State = SubscriptionState.Active,
			PlanId = planId,
			ExpiresAt = (planId.Contains("annual", StringComparison.OrdinalIgnoreCase) ? DateTimeOffset.UtcNow.AddYears(1) : DateTimeOffset.UtcNow.AddMonths(1))
		};
		return Task.CompletedTask;
	}

	public Task RestoreAsync(CancellationToken ct = default(CancellationToken))
	{
		return RefreshAsync(ct);
	}
}
