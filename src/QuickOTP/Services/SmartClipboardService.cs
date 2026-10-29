using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickOTP.Services;

public sealed class SmartClipboardService
{
	private readonly ClipboardService _clipboard = new ClipboardService();

	private string? _lastCopiedByUs;

	private CancellationTokenSource? _clearCts;

	public string? LastCopiedByUs => _lastCopiedByUs;

	public async Task CopyOtpAsync(string code, TimeSpan? clearAfter)
	{
		await _clipboard.SetTextAsync(code).ConfigureAwait(continueOnCapturedContext: false);
		_lastCopiedByUs = code;
		ScheduleClear(clearAfter);
	}

	public async Task CopyLastAsync(string code)
	{
		await _clipboard.SetTextAsync(code).ConfigureAwait(continueOnCapturedContext: false);
		_lastCopiedByUs = code;
	}

	private void ScheduleClear(TimeSpan? clearAfter)
	{
		_clearCts?.Cancel();
		_clearCts?.Dispose();
		_clearCts = null;
		if (!clearAfter.HasValue || clearAfter <= TimeSpan.Zero)
		{
			return;
		}
		CancellationTokenSource cts = new CancellationTokenSource();
		_clearCts = cts;
		string expected = _lastCopiedByUs;
		Task.Run(async delegate
		{
			_ = 1;
			try
			{
				await Task.Delay(clearAfter.Value, cts.Token).ConfigureAwait(continueOnCapturedContext: false);
				await ClearIfUnchangedAsync(expected).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException)
			{
			}
		}, cts.Token);
	}

	public Task ClearIfUnchangedAsync(string? expected)
	{
		if (string.IsNullOrEmpty(expected))
		{
			return Task.CompletedTask;
		}
		TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				string text = null;
				try
				{
					text = Clipboard.GetText();
				}
				catch
				{
				}
				if (text == expected)
				{
					try
					{
						Clipboard.Clear();
					}
					catch
					{
					}
					if (_lastCopiedByUs == expected)
					{
						_lastCopiedByUs = null;
					}
				}
				tcs.SetResult();
			}
			catch (Exception exception)
			{
				tcs.SetException(exception);
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.IsBackground = true;
		thread.Start();
		return tcs.Task;
	}
}
