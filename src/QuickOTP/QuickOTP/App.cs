using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using QuickOTP.Core.Accounts;
using QuickOTP.Core.Gmail;
using QuickOTP.Core.History;
using QuickOTP.Core.Logging;
using QuickOTP.Core.MicrosoftGraph;
using QuickOTP.Core.Models;
using QuickOTP.Core.Monitoring;
using QuickOTP.Core.Rules;
using QuickOTP.Core.Security;
using QuickOTP.Core.Settings;
using QuickOTP.Core.Sources;
using QuickOTP.Core.Subscription;
using QuickOTP.Services;

namespace QuickOTP;

public class App : Application
{
	public const string MutexName = "Global\\QuickOTP_SingleInstance_Mutex_v1";

	private Mutex? _mutex;

	private bool _ownsMutex;

	private TrayService? _tray;

	private OtpOrchestrator? _orchestrator;

	private MailMonitor? _monitorAdapter;

	private MainWindow? _mainWindow;

	private MiniWindow? _miniWindow;

	private ISettingsStore? _settings;

	private IAppLogger _logger = NullLogger.Instance;

	private SingleInstancePipe? _pipe;

	private GlobalHotkeyService? _hotkey;

	private SmartClipboardService? _clipboard;

	private IAccountStore? _accounts;

	private IAccountTokenVault? _tokenVault;

	private UserRulesEngine? _rules;

	private JsonRulesStore? _rulesStore;

	private OAuthClientConfig? _googleOAuth;

	private MicrosoftOAuthConfig? _msOAuth;

	private bool _contentLoaded;

	public static App Instance => (App)Application.Current;

	public OtpOrchestrator? Orchestrator => _orchestrator;

	public MailMonitor? Monitor => _monitorAdapter;

	public ISettingsStore SettingsStore => _settings;

	public IHistoryStore? HistoryStore { get; private set; }

	public IAccountStore Accounts => _accounts;

	public UserRulesEngine Rules => _rules;

	public JsonRulesStore RulesStore => _rulesStore;

	public SmartClipboardService Clipboard => _clipboard;

	public NotificationService? Notifications { get; private set; }

	public ISubscriptionService Subscription { get; private set; } = new DevSubscriptionService();

	public IWindowsHelloService WindowsHello { get; } = new WindowsHelloService();

	public OAuthClientConfig GoogleOAuth => _googleOAuth ?? new OAuthClientConfig();

	public MicrosoftOAuthConfig MicrosoftOAuth => _msOAuth ?? new MicrosoftOAuthConfig();

	public IAccountTokenVault TokenVault => _tokenVault;

	public static string UserConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");

	public static string GoogleClientSecretPath => Path.Combine(UserConfigDirectory, "client_secret.json");

