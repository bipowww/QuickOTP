using System;

namespace QuickOTP.Core.Time;

public sealed class FakeClock : IClock
{
	public DateTimeOffset UtcNow { get; set; }

	public FakeClock(DateTimeOffset start)
	{
		UtcNow = start;
	}

	public void Advance(TimeSpan delta)
	{
		UtcNow += delta;
	}
}
