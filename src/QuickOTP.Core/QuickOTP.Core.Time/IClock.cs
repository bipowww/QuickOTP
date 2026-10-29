using System;

namespace QuickOTP.Core.Time;

public interface IClock
{
	DateTimeOffset UtcNow { get; }
}
