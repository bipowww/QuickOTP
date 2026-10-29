using System;

namespace QuickOTP.Core.Monitoring;

public sealed class PollingJitter
{
	private readonly Random _rng;

	private readonly double _fraction;

	private readonly bool _enabled;

	private readonly double _absoluteMinMs;

	public static PollingJitter Disabled { get; } = new PollingJitter(null, 0.1, enabled: false);

	public PollingJitter(Random? random = null, double fraction = 0.1, bool enabled = true, double absoluteMinMs = 500.0)
	{
		_rng = random ?? Random.Shared;
		_fraction = Math.Clamp(fraction, 0.0, 0.5);
		_enabled = enabled;
		_absoluteMinMs = Math.Max(0.0, absoluteMinMs);
	}

	public static PollingJitter CreateDeterministic(int seed, double fraction = 0.1)
	{
		return new PollingJitter(new Random(seed), fraction);
	}

	public TimeSpan Apply(TimeSpan baseDelay)
	{
		if (!_enabled || baseDelay <= TimeSpan.Zero)
		{
			if (!(baseDelay < TimeSpan.Zero))
			{
				return baseDelay;
			}
			return TimeSpan.Zero;
		}
		double num = 1.0 + (_rng.NextDouble() * 2.0 - 1.0) * _fraction;
		double value = Math.Max(baseDelay.TotalMilliseconds * num, _absoluteMinMs);
		double min = Math.Max(_absoluteMinMs, baseDelay.TotalMilliseconds * (1.0 - _fraction));
		double max = baseDelay.TotalMilliseconds * (1.0 + _fraction);
		return TimeSpan.FromMilliseconds(Math.Clamp(value, min, max));
	}
}
