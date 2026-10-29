using System;
using System.Text.Json.Serialization;

namespace QuickOTP.Core.Models;

public sealed class AppSettings
{
	public bool LaunchAtStartup { get; set; } = true;

	public bool AutoCopyCodes { get; set; } = true;

	public bool ShowNotifications { get; set; } = true;

	public bool HideAfterOpen { get; set; }

	public int CodeValidityMinutes { get; set; } = 10;

	public int PollIntervalSeconds { get; set; } = 10;

	public HistoryRetention HistoryRetention { get; set; } = HistoryRetention.OneHour;

	public bool EnableLogging { get; set; }

	public LogLevelSetting LogLevel { get; set; } = LogLevelSetting.ErrorsOnly;

	public bool EnableDigitCodes { get; set; } = true;

	public bool EnableAlphanumericCodes { get; set; } = true;

	public bool EnableHyphenatedCodes { get; set; } = true;

	public NotificationPrivacy NotificationPrivacy { get; set; }

	public bool PrivacyMode { get; set; }

	public ClipboardClearDelay ClipboardClearAfter { get; set; } = ClipboardClearDelay.Minutes2;

	public bool HotkeyEnabled { get; set; } = true;

	public string HotkeyGesture { get; set; } = "Ctrl+Alt+V";

	public bool RequireWindowsHelloForHistory { get; set; }

	public bool OnboardingDismissed { get; set; }

	public bool ShowAdvancedDiagnostics { get; set; }

	public string? ConnectedEmail { get; set; }

	public ulong? LastHistoryId { get; set; }

	[JsonIgnore]
	public TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Clamp(PollIntervalSeconds, 5, 60));

	[JsonIgnore]
	public TimeSpan CodeValidity => TimeSpan.FromMinutes(Math.Clamp(CodeValidityMinutes, 1, 60));

	public TimeSpan GetHistoryRetentionSpan()
	{
		return HistoryRetention switch
		{
			HistoryRetention.None => TimeSpan.Zero, 
			HistoryRetention.FifteenMinutes => TimeSpan.FromMinutes(15.0), 
			HistoryRetention.OneHour => TimeSpan.FromHours(1.0), 
			HistoryRetention.TwentyFourHours => TimeSpan.FromHours(24.0), 
			_ => TimeSpan.FromHours(1.0), 
		};
	}
}
