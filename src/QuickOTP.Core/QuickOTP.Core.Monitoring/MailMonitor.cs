using System;
using System.Threading.Tasks;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Monitoring;

public sealed class MailMonitor : IAsyncDisposable
{
	private readonly OtpOrchestrator _orch;

	public AppStatus Status => _orch.Status;

	public DetectedCode? LastCode
	{
		get
		{
			if (_orch.LastCode != null)
			{
				return DetectedCode.From(_orch.LastCode);
			}
			return null;
		}
	}

	public string? LastError => _orch.LastError;

	public bool IsPaused => _orch.IsPaused;

	public bool IsRunning => _orch.IsRunning;

	public event EventHandler? StateChanged
	{
		add
		{
			_orch.StateChanged += value;
		}
		remove
		{
			_orch.StateChanged -= value;
		}
	}

	public MailMonitor(OtpOrchestrator orch)
	{
		_orch = orch;
	}

	public void Pause()
	{
		_orch.Pause();
	}

	public void Resume()
	{
		_orch.Resume();
	}

	public void ReloadDetector()
	{
		_orch.ReloadEngine();
	}

	public Task StartAsync()
	{
		return _orch.StartAsync();
	}

	public Task StopAsync()
	{
		return _orch.StopAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _orch.DisposeAsync();
	}
}
