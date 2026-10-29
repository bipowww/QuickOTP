using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using QuickOTP.Core.Models;
using QuickOTP.Core.Monitoring;
using QuickOTP.Core.Rules;
using QuickOTP.Core.Sources;
using QuickOTP.Services;

namespace QuickOTP;

public partial class MainWindow : Window
{
	private bool _suppress;

	private readonly DispatcherTimer _countdown;

	private bool _historyUnlocked;



































	public MainWindow()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		InitializeComponent();
		_countdown = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_countdown.Tick += delegate
		{
			RefreshLastCodeOnly();
		};
		_countdown.Start();
		RefreshUi();
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		base.Opacity = 0.0;
		DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(420.0))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		BeginAnimation(UIElement.OpacityProperty, animation);
		if (!(HomePanel.RenderTransform is TranslateTransform))
		{
			HomePanel.RenderTransform = new TranslateTransform();
		}
		DoubleAnimation animation2 = new DoubleAnimation(12.0, 0.0, TimeSpan.FromMilliseconds(480.0))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		((TranslateTransform)HomePanel.RenderTransform).BeginAnimation(TranslateTransform.YProperty, animation2);
	}

	public void ShowSettingsTab()
	{
		MainTabs.SelectedIndex = 4;
	}

	public async void RefreshUi()
	{
		_suppress = true;
		try
		{
			App instance = App.Instance;
			AppSettings appSettings = instance.SettingsStore.Load();
			OtpOrchestrator orchestrator = instance.Orchestrator;
			PrivacyToggle.Content = (appSettings.PrivacyMode ? "Privé · ON" : "Privé · OFF");
			AppStatus appStatus = orchestrator?.Status ?? AppStatus.Idle;
			TextBlock statusText = StatusText;
			statusText.Text = appStatus switch
			{
				AppStatus.Monitoring => "Actif", 
				AppStatus.Paused => "En pause", 
				AppStatus.Connecting => "Connexion…", 
				AppStatus.NotConnected => "Sans compte", 
				AppStatus.Error => orchestrator?.LastError ?? "Erreur", 
				_ => "En veille", 
			};
			Brush brush = appStatus switch
			{
				AppStatus.Error => (Brush)FindResource("WarnBrush"), 
				AppStatus.Monitoring => (Brush)FindResource("SuccessBrush"), 
				_ => (Brush)FindResource("MutedBrush"), 
			};
			StatusText.Foreground = brush;
			Ellipse statusDot = StatusDot;
			if (statusDot != null)
			{
				statusDot.Fill = brush;
			}
			IReadOnlyList<AccountInfo> all = instance.Accounts.GetAll();
			int num = all.Count((AccountInfo a) => a.Status == AccountConnectionStatus.Connected);
			AccountsSummaryText.Text = num switch
			{
				1 => "1 compte connecté", 
				0 => "Aucun compte connecté", 
				_ => $"{num} comptes connectés", 
			};
			RefreshLastCodeOnly();
			SourcesText.Text = ((all.Count == 0)
				? "Aucun compte pour l'instant. Utilisez Gmail ou Outlook ci-dessus — ça prend moins d'une minute."
				: string.Join("\n", all.Select((AccountInfo a) => $"{a.Provider}  ·  {(a.Enabled ? StatusFr(a.Status) : "En pause")}  ·  {a.Email ?? "—"}")));
			PauseResumeButton.Content = ((orchestrator != null && orchestrator.IsPaused) ? "Reprendre" : "Pause");
			PauseResumeButton.IsEnabled = orchestrator != null;
			LaunchAtStartupCheck.IsChecked = appSettings.LaunchAtStartup;
			AutoCopyCheck.IsChecked = appSettings.AutoCopyCodes;
			NotificationsCheck.IsChecked = appSettings.ShowNotifications;
			HotkeyCheck.IsChecked = appSettings.HotkeyEnabled;
			HelloCheck.IsChecked = appSettings.RequireWindowsHelloForHistory;
			ValidityBox.Text = appSettings.CodeValidityMinutes.ToString();
			PollIntervalBox.Text = appSettings.PollIntervalSeconds.ToString();
			HotkeyBox.Text = appSettings.HotkeyGesture;
			NotifPrivacyBox.SelectedIndex = (int)appSettings.NotificationPrivacy;
			ComboBox clipboardClearBox = ClipboardClearBox;
			clipboardClearBox.SelectedIndex = appSettings.ClipboardClearAfter switch
			{
				ClipboardClearDelay.Never => 0, 
				ClipboardClearDelay.Seconds30 => 1, 
				ClipboardClearDelay.Minute1 => 2, 
				ClipboardClearDelay.Minutes2 => 3, 
				ClipboardClearDelay.Minutes5 => 4, 
				ClipboardClearDelay.Minutes10 => 5, 
				_ => 3, 
			};
			clipboardClearBox = HistoryRetentionBox;
			clipboardClearBox.SelectedIndex = appSettings.HistoryRetention switch
			{
				HistoryRetention.None => 0, 
				HistoryRetention.FifteenMinutes => 1, 
				HistoryRetention.OneHour => 2, 
				HistoryRetention.TwentyFourHours => 3, 
				_ => 2, 
			};
			LogLevelBox.SelectedIndex = (int)appSettings.LogLevel;
			await RefreshHistoryAsync();
			RefreshAccounts();
			RefreshRules();
		}
		finally
		{
			_suppress = false;
		}
	}

	private void RefreshLastCodeOnly()
	{
		App instance = App.Instance;
		bool privacyMode = instance.SettingsStore.Load().PrivacyMode;
		OtpDetectionResult otpDetectionResult = instance.Orchestrator?.LastCode;
		if (otpDetectionResult == null)
		{
			LastServiceText.Text = "En attente d’un e-mail…";
			LastCodeText.Text = "········";
			LastMetaText.Text = "Connectez Gmail ou Outlook pour commencer";
		}
		else
		{
			LastServiceText.Text = otpDetectionResult.Service ?? "OTP";
			LastCodeText.Text = (privacyMode ? new string('·', Math.Clamp(otpDetectionResult.Code.Length, 4, 12)) : FormatCode(otpDetectionResult.Code));
			string text = FormatRelative(otpDetectionResult.ReceivedAt);
			string text2 = ((!otpDetectionResult.ExpiresAt.HasValue) ? "" : (otpDetectionResult.IsExpired ? "Expiré" : ("Expire dans " + FormatCountdown(otpDetectionResult.ExpiresAt.Value - DateTimeOffset.Now))));
			LastMetaText.Text = (string.IsNullOrEmpty(text2) ? ("Copié " + text) : ("Copié " + text + "  ·  " + text2));
		}
	}

	private async Task RefreshHistoryAsync()
	{
		App app = App.Instance;
		AppSettings settings = app.SettingsStore.Load();
		if (settings.RequireWindowsHelloForHistory && !_historyUnlocked)
		{
			HistoryList.ItemsSource = null;
			HistoryEmptyText.Visibility = Visibility.Visible;
			HistoryEmptyText.Text = "Historique verrouillé — Windows Hello requis.";
			if (!(await app.WindowsHello.VerifyAsync("Déverrouiller l'historique QuickOTP")))
			{
				return;
			}
			_historyUnlocked = true;
		}
		if (settings.HistoryRetention == HistoryRetention.None)
		{
			HistoryList.ItemsSource = null;
			HistoryEmptyText.Visibility = Visibility.Visible;
			HistoryEmptyText.Text = "Historique désactivé.";
			return;
		}
		bool privacy = settings.PrivacyMode;
		IReadOnlyList<HistoryEntry> readOnlyList = app.HistoryStore?.GetEntries() ?? Array.Empty<HistoryEntry>();
		HistoryList.ItemsSource = readOnlyList.Select((HistoryEntry e) => new
		{
			Service = (e.Service ?? "—"),
			Code = (privacy ? "••••••" : e.Code),
			Time = e.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
			Source = e.Source.ToString(),
			Account = Truncate(e.AccountEmail, 24),
			Expires = ((!e.ExpiresAt.HasValue) ? "—" : ((e.ExpiresAt <= DateTimeOffset.Now) ? "expiré" : FormatCountdown(e.ExpiresAt.Value - DateTimeOffset.Now))),
			Raw = e
		}).ToList();
		HistoryEmptyText.Visibility = ((readOnlyList.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		HistoryEmptyText.Text = "Aucun code récent.";
	}

	private void RefreshAccounts()
	{
		AccountsList.ItemsSource = (from a in App.Instance.Accounts.GetAll()
			select new
			{
				Provider = a.Provider.ToString(),
				Email = (a.Email ?? "—"),
				Status = a.Status.ToString(),
				Enabled = (a.Enabled ? "Oui" : "Non"),
				Error = (a.LastError ?? ""),
				Id = a.Id
			}).ToList();
	}

	private void RefreshRules()
	{
		UserRules current = App.Instance.Rules.Current;
		AlwaysDomainsBox.Text = string.Join(Environment.NewLine, current.AlwaysAcceptDomains);
		NeverDomainsBox.Text = string.Join(Environment.NewLine, current.NeverDetectDomains);
		OverridesBox.Text = string.Join(Environment.NewLine, current.ServiceOverrides.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kv) => kv.Key + "→" + kv.Value));
		NoAutoCopyBox.Text = string.Join(Environment.NewLine, current.DisableAutoCopyServices);
	}

	private static string FormatCode(string code)
	{
		if (string.IsNullOrEmpty(code) || !code.All(char.IsDigit))
		{
			return code;
		}
		switch (code.Length)
		{
		case 6:
		{
			string text4 = code.Substring(0, 3);
			string text2 = code;
			return text4 + " " + text2.Substring(3, text2.Length - 3);
		}
		case 8:
		{
			string text3 = code.Substring(0, 4);
			string text2 = code;
			return text3 + " " + text2.Substring(4, text2.Length - 4);
		}
		case 9:
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 3);
			defaultInterpolatedStringHandler.AppendFormatted(code.Substring(0, 3));
			defaultInterpolatedStringHandler.AppendLiteral(" ");
			defaultInterpolatedStringHandler.AppendFormatted(code.Substring(3, 3));
			defaultInterpolatedStringHandler.AppendLiteral(" ");
			string text2 = code;
			defaultInterpolatedStringHandler.AppendFormatted(text2.Substring(6, text2.Length - 6));
			return defaultInterpolatedStringHandler.ToStringAndClear();
		}
		case 10:
		{
			string text = code.Substring(0, 5);
			string text2 = code;
			return text + " " + text2.Substring(5, text2.Length - 5);
		}
		default:
			return code;
		}
	}

	private static string FormatCountdown(TimeSpan t)
	{
		if (t < TimeSpan.Zero)
		{
			return "00:00";
		}
		if (!(t.TotalHours >= 1.0))
		{
			return t.ToString("mm\\:ss");
		}
		return t.ToString("h\\:mm\\:ss");
	}

	private static string FormatRelative(DateTimeOffset when)
	{
		TimeSpan timeSpan = DateTimeOffset.Now - when;
		if (timeSpan.TotalSeconds < 60.0)
		{
			return $"il y a {(int)timeSpan.TotalSeconds} s";
		}
		if (timeSpan.TotalMinutes < 60.0)
		{
			return $"il y a {(int)timeSpan.TotalMinutes} min";
		}
		return when.ToLocalTime().ToString("g");
	}

	private static string StatusFr(AccountConnectionStatus status) => status switch
	{
		AccountConnectionStatus.Connected => "Connecté",
		AccountConnectionStatus.Error => "Erreur",
		AccountConnectionStatus.Disabled => "Désactivé",
		AccountConnectionStatus.Disconnected => "Déconnecté",
		_ => status.ToString()
	};

	private static string Truncate(string? s, int max)
	{
		if (!string.IsNullOrEmpty(s))
		{
			if (s.Length > max)
			{
				return s.Substring(0, max - 1) + "…";
			}
			return s;
		}
		return "—";
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		Hide();
	}

	private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
	{
		OtpOrchestrator orchestrator = App.Instance.Orchestrator;
		if (orchestrator != null)
		{
			if (orchestrator.IsPaused)
			{
				orchestrator.Resume();
			}
			else
			{
				orchestrator.Pause();
			}
			RefreshUi();
		}
	}

	private void OpenMini_Click(object sender, RoutedEventArgs e)
	{
		App.Instance.ShowMiniWindow();
	}

	private async void CopyLastCode_Click(object sender, RoutedEventArgs e)
	{
		string text = App.Instance.Orchestrator?.LastCode?.Code;
		if (!string.IsNullOrEmpty(text))
		{
			await App.Instance.Clipboard.CopyLastAsync(text);
		}
	}

	private void PrivacyToggle_Click(object sender, RoutedEventArgs e)
	{
		App.Instance.TogglePrivacyMode();
	}

	private async void AddGmail_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			App.Instance.ReloadGoogleOAuth();
			if (!App.Instance.GoogleOAuth.IsValid)
			{
				MessageBox.Show("Configuration Gmail introuvable.\nPlacez client_secret.json dans :\n" + App.GoogleClientSecretPath, "QuickOTP — Gmail");
				return;
			}
			await App.Instance.AddGmailAccountAsync();
			RefreshUi();
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "QuickOTP — Gmail");
		}
	}

	private async void AddMicrosoft_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (!App.Instance.MicrosoftOAuth.IsValid)
			{
				MessageBox.Show("msal_client.json manquant. Voir README (Azure AD).", "QuickOTP");
				return;
			}
			await App.Instance.AddMicrosoftAccountAsync();
			RefreshUi();
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "QuickOTP — Microsoft");
		}
	}

	private async void RemoveAccount_Click(object sender, RoutedEventArgs e)
	{
		if (AccountsList.SelectedItem != null)
		{
			string accountId = (string)AccountsList.SelectedItem.GetType().GetProperty("Id").GetValue(AccountsList.SelectedItem);
			await App.Instance.RemoveAccountAsync(accountId);
			RefreshUi();
		}
	}

	private void ToggleAccount_Click(object sender, RoutedEventArgs e)
	{
		if (AccountsList.SelectedItem == null)
		{
			return;
		}
		string id = (string)AccountsList.SelectedItem.GetType().GetProperty("Id").GetValue(AccountsList.SelectedItem);
		AccountInfo accountInfo = App.Instance.Accounts.Get(id);
		if (accountInfo != null)
		{
			accountInfo.Enabled = !accountInfo.Enabled;
			App.Instance.Accounts.Upsert(accountInfo);
			IMessageSource messageSource = App.Instance.Orchestrator?.Sources.FirstOrDefault((IMessageSource s) => s.AccountId == id);
			if (messageSource != null)
			{
				messageSource.IsEnabled = accountInfo.Enabled;
			}
			App.Instance.RestartMonitoringAsync();
			RefreshUi();
		}
	}

	private void ClearHistory_Click(object sender, RoutedEventArgs e)
	{
		App.Instance.HistoryStore?.Clear();
		RefreshHistoryAsync();
	}

	private void MarkFalsePositive_Click(object sender, RoutedEventArgs e)
	{
		if (HistoryList.SelectedItem == null)
		{
			return;
		}
		dynamic selectedItem = HistoryList.SelectedItem;
		try
		{
			if (selectedItem.Raw is HistoryEntry historyEntry)
			{
				App.Instance.HistoryStore?.MarkFalsePositive(historyEntry.MessageId ?? "", historyEntry.Code);
				App.Instance.Rules.MarkFalsePositive(new MessageCandidate
				{
					Id = (historyEntry.MessageId ?? ""),
					AccountId = (historyEntry.AccountId ?? ""),
					Source = historyEntry.Source,
					Sender = historyEntry.Sender
				}, historyEntry.Code);
				App.Instance.RulesStore.Save(App.Instance.Rules.Current);
				RefreshHistoryAsync();
			}
		}
		catch
		{
		}
	}

	private void Rules_Save(object sender, RoutedEventArgs e)
	{
		if (_suppress)
		{
			return;
		}
		UserRules current = App.Instance.Rules.Current;
		current.AlwaysAcceptDomains = SplitLines(AlwaysDomainsBox.Text);
		current.NeverDetectDomains = SplitLines(NeverDomainsBox.Text);
		current.DisableAutoCopyServices = SplitLines(NoAutoCopyBox.Text);
		current.ServiceOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in SplitLines(OverridesBox.Text))
		{
			string[] array = item.Split('→', '>', '=');
			if (array.Length >= 2)
			{
				current.ServiceOverrides[array[0].Trim()] = array[^1].Trim();
			}
		}
		App.Instance.Rules.Replace(current);
		App.Instance.RulesStore.Save(current);
		App.Instance.Orchestrator?.ReloadEngine();
	}

	private static List<string> SplitLines(string text)
	{
		return text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
	}

	private void Settings_Changed(object sender, RoutedEventArgs e)
	{
		if (!_suppress)
		{
			SaveSettings();
		}
	}

	private void SaveSettings()
	{
		App instance = App.Instance;
		AppSettings appSettings = instance.SettingsStore.Load();
		appSettings.LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true;
		appSettings.AutoCopyCodes = AutoCopyCheck.IsChecked == true;
		appSettings.ShowNotifications = NotificationsCheck.IsChecked == true;
		appSettings.HotkeyEnabled = HotkeyCheck.IsChecked == true;
		appSettings.RequireWindowsHelloForHistory = HelloCheck.IsChecked == true;
		appSettings.NotificationPrivacy = (NotificationPrivacy)Math.Clamp(NotifPrivacyBox.SelectedIndex, 0, 2);
		AppSettings appSettings2 = appSettings;
		appSettings2.ClipboardClearAfter = ClipboardClearBox.SelectedIndex switch
		{
			1 => ClipboardClearDelay.Seconds30, 
			2 => ClipboardClearDelay.Minute1, 
			3 => ClipboardClearDelay.Minutes2, 
			4 => ClipboardClearDelay.Minutes5, 
			5 => ClipboardClearDelay.Minutes10, 
			_ => ClipboardClearDelay.Never, 
		};
		appSettings2 = appSettings;
		appSettings2.HistoryRetention = HistoryRetentionBox.SelectedIndex switch
		{
			0 => HistoryRetention.None, 
			1 => HistoryRetention.FifteenMinutes, 
			3 => HistoryRetention.TwentyFourHours, 
			_ => HistoryRetention.OneHour, 
		};
		appSettings.LogLevel = (LogLevelSetting)Math.Clamp(LogLevelBox.SelectedIndex, 0, 2);
		appSettings.EnableLogging = appSettings.LogLevel != LogLevelSetting.Off;
		if (int.TryParse(ValidityBox.Text, out var result))
		{
			appSettings.CodeValidityMinutes = Math.Clamp(result, 1, 60);
		}
		if (int.TryParse(PollIntervalBox.Text, out var result2))
		{
			appSettings.PollIntervalSeconds = Math.Clamp(result2, 5, 60);
		}
		if (!string.IsNullOrWhiteSpace(HotkeyBox.Text))
		{
			appSettings.HotkeyGesture = HotkeyBox.Text.Trim();
		}
		instance.SettingsStore.Save(appSettings);
		StartupManager.Apply(appSettings.LaunchAtStartup);
		instance.RebindHotkey();
		instance.HistoryStore?.Prune(appSettings.HistoryRetention);
		instance.Orchestrator?.ReloadEngine();
	}

	private void Subscription_Click(object sender, RoutedEventArgs e)
	{
		SubscriptionWindow subscriptionWindow = new SubscriptionWindow();
		subscriptionWindow.Owner = this;
		subscriptionWindow.ShowDialog();
	}

	private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
	{
		RefreshDiagnostics();
	}

	private void RefreshDiagnostics()
	{
		OtpOrchestrator orchestrator = App.Instance.Orchestrator;
		if (orchestrator == null)
		{
			DiagPollingText.Text = "Orchestrator inactive.";
			DiagLatencyText.Text = "—";
			return;
		}
		PollDiagnosticsSnapshot diagnostics = orchestrator.GetDiagnostics();
		if (diagnostics.Accounts.Count == 0)
		{
			DiagPollingText.Text = "No accounts.";
		}
		else
		{
			DiagPollingText.Text = string.Join("\n\n", diagnostics.Accounts.Select((AccountPollDiagnostics a) => $"{a.AccountKey} ({a.SourceKind})\n  Mode: {a.Mode}\n  Current interval: {a.CurrentInterval.TotalSeconds:F1} s\n  Requests last minute: {a.RequestsLastMinute}\n  Requests last hour: {a.RequestsLastHour}\n  history.list/min: {a.HistoryListLastMinute}  messages.get/min: {a.MessagesGetLastMinute}" + (a.InBudgetCooldown ? "\n  Budget cooldown: yes" : "")));
		}
		SeriesStats total = diagnostics.Latency.Total;
		SeriesStats historyApi = diagnostics.Latency.HistoryApi;
		SeriesStats messageFetch = diagnostics.Latency.MessageFetch;
		SeriesStats otpProcessing = diagnostics.Latency.OtpProcessing;
		SeriesStats clipboard = diagnostics.Latency.Clipboard;
		DiagLatencyText.Text = $"Samples: {diagnostics.Latency.OtpMeasurements}\n\nTOTAL\n  Avg: {Fmt(total.AverageMs)}  P50: {Fmt(total.P50Ms)}  P90: {Fmt(total.P90Ms)}  P95: {Fmt(total.P95Ms)}  P99: {Fmt(total.P99Ms)}\n  Min: {Fmt(total.MinMs)}  Max: {Fmt(total.MaxMs)}\n\nHISTORY API\n  Avg: {Fmt(historyApi.AverageMs)}  P50: {Fmt(historyApi.P50Ms)}  P95: {Fmt(historyApi.P95Ms)}\n\nMESSAGE FETCH\n  Avg: {Fmt(messageFetch.AverageMs)}  P50: {Fmt(messageFetch.P50Ms)}  P95: {Fmt(messageFetch.P95Ms)}\n\nOTP PROCESSING\n  Avg: {Fmt(otpProcessing.AverageMs)}  P50: {Fmt(otpProcessing.P50Ms)}  P95: {Fmt(otpProcessing.P95Ms)}\n\nCLIPBOARD\n  Avg: {Fmt(clipboard.AverageMs)}  P50: {Fmt(clipboard.P50Ms)}  P95: {Fmt(clipboard.P95Ms)}";
	}

	private static string Fmt(double? ms)
	{
		if (ms.HasValue)
		{
			if (ms < 1000.0)
			{
				return $"{ms:F0} ms";
			}
			return $"{ms / 1000.0:F2} s";
		}
		return "—";
	}

}