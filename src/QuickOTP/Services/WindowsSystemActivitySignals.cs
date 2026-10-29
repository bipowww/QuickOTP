using System;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using QuickOTP.Core.Monitoring;

namespace QuickOTP.Services;

public sealed class WindowsSystemActivitySignals : ISystemActivitySignals, IDisposable
{
	private bool _disposed;

	private bool? _lastNetworkAvailable;

	public event EventHandler? ResumedFromSleep;

	public event EventHandler? NetworkBecameAvailable;

	public event EventHandler? NetworkBecameUnavailable;

	public WindowsSystemActivitySignals()
	{
		SystemEvents.PowerModeChanged += OnPowerModeChanged;
		NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
		_lastNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
	}

	private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Resume)
		{
			this.ResumedFromSleep?.Invoke(this, EventArgs.Empty);
		}
	}

	private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
	{
		bool isAvailable = e.IsAvailable;
		bool? lastNetworkAvailable = _lastNetworkAvailable;
		_lastNetworkAvailable = isAvailable;
		if (isAvailable && lastNetworkAvailable == false)
		{
			this.NetworkBecameAvailable?.Invoke(this, EventArgs.Empty);
		}
		else if (!isAvailable && lastNetworkAvailable == true)
		{
			this.NetworkBecameUnavailable?.Invoke(this, EventArgs.Empty);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		try
		{
			SystemEvents.PowerModeChanged -= OnPowerModeChanged;
			NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
		}
		catch
		{
		}
	}
}
