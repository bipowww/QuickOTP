using System;

namespace QuickOTP.Core.Monitoring;

public sealed class AdaptivePollingPolicy
{
	public static readonly TimeSpan FastInterval = TimeSpan.FromSeconds(2.0);

	public static readonly TimeSpan RecentActivityInterval = TimeSpan.FromSeconds(3.0);

	public static readonly TimeSpan NormalInterval = TimeSpan.FromSeconds(5.0);

	public static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(15.0);

	public static readonly TimeSpan DeepIdleInterval = TimeSpan.FromSeconds(30.0);

	public static readonly TimeSpan FastBoostWindow = TimeSpan.FromMinutes(2.0);

	public static readonly TimeSpan RecentActivityWindow = TimeSpan.FromMinutes(2.0);

	public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(5.0);

	public static readonly TimeSpan DeepIdleAfter = TimeSpan.FromMinutes(30.0);

	private readonly PollingJitter _jitter;

	private readonly TimeProvider _time;

	public PollingJitter Jitter => _jitter;

	public TimeProvider Time => _time;

	public DateTimeOffset UtcNow => _time.GetUtcNow();

	public AdaptivePollingPolicy(PollingJitter? jitter = null, TimeProvider? timeProvider = null)
	{
		_jitter = jitter ?? new PollingJitter();
		_time = timeProvider ?? TimeProvider.System;
	}

	public PollingMode ResolveMode(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		if (state.BudgetCooldownUntil.HasValue && state.BudgetCooldownUntil > dateTimeOffset)
		{
			return PollingMode.BudgetCooldown;
		}
		if (state.BackoffUntil.HasValue && state.BackoffUntil > dateTimeOffset)
		{
			return PollingMode.Backoff;
		}
		if (state.FastModeUntil.HasValue && state.FastModeUntil > dateTimeOffset)
		{
			return state.FastBoostReason switch
			{
				FastBoostReason.Wake => PollingMode.WakeBoost, 
				FastBoostReason.Network => PollingMode.NetworkRecovery, 
				_ => PollingMode.StartupBoost, 
			};
		}
		if (state.RecentActivityUntil.HasValue && state.RecentActivityUntil > dateTimeOffset)
		{
			return PollingMode.RecentActivity;
		}
		TimeSpan timeSpan = dateTimeOffset - state.LastActivityAt;
		if (timeSpan >= DeepIdleAfter)
		{
			return PollingMode.DeepIdle;
		}
		if (timeSpan >= IdleAfter)
		{
			return PollingMode.Idle;
		}
		return PollingMode.Normal;
	}

	public TimeSpan ResolveInterval(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		switch (ResolveMode(state, dateTimeOffset))
		{
		case PollingMode.BudgetCooldown:
			return state.BudgetCooldownUntil.Value - dateTimeOffset;
		case PollingMode.Backoff:
			return state.CurrentBackoff;
		case PollingMode.WakeBoost:
		case PollingMode.NetworkRecovery:
		case PollingMode.StartupBoost:
			return FastInterval;
		case PollingMode.RecentActivity:
			return RecentActivityInterval;
		case PollingMode.Idle:
			return IdleInterval;
		case PollingMode.DeepIdle:
			return DeepIdleInterval;
		default:
			return NormalInterval;
		}
	}

	public TimeSpan ResolveIntervalWithJitter(AdaptivePollState state, DateTimeOffset? now = null)
	{
		return _jitter.Apply(ResolveInterval(state, now));
	}

	public string DescribeMode(AdaptivePollState state, DateTimeOffset? now = null)
	{
		return ResolveMode(state, now).ToString();
	}

	public void OnAppStarted(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		state.FastModeUntil = dateTimeOffset + FastBoostWindow;
		state.FastBoostReason = FastBoostReason.Startup;
		state.LastActivityAt = dateTimeOffset;
		ClearBackoff(state);
	}

	public void OnWakeFromSleep(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		state.FastModeUntil = dateTimeOffset + FastBoostWindow;
		state.FastBoostReason = FastBoostReason.Wake;
		state.LastActivityAt = dateTimeOffset;
		state.ForceImmediate = true;
		ClearBackoff(state);
	}

	public void OnNetworkRestored(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		state.FastModeUntil = dateTimeOffset + FastBoostWindow;
		state.FastBoostReason = FastBoostReason.Network;
		state.LastActivityAt = dateTimeOffset;
		state.ForceImmediate = true;
		ClearBackoff(state);
	}

	public void OnChangesDetected(AdaptivePollState state, DateTimeOffset? now = null, int changeCount = 1)
	{
		if (changeCount > 0)
		{
			DateTimeOffset dateTimeOffset = (state.LastActivityAt = now ?? UtcNow);
			DateTimeOffset value = dateTimeOffset + RecentActivityWindow;
			if (!state.RecentActivityUntil.HasValue || state.RecentActivityUntil < value)
			{
				state.RecentActivityUntil = value;
			}
			ClearBackoff(state);
		}
	}

	public void OnPollSuccessNoChange(AdaptivePollState state, DateTimeOffset? now = null)
	{
		ClearBackoff(state);
	}

	public void OnBudgetExceeded(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		state.BudgetCooldownUntil = dateTimeOffset + TimeSpan.FromSeconds(30.0);
		state.LastActivityAt = dateTimeOffset;
	}

	public void OnTransientError(AdaptivePollState state, DateTimeOffset? now = null)
	{
		DateTimeOffset dateTimeOffset = now ?? UtcNow;
		TimeSpan timeSpan = (state.CurrentBackoff = ((state.CurrentBackoff <= TimeSpan.Zero) ? TimeSpan.FromSeconds(5.0) : TimeSpan.FromSeconds(Math.Min(state.CurrentBackoff.TotalSeconds * 2.0, 60.0))));
		state.BackoffUntil = dateTimeOffset + timeSpan;
		state.ConsecutiveErrors++;
	}

	public void ClearBackoff(AdaptivePollState state)
	{
		state.CurrentBackoff = TimeSpan.Zero;
		state.BackoffUntil = null;
		state.ConsecutiveErrors = 0;
	}
}
