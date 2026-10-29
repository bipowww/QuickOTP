using System;

namespace QuickOTP.Core.Monitoring;

public sealed class NullSystemActivitySignals : ISystemActivitySignals, IDisposable
{
	public event EventHandler? ResumedFromSleep
	{
		add
		{
		}
		remove
		{
		}
	}

	public event EventHandler? NetworkBecameAvailable
	{
		add
		{
		}
		remove
		{
		}
	}

	public event EventHandler? NetworkBecameUnavailable
	{
		add
		{
		}
		remove
		{
		}
	}

	public void Dispose()
	{
	}
}
