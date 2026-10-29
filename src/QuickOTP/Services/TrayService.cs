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
	private readonly ToolStripMenuItem _copyItem;
	private readonly Action _pause;
	private readonly Action _resume;
	private string? _lastCodePreview;

	public TrayService(
		Action showWindow,
		Action showMini,
		Action pause,
		Action resume,
		Action togglePrivacy,
		Action openSettings,
		Action copyLastCode,
		Action exit)
	{
		_pause = pause;
		_resume = resume;

		_pauseItem = new ToolStripMenuItem("Mettre en pause", null, (_, _) => _pause());
		_resumeItem = new ToolStripMenuItem("Reprendre", null, (_, _) => _resume()) { Enabled = false };
		_privacyItem = new ToolStripMenuItem("Mode privé", null, (_, _) => togglePrivacy()) { CheckOnClick = true };
		_copyItem = new ToolStripMenuItem("Copier le dernier code", null, (_, _) => copyLastCode()) { Enabled = false };

		var menu = new ContextMenuStrip();
		menu.Items.Add("Ouvrir QuickOTP", null, (_, _) => showWindow());
		menu.Items.Add("Fenêtre compacte", null, (_, _) => showMini());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(_copyItem);
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(_pauseItem);
		menu.Items.Add(_resumeItem);
		menu.Items.Add(_privacyItem);
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("Réglages", null, (_, _) => openSettings());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("Quitter", null, (_, _) => exit());

		_icon = new NotifyIcon
		{
			Icon = LoadIcon(),
			Text = "QuickOTP",
			Visible = true,
			ContextMenuStrip = menu
		};
		_icon.DoubleClick += (_, _) => showWindow();
	}

	public void SetPrivacyChecked(bool on) => _privacyItem.Checked = on;

	public void SetLastCode(string? code, bool privacyMode)
	{
		_lastCodePreview = code;
		_copyItem.Enabled = !string.IsNullOrEmpty(code);
		if (string.IsNullOrEmpty(code))
		{
			_copyItem.Text = "Copier le dernier code";
			return;
		}
		var shown = privacyMode ? new string('·', Math.Clamp(code.Length, 4, 8)) : code;
		_copyItem.Text = "Copier " + shown;
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
		_pauseItem.Enabled = !paused && (status == AppStatus.Monitoring || status == AppStatus.Error);
		_resumeItem.Enabled = paused;
		_icon.Text = status switch
		{
			AppStatus.Monitoring when !paused => "QuickOTP — Actif",
			AppStatus.Paused => "QuickOTP — En pause",
			AppStatus.NotConnected => "QuickOTP — Sans compte",
			AppStatus.Error => "QuickOTP — Erreur",
			AppStatus.Connecting => "QuickOTP — Connexion…",
			_ => "QuickOTP"
		};
	}

	private static Icon LoadIcon()
	{
		try
		{
			var path = Path.Combine(AppContext.BaseDirectory, "Assets", "quickotp.ico");
			if (File.Exists(path))
				return new Icon(path);
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
