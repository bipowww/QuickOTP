using System;

namespace QuickOTP.Core.Time;

public sealed class SystemClock : IClock
{
	public static SystemClock Instance { get; } = new SystemClock();

	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
