using System;

namespace QuickOTP.Core.Monitoring;

public static class AdaptiveApiEstimator
{
	public static ApiEstimate EstimateIdle(TimeSpan duration, int accounts, bool includeStartupBurst = true)
	{
		if (accounts < 1)
		{
			accounts = 1;
		}
		AdaptivePollingPolicy adaptivePollingPolicy = new AdaptivePollingPolicy(PollingJitter.Disabled);
		int num = 0;
		for (int i = 0; i < 1; i++)
		{
			AdaptivePollState adaptivePollState = new AdaptivePollState
			{
				AccountId = "est"
			};
			DateTimeOffset dateTimeOffset = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
			if (includeStartupBurst)
			{
				adaptivePollingPolicy.OnAppStarted(adaptivePollState, dateTimeOffset);
			}
			else
			{
				adaptivePollState.LastActivityAt = dateTimeOffset.AddHours(-1.0);
			}
			DateTimeOffset dateTimeOffset2 = dateTimeOffset;
			DateTimeOffset dateTimeOffset3 = dateTimeOffset + duration;
			int num2 = 0;
			while (dateTimeOffset2 < dateTimeOffset3)
			{
				TimeSpan timeSpan = adaptivePollingPolicy.ResolveInterval(adaptivePollState, dateTimeOffset2);
				if (timeSpan < TimeSpan.FromMilliseconds(100.0))
				{
					timeSpan = TimeSpan.FromMilliseconds(100.0);
				}
				num2++;
				dateTimeOffset2 += timeSpan;
				if (num2 > 1000000)
				{
					break;
				}
			}
			num = num2;
		}
		int num3 = num * accounts;
		return new ApiEstimate(num3, 0, num3, accounts, duration);
	}
}
