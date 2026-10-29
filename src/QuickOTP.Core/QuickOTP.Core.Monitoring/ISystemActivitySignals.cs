using System;

namespace QuickOTP.Core.Monitoring;

public interface ISystemActivitySignals : IDisposable
{
	event EventHandler? ResumedFromSleep;

	event EventHandler? NetworkBecameAvailable;

	event EventHandler? NetworkBecameUnavailable;
}
