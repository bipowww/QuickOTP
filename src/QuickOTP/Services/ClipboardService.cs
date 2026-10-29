using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace QuickOTP.Services;

public sealed class ClipboardService
{
	public Task SetTextAsync(string text)
	{
		TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				for (int i = 0; i < 3; i++)
				{
					try
					{
						System.Windows.Clipboard.SetText(text);
						tcs.SetResult();
						return;
					}
					catch
					{
						Thread.Sleep(50);
					}
				}
				try
				{
					System.Windows.Forms.Clipboard.SetText(text);
					tcs.SetResult();
				}
				catch (Exception exception)
				{
					tcs.SetException(exception);
				}
			}
			catch (Exception exception2)
			{
				tcs.SetException(exception2);
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.IsBackground = true;
		thread.Start();
		return tcs.Task;
	}
}