	public GmailService? Gmail => _orchestrator?.Sources.OfType<GmailMessageSource>().FirstOrDefault()?.Inner;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		_mutex = new Mutex(initiallyOwned: true, "Global\\QuickOTP_SingleInstance_Mutex_v1", out _ownsMutex);
		if (!_ownsMutex)
		{
			SingleInstancePipe.SignalShowWindow();
			Shutdown();
			return;
		}
		base.DispatcherUnhandledException += (DispatcherUnhandledExceptionEventHandler)delegate(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			Exception ex4 = args.Exception;
			while (ex4.InnerException != null)
			{
				ex4 = ex4.InnerException;
			}
			_logger.Error("Unhandled UI exception: " + ex4.GetType().Name + ": " + ex4.Message, args.Exception);
			try
			{
				MessageBox.Show("Erreur d'interface :\n" + ex4.Message, "QuickOTP", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			catch
			{
			}
			args.Handled = true;
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
		{
			if (args.ExceptionObject is Exception ex4)
			{
				_logger.Error("Unhandled domain exception", ex4);
			}
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs args)
		{
			_logger.Error("Unobserved task exception", args.Exception);
			args.SetObserved();
		};
		_settings = new JsonSettingsStore();
		AppSettings settings = _settings.Load();
		MigrateLegacyHistoryRetention(settings);
		_logger = CreateLogger(settings);
		HistoryStore = new SecureHistoryStore();
		HistoryStore.Prune(settings.HistoryRetention);
		_accounts = new JsonAccountStore();
		_tokenVault = new DpapiAccountTokenVault();
		MigrateLegacyAccount(settings);
		_rulesStore = new JsonRulesStore();
		_rules = new UserRulesEngine(_rulesStore.Load());
		_googleOAuth = DiscoverGoogleOAuth();
		_msOAuth = MicrosoftOAuthConfig.Discover();
		Notifications = new NotificationService();
		_clipboard = new SmartClipboardService();
		WindowsSystemActivitySignals systemSignals = new WindowsSystemActivitySignals();
		_orchestrator = new OtpOrchestrator(_settings, HistoryStore, _accounts, _rules, async delegate(OtpDetectionResult result, bool allowCopy)
		{
			AppSettings s = _settings.Load();
			if (s.AutoCopyCodes && allowCopy)
			{
				TimeSpan? clearAfter = ((s.ClipboardClearAfter == ClipboardClearDelay.Never) ? ((TimeSpan?)null) : new TimeSpan?(TimeSpan.FromSeconds((double)s.ClipboardClearAfter)));
				await _clipboard.CopyOtpAsync(result.Code, clearAfter).ConfigureAwait(continueOnCapturedContext: false);
				_orchestrator?.RecordClipboardLatency(result, DateTimeOffset.UtcNow);
			}
			ShowDetectionNotification(s, result);
			await ((DispatcherObject)this).Dispatcher.InvokeAsync((Action)delegate
			{
				_mainWindow?.RefreshUi();
				_miniWindow?.Refresh();
			});
		}, _logger, systemSignals);
		foreach (AccountInfo item in _accounts.GetAll())
		{
			await EnsureSourceForAccountAsync(item);
		}
		_monitorAdapter = new MailMonitor(_orchestrator);
		_orchestrator.StateChanged += delegate
		{
			((DispatcherObject)this).Dispatcher.InvokeAsync((Action)delegate
			{
				_tray?.UpdateStatus(_orchestrator.Status, _orchestrator.IsPaused);
				_mainWindow?.RefreshUi();
				_miniWindow?.Refresh();
			});
		};
		try
		{
			_mainWindow = new MainWindow();
			base.MainWindow = _mainWindow;
		}
		catch (Exception ex)
		{
			_logger.Error("Impossible de créer la fenêtre principale", ex);
			Exception ex2 = ex;
			while (ex2.InnerException != null)
			{
				ex2 = ex2.InnerException;
			}
			MessageBox.Show("QuickOTP n'a pas pu démarrer :\n" + ex2.Message, "QuickOTP", MessageBoxButton.OK, MessageBoxImage.Hand);
			Shutdown();
			return;
		}
		_tray = new TrayService(ShowMainWindow, ShowMiniWindow, delegate
		{
			_orchestrator.Pause();
		}, delegate
		{
			_orchestrator.Resume();
		}, TogglePrivacyMode, delegate
		{
			ShowMainWindow();
			_mainWindow.ShowSettingsTab();
		}, ExitApplication);
		_tray.SetPrivacyChecked(settings.PrivacyMode);
		Notifications.AttachTray(_tray.ShowBalloon);
		_tray.UpdateStatus(AppStatus.Idle, paused: false);
		_pipe = new SingleInstancePipe(ShowMainWindow);
		_pipe.Start();
		StartupManager.Apply(settings.LaunchAtStartup);
		WindowsShellIntegration.EnsureSearchRegistration();
		try
		{
			_hotkey = new GlobalHotkeyService();
			if (settings.HotkeyEnabled)
			{
				_hotkey.Register(_mainWindow, settings.HotkeyGesture, OnHotkey);
			}
		}
		catch (Exception ex3)
		{
			_logger.Warn("Hotkey registration failed: " + ex3.Message);
		}
		bool flag = e.Args.Any((string a) => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
		if (e.Args.Any((string a) => a.Equals("--show", StringComparison.OrdinalIgnoreCase)) || !flag)
		{
			ShowMainWindow();
		}
		if (_orchestrator.Sources.Any((IMessageSource s) => s.IsConnected || _tokenVault.HasTokens(s.AccountId)))
		{
			await _orchestrator.StartAsync();
		}
		else
		{
			_tray.UpdateStatus(AppStatus.NotConnected, paused: false);
		}
	}

	private void OnHotkey()
	{
		string text = _orchestrator?.LastCode?.Code;
		if (!string.IsNullOrEmpty(text))
		{
			_clipboard.CopyLastAsync(text);
			AppSettings appSettings = _settings.Load();
			if (appSettings.NotificationPrivacy != NotificationPrivacy.Silent && !appSettings.PrivacyMode)
			{
				Notifications?.Show("QuickOTP", "Last OTP copied", (appSettings.NotificationPrivacy == NotificationPrivacy.Private) ? "" : text);
			}
		}
	}

	public void TogglePrivacyMode()
	{
		AppSettings appSettings = SettingsStore.Load();
		appSettings.PrivacyMode = !appSettings.PrivacyMode;
		SettingsStore.Save(appSettings);
		_tray?.SetPrivacyChecked(appSettings.PrivacyMode);
		_mainWindow?.RefreshUi();
		_miniWindow?.Refresh();
	}

	private void ShowDetectionNotification(AppSettings s, OtpDetectionResult result)
	{
		if (s.ShowNotifications && s.NotificationPrivacy != NotificationPrivacy.Silent)
		{
			string text = result.Service ?? "OTP";
			if (s.PrivacyMode || s.NotificationPrivacy == NotificationPrivacy.Private)
			{
				Notifications?.Show("QuickOTP", text ?? "", "Verification code copied");
			}
			else
			{
				Notifications?.Show("QuickOTP", text ?? "", result.Code + " copied");
			}
		}
	}

	public async Task EnsureSourceForAccountAsync(AccountInfo acc)
	{
		if (_orchestrator != null && _tokenVault != null && !_orchestrator.Sources.Any((IMessageSource s) => s.AccountId == acc.Id))
		{
			if (acc.Provider == AccountProvider.Gmail)
			{
				GmailMessageSource source = new GmailMessageSource(acc, _tokenVault.ForAccount(acc.Id), GoogleOAuth, _accounts, _logger);
				_orchestrator.RegisterSource(source);
			}
			else if (acc.Provider == AccountProvider.Microsoft)
			{
				MicrosoftGraphMessageSource source2 = new MicrosoftGraphMessageSource(acc, _tokenVault.ForAccount(acc.Id), MicrosoftOAuth, _accounts, _logger);
				_orchestrator.RegisterSource(source2);
			}
			await Task.CompletedTask;
		}
	}

	public async Task<AccountInfo> AddGmailAccountAsync()
	{
		string id = "gmail-" + Guid.NewGuid().ToString("N").Substring(0, 8);
		AccountInfo acc = new AccountInfo
		{
			Id = id,
			Provider = AccountProvider.Gmail,
			Enabled = true
		};
		_accounts.Upsert(acc);
		GmailMessageSource gmailMessageSource = new GmailMessageSource(acc, _tokenVault.ForAccount(id), GoogleOAuth, _accounts, _logger);
		_orchestrator.RegisterSource(gmailMessageSource);
		try
		{
			await gmailMessageSource.AuthorizeAsync();
			await RestartMonitoringAsync();
			return acc;
		}
		catch
		{
			await _orchestrator.UnregisterSourceAsync(id);
			_tokenVault.Clear(id);
			_accounts.Remove(id);
			throw;
		}
	}

	public async Task<AccountInfo> AddMicrosoftAccountAsync()
	{
		string id = "ms-" + Guid.NewGuid().ToString("N").Substring(0, 8);
		AccountInfo acc = new AccountInfo
		{
			Id = id,
			Provider = AccountProvider.Microsoft,
			Enabled = true
		};
		_accounts.Upsert(acc);
		MicrosoftGraphMessageSource microsoftGraphMessageSource = new MicrosoftGraphMessageSource(acc, _tokenVault.ForAccount(id), MicrosoftOAuth, _accounts, _logger);
		_orchestrator.RegisterSource(microsoftGraphMessageSource);
		try
		{
			await microsoftGraphMessageSource.AuthorizeAsync();
			await RestartMonitoringAsync();
			return acc;
		}
		catch
		{
			await _orchestrator.UnregisterSourceAsync(id);
			_tokenVault.Clear(id);
			_accounts.Remove(id);
			throw;
		}
	}

	public async Task RemoveAccountAsync(string accountId)
	{
		await _orchestrator.UnregisterSourceAsync(accountId);
		_tokenVault.Clear(accountId);
		_accounts.Remove(accountId);
		await RestartMonitoringAsync();
	}

	private void MigrateLegacyAccount(AppSettings settings)
	{
		if (_accounts.GetAll().Count <= 0 && (_tokenVault.HasTokens("gmail-legacy") || !string.IsNullOrEmpty(settings.ConnectedEmail)))
		{
			AccountInfo account = new AccountInfo
			{
				Id = "gmail-legacy",
				Provider = AccountProvider.Gmail,
				Email = settings.ConnectedEmail,
				DisplayName = settings.ConnectedEmail,
				Enabled = true,
				SyncCursor = settings.LastHistoryId?.ToString(),
				Status = AccountConnectionStatus.Connected
			};
			_accounts.Upsert(account);
		}
	}

	private static void MigrateLegacyHistoryRetention(AppSettings settings)
	{
		switch ((int)settings.HistoryRetention)
		{
		case 1:
			settings.HistoryRetention = HistoryRetention.OneHour;
			break;
		case 24:
			settings.HistoryRetention = HistoryRetention.TwentyFourHours;
			break;
		}
	}

	private static IAppLogger CreateLogger(AppSettings settings)
	{
		LogLevelSetting logLevelSetting = ((settings.EnableLogging && settings.LogLevel == LogLevelSetting.Off) ? LogLevelSetting.ErrorsOnly : settings.LogLevel);
		if (!settings.EnableLogging && logLevelSetting == LogLevelSetting.Off)
		{
			return NullLogger.Instance;
		}
		if (logLevelSetting == LogLevelSetting.Off)
		{
			return NullLogger.Instance;
		}
		return new FileAppLogger(logLevelSetting);
	}

	public void ReloadGoogleOAuth()
	{
		_googleOAuth = DiscoverGoogleOAuth();
	}

	private static OAuthClientConfig DiscoverGoogleOAuth()
	{
		OAuthClientConfig oAuthClientConfig = OAuthClientConfig.Discover(AppContext.BaseDirectory);
		if (oAuthClientConfig.IsValid)
		{
			return oAuthClientConfig;
		}
		string[] array = new string[4]
		{
			GoogleClientSecretPath,
			Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "client_secret.json")),
			Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "client_secret.json")),
			Path.Combine(AppContext.BaseDirectory, "client_secret.json")
		};
		for (int i = 0; i < array.Length; i++)
		{
			oAuthClientConfig = OAuthClientConfig.LoadFromFile(array[i]);
			if (oAuthClientConfig.IsValid)
			{
				return oAuthClientConfig;
			}
		}
		return new OAuthClientConfig();
	}

	public void ShowMainWindow()
	{
		if (((DispatcherObject)this).Dispatcher.CheckAccess())
		{
			Show();
		}
		else
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)Show);
		}
		void Show()
		{
			if (_mainWindow != null)
			{
				if (!_mainWindow.IsVisible)
				{
					_mainWindow.Show();
				}
				if (_mainWindow.WindowState == WindowState.Minimized)
				{
					_mainWindow.WindowState = WindowState.Normal;
				}
				_mainWindow.Activate();
				_mainWindow.Topmost = true;
				_mainWindow.Topmost = false;
				_mainWindow.Focus();
				_mainWindow.RefreshUi();
			}
		}
	}

	public void ShowMiniWindow()
	{
		((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
		{
			if (_miniWindow == null)
			{
				_miniWindow = new MiniWindow();
			}
			if (!_miniWindow.IsVisible)
			{
				_miniWindow.Show();
			}
			_miniWindow.Activate();
			_miniWindow.Refresh();
		});
	}

	public async Task RestartMonitoringAsync()
	{
		if (_orchestrator != null)
		{
			await _orchestrator.StopAsync();
			_orchestrator.ReloadEngine();
			if (_orchestrator.Sources.Any((IMessageSource s) => s.IsEnabled))
			{
				await _orchestrator.StartAsync();
			}
		}
	}

	public void RebindHotkey()
	{
		try
		{
			AppSettings appSettings = SettingsStore.Load();
			_hotkey?.Unregister();
			if (appSettings.HotkeyEnabled && _mainWindow != null)
			{
				_hotkey?.Register(_mainWindow, appSettings.HotkeyGesture, OnHotkey);
			}
		}
		catch (Exception ex)
		{
			_logger.Warn("Hotkey rebind failed: " + ex.Message);
		}
	}

	public void ExitApplication()
	{
		ShutdownGracefullyAsync();
	}

	private async Task ShutdownGracefullyAsync()
	{
		try
		{
			_hotkey?.Dispose();
			if (_orchestrator != null)
			{
				await _orchestrator.DisposeAsync();
			}
		}
		catch
		{
		}
		finally
		{
			_tray?.Dispose();
			_pipe?.Dispose();
			if (_ownsMutex)
			{
				try
				{
					_mutex?.ReleaseMutex();
				}
				catch
				{
				}
				_mutex?.Dispose();
			}
			Shutdown();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.31.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QuickOTP;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.31.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
