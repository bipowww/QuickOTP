using System;

namespace QuickOTP.Services;

public sealed class NotificationService
{
	private Action<string, string>? _showBalloon;

	public void AttachTray(Action<string, string> showBalloon)
	{
		_showBalloon = showBalloon;
	}

	public void Show(string appName, string title, string code)
	{
		try
		{
			string arg = (string.IsNullOrEmpty(code) ? title : (title + "\n" + code));
			_showBalloon?.Invoke(appName, arg);
		}
		catch
		{
		}
	}
}
