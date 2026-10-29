using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QuickOTP.Core.Models;

namespace QuickOTP.Services;

public sealed class TrayService : IDisposable
{
	private readonly NotifyIcon _icon;

	private readonly ToolStripMenuItem _pauseItem;

	private readonly ToolStripMenuItem _resumeItem;

	private readonly ToolStripMenuItem _privacyItem;

	private readonly Action _pause;

	private readonly Action _resume;

	private bool _privacy;

	public TrayService(Action showWindow, Action showMini, Action pause, Action resume, Action togglePrivacy, Action openSettings, Action exit)
	{
		TrayService trayService = this;
		_pause = pause;
		_resume = resume;
		_pauseItem = new ToolStripMenuItem("Pause Monitoring", null, delegate
		{
			trayService._pause();
		});
		_resumeItem = new ToolStripMenuItem("Resume Monitoring", null, delegate
		{
			trayService._resume();
		})
		{
			Enabled = false
		};
		_privacyItem = new ToolStripMenuItem("Privacy Mode", null, delegate
		{
			togglePrivacy();
		})
		{
			CheckOnClick = true
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip
		{
			Items = 
			{
				{
					"Open QuickOTP",
					(Image?)null,
					(EventHandler?)delegate
					{
						showWindow();
					}
				},
				{
					"Quick Codes",
					(Image?)null,
					(EventHandler?)delegate
					{
						showMini();
					}
				},
				(ToolStripItem)new ToolStripSeparator(),
				(ToolStripItem)_pauseItem,
				(ToolStripItem)_resumeItem,
				(ToolStripItem)_privacyItem,
				(ToolStripItem)new ToolStripSeparator(),
				{
					"Settings",
					(Image?)null,
					(EventHandler?)delegate
					{
						openSettings();
					}
				},
				(ToolStripItem)new ToolStripSeparator(),
				{
					"Quit",
					(Image?)null,
					(EventHandler?)delegate
					{
						exit();
					}
				}
			}
		};
		_icon = new NotifyIcon
		{
			Icon = LoadIcon(),
			Text = "QuickOTP",
			Visible = true,
			ContextMenuStrip = contextMenuStrip
		};
		_icon.DoubleClick += delegate
		{
			showWindow();
		};
	}

	public void SetPrivacyChecked(bool on)
	{
		_privacy = on;
		_privacyItem.Checked = on;
	}

	public void ShowBalloon(string title, string text)
	{
		try
		{
			_icon.BalloonTipTitle = title;
			_icon.BalloonTipText = text;
			_icon.BalloonTipIcon = ToolTipIcon.Info;
			_icon.ShowBalloonTip(3500);
		}
		catch
		{
		}
	}

	public void UpdateStatus(AppStatus status, bool paused)
	{
		ToolStripMenuItem pauseItem = _pauseItem;
		bool flag = !paused;
		if (flag)
		{
			bool flag2 = ((status == AppStatus.Monitoring || status == AppStatus.Error) ? true : false);
			flag = flag2;
		}
		pauseItem.Enabled = flag;
		_resumeItem.Enabled = paused;
		NotifyIcon icon = _icon;
		string text;
		switch (status)
		{
		case AppStatus.Monitoring:
			if (!paused)
			{
				text = "QuickOTP — Active";
				break;
			}
			goto default;
		case AppStatus.Paused:
			text = "QuickOTP — Paused";
			break;
		case AppStatus.NotConnected:
			text = "QuickOTP — No account";
			break;
		case AppStatus.Error:
			text = "QuickOTP — Error";
			break;
		case AppStatus.Connecting:
			text = "QuickOTP — Connecting…";
			break;
		default:
			text = "QuickOTP";
			break;
		}
		icon.Text = text;
	}

	private static Icon LoadIcon()
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Assets", "quickotp.ico");
			if (File.Exists(text))
			{
				return new Icon(text);
			}
			return SystemIcons.Application;
		}
		catch
		{
			return SystemIcons.Application;
		}
	}

	public void Dispose()
	{
		_icon.Visible = false;
		_icon.Dispose();
	}
}
