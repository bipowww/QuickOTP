using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuickOTP.Core.Accounts;
using QuickOTP.Core.Dedup;
using QuickOTP.Core.Detection;
using QuickOTP.Core.Gmail;
using QuickOTP.Core.History;
using QuickOTP.Core.Logging;
using QuickOTP.Core.Models;
using QuickOTP.Core.Rules;
using QuickOTP.Core.Settings;
using QuickOTP.Core.Sources;

namespace QuickOTP.Core.Monitoring;

public sealed class OtpOrchestrator : IAsyncDisposable
{
	private readonly List<IMessageSource> _sources = new List<IMessageSource>();

	private readonly Dictionary<string, AdaptivePollState> _pollStates = new Dictionary<string, AdaptivePollState>(StringComparer.Ordinal);

	private readonly AdaptivePollingPolicy _policy = new AdaptivePollingPolicy();

	private readonly PollLatencyStats _latency = new PollLatencyStats();

	private readonly ISettingsStore _settings;

	private readonly IHistoryStore _history;

	private readonly IAccountStore _accounts;

	private readonly UserRulesEngine _rules;

	private readonly CodeDeduplicator _dedup = new CodeDeduplicator();

	private readonly IAppLogger _logger;

	private readonly Func<OtpDetectionResult, bool, Task> _onDetected;

	private readonly ISystemActivitySignals _system;

	private readonly object _gate = new object();

	private readonly object _wakeGate = new object();

	private OtpDetectionEngine _engine;

	private CancellationTokenSource? _cts;

	private Task? _loop;

	private bool _paused;

	private AppStatus _status;

	private string? _lastError;

	private OtpDetectionResult? _lastCode;

	private readonly List<OtpDetectionResult> _recentValid = new List<OtpDetectionResult>();

	private TaskCompletionSource _wake = NewWake();

	public AppStatus Status => _status;

	public string? LastError => _lastError;

	public bool IsPaused => _paused;

	public bool IsRunning
	{
		get
		{
			Task loop = _loop;
			if (loop != null)
			{
				return !loop.IsCompleted;
			}
			return false;
		}
	}

	public PollLatencyStats LatencyStats => _latency;

	public AdaptivePollingPolicy Policy => _policy;

	public OtpDetectionResult? LastCode
	{
		get
		{
			OtpDetectionResult lastCode = _lastCode;
			if (lastCode == null || lastCode.IsExpired)
			{
				return null;
			}
			return _lastCode;
		}
	}

	public IReadOnlyList<IMessageSource> Sources => _sources;

	public IReadOnlyList<OtpDetectionResult> RecentValid
	{
		get
		{
			lock (_gate)
			{
				PruneRecentLocked();
				return _recentValid.ToList();
			}
		}
	}

	public event EventHandler? StateChanged;

	public OtpOrchestrator(ISettingsStore settings, IHistoryStore history, IAccountStore accounts, UserRulesEngine rules, Func<OtpDetectionResult, bool, Task> onDetected, IAppLogger? logger = null, ISystemActivitySignals? systemSignals = null)
	{
		_settings = settings;
		_history = history;
		_accounts = accounts;
		_rules = rules;
		_onDetected = onDetected;
		_logger = logger ?? NullLogger.Instance;
		_system = systemSignals ?? new NullSystemActivitySignals();
		_engine = CreateEngine(settings.Load());
		_system.ResumedFromSleep += delegate
		{
			NotifyWakeFromSleep();
		};
		_system.NetworkBecameAvailable += delegate
		{
			NotifyNetworkRestored();
		};
		_system.NetworkBecameUnavailable += delegate
		{
			NotifyNetworkLost();
		};
	}

	public PollDiagnosticsSnapshot GetDiagnostics()
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		List<AccountPollDiagnostics> list = new List<AccountPollDiagnostics>();
		foreach (IMessageSource source in _sources)
		{
			if (_pollStates.TryGetValue(source.AccountId, out AdaptivePollState value))
			{
				BudgetSnapshot budgetSnapshot = value.Budget.Snapshot(utcNow);
				TimeSpan currentInterval = _policy.ResolveInterval(value, utcNow);
				list.Add(new AccountPollDiagnostics(OtpLatencyMeasurement.AnonymizeAccountId(source.AccountId), source.Kind.ToString(), _policy.DescribeMode(value, utcNow), currentInterval, budgetSnapshot.TotalLastMinute, budgetSnapshot.TotalLastHour, budgetSnapshot.HistoryListLastMinute, budgetSnapshot.MessagesGetLastMinute, budgetSnapshot.InCooldown));
			}
		}
		return new PollDiagnosticsSnapshot(list, _latency.SnapshotDetailed());
	}

	public void RegisterSource(IMessageSource source)
	{
		if (!_sources.Any((IMessageSource s) => s.AccountId == source.AccountId))
		{
			_sources.Add(source);
			AdaptivePollState adaptivePollState = new AdaptivePollState
			{
				AccountId = source.AccountId
			};
			_policy.OnAppStarted(adaptivePollState, DateTimeOffset.UtcNow);
			int num = Math.Abs(source.AccountId.GetHashCode()) % 400;
			adaptivePollState.NextDueAt = DateTimeOffset.UtcNow.AddMilliseconds(num);
			_pollStates[source.AccountId] = adaptivePollState;
			this.StateChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public async Task UnregisterSourceAsync(string accountId)
	{
		IMessageSource src = _sources.FirstOrDefault((IMessageSource s) => s.AccountId == accountId);
		if (src != null)
		{
			_sources.Remove(src);
			_pollStates.Remove(accountId);
			await src.DisconnectAsync().ConfigureAwait(continueOnCapturedContext: false);
			await src.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			this.StateChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public void Pause()
	{
		_paused = true;
		SetStatus(AppStatus.Paused);
	}

	public void Resume()
	{
		_paused = false;
		if (IsRunning)
		{
			SetStatus(AppStatus.Monitoring);
		}
		PulseWake();
	}

	public void NotifyWakeFromSleep()
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		foreach (AdaptivePollState value in _pollStates.Values)
		{
			_policy.OnWakeFromSleep(value, utcNow);
		}
		_logger.Diagnostic("System resume — adaptive fast mode 2 min");
		PulseWake();
	}

	public void NotifyNetworkRestored()
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		foreach (AdaptivePollState value in _pollStates.Values)
		{
			_policy.OnNetworkRestored(value, utcNow);
		}
		_logger.Diagnostic("Network available — adaptive fast mode 2 min");
		_lastError = null;
		if (!_paused && IsRunning)
		{
			SetStatus(AppStatus.Monitoring);
		}
		PulseWake();
	}

	public void NotifyNetworkLost()
	{
		_lastError = "Hors ligne";
		_logger.Diagnostic("Network unavailable — waiting for recovery (no aggressive retry)");
		if (!_paused && IsRunning)
		{
			SetStatus(AppStatus.Error);
		}
	}

	public void ReloadEngine()
	{
		_engine = CreateEngine(_settings.Load());
		_rules.Replace(_rules.Current);
	}

	public async Task StartAsync()
	{
		if (IsRunning)
		{
			return;
		}
		_cts = new CancellationTokenSource();
		SetStatus(AppStatus.Connecting);
		bool any = false;
		DateTimeOffset now = DateTimeOffset.UtcNow;
		foreach (IMessageSource src in _sources.Where((IMessageSource s) => s.IsEnabled))
		{
			try
			{
				if (await src.InitializeAsync(_cts.Token).ConfigureAwait(continueOnCapturedContext: false))
				{
					any = true;
					if (_pollStates.TryGetValue(src.AccountId, out AdaptivePollState value))
					{
						_policy.OnAppStarted(value, now);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.Error("Source init failed", ex);
			}
		}
		if (!any)
		{
			SetStatus(AppStatus.NotConnected);
			return;
		}
		SetStatus(AppStatus.Monitoring);
		_loop = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
	}

	public async Task StopAsync()
	{
		if (_cts == null)
		{
			return;
		}
		_cts.Cancel();
		PulseWake();
		if (_loop != null)
		{
			try
			{
				await _loop.ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
			}
		}
		_cts.Dispose();
		_cts = null;
		_loop = null;
		SetStatus(AppStatus.Idle);
	}

	private async Task RunLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				if (_paused)
				{
					await WaitInterruptibleAsync(TimeSpan.FromSeconds(30.0), ct).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				DateTimeOffset now = DateTimeOffset.UtcNow;
				List<IMessageSource> due = _sources.Where((IMessageSource s) => s.IsEnabled).Where(delegate(IMessageSource s)
				{
					if (!_pollStates.TryGetValue(s.AccountId, out AdaptivePollState value))
					{
						return true;
					}
					return value.ForceImmediate || value.NextDueAt <= now;
				}).ToList();
				if (due.Count == 0)
				{
					TimeSpan timeSpan = (from s in _pollStates.Values
						where _sources.Any((IMessageSource x) => x.AccountId == s.AccountId && x.IsEnabled)
						select s.NextDueAt).DefaultIfEmpty(now.AddSeconds(5.0)).Min() - now;
					if (timeSpan < TimeSpan.FromMilliseconds(50.0))
					{
						timeSpan = TimeSpan.FromMilliseconds(50.0);
					}
					if (timeSpan > TimeSpan.FromSeconds(30.0))
					{
						timeSpan = TimeSpan.FromSeconds(30.0);
					}
					await WaitInterruptibleAsync(timeSpan, ct).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				AppSettings settings = _settings.Load();
				bool anyError = false;
				for (int i = 0; i < due.Count; i++)
				{
					ct.ThrowIfCancellationRequested();
					if (i > 0)
					{
						await Task.Delay(75, ct).ConfigureAwait(continueOnCapturedContext: false);
					}
					IMessageSource messageSource = due[i];
					AdaptivePollState state = GetOrCreateState(messageSource.AccountId);
					try
					{
						await PollOneAsync(messageSource, state, settings, ct).ConfigureAwait(continueOnCapturedContext: false);
						if (_status != AppStatus.Monitoring && !_paused)
						{
							SetStatus(AppStatus.Monitoring);
						}
						_lastError = null;
					}
					catch (OperationCanceledException) when (ct.IsCancellationRequested)
					{
						throw;
					}
					catch (PermanentAuthException ex2)
					{
						anyError = true;
						_lastError = "Compte révoqué — reconnectez-vous";
						_logger.Error("Permanent auth failure", ex2);
						state.NextDueAt = DateTimeOffset.UtcNow.AddMinutes(5.0);
						state.ForceImmediate = false;
						_policy.ClearBackoff(state);
						SetStatus(AppStatus.Error);
					}
					catch (GmailApiException ex3)
					{
						anyError = true;
						_lastError = ((ex3.StatusCode == 403) ? "Activez Gmail API dans Google Cloud" : ex3.Message);
						_logger.Error("Gmail API error", ex3);
						state.NextDueAt = DateTimeOffset.UtcNow.AddMinutes(2.0);
						state.ForceImmediate = false;
						_policy.ClearBackoff(state);
						SetStatus(AppStatus.Error);
					}
					catch (Exception ex4)
					{
						anyError = true;
						_lastError = "Hors ligne / erreur réseau";
						_logger.Error("Adaptive poll error", ex4);
						_policy.OnTransientError(state, DateTimeOffset.UtcNow);
						ScheduleNext(state, DateTimeOffset.UtcNow);
						SetStatus(AppStatus.Error);
					}
				}
				if (!anyError && _status == AppStatus.Error && !_paused)
				{
					SetStatus(AppStatus.Monitoring);
				}
				_history.Prune(settings.HistoryRetention);
				lock (_gate)
				{
					PruneRecentLocked();
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				break;
			}
		}
	}

	private async Task PollOneAsync(IMessageSource src, AdaptivePollState state, AppSettings settings, CancellationToken ct)
	{
		state.ForceImmediate = false;
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		string accKey = OtpLatencyMeasurement.AnonymizeAccountId(src.AccountId);
		if (state.Budget.IsInCooldown(utcNow) || (state.BudgetCooldownUntil.HasValue && state.BudgetCooldownUntil > utcNow))
		{
			_logger.Warn("[Gmail] budget cooldown account=" + accKey);
			ScheduleNext(state, utcNow);
			return;
		}
		if (!state.Budget.TryConsume(GmailApiCallKind.HistoryList, utcNow, out var budgetExceeded) || budgetExceeded)
		{
			_policy.OnBudgetExceeded(state, utcNow);
			_logger.Warn("[Gmail] ApiBudgetExceeded account=" + accKey + " — cooldown 30s");
			ScheduleNext(state, utcNow);
			return;
		}
		_logger.Diagnostic($"[Gmail] poll started account={accKey} mode={_policy.DescribeMode(state, utcNow)} interval={(int)_policy.ResolveInterval(state, utcNow).TotalSeconds}s");
		SourcePollResult sourcePollResult;
		try
		{
			sourcePollResult = await src.PollAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
			throw;
		}
		int num = Math.Max(0, sourcePollResult.HistoryListCalls - 1);
		for (int i = 0; i < num; i++)
		{
			state.Budget.TryConsume(GmailApiCallKind.HistoryList, DateTimeOffset.UtcNow, out var _);
		}
		for (int j = 0; j < sourcePollResult.MessagesGetCalls; j++)
		{
			if (!state.Budget.TryConsume(GmailApiCallKind.MessagesGet, DateTimeOffset.UtcNow, out var budgetExceeded3) || budgetExceeded3)
			{
				_policy.OnBudgetExceeded(state, DateTimeOffset.UtcNow);
				_logger.Warn("[Gmail] ApiBudgetExceeded (messages.get) account=" + accKey);
				break;
			}
		}
		if (sourcePollResult.ApiBudgetExceeded)
		{
			_policy.OnBudgetExceeded(state, DateTimeOffset.UtcNow);
			_logger.Warn("[Gmail] ApiBudgetExceeded account=" + accKey);
		}
		state.ApiCalls += sourcePollResult.ApiCalls;
		_latency.AddApiCalls(sourcePollResult.ApiCalls);
		_logger.Diagnostic($"[Gmail] poll account={accKey} duration={(int)sourcePollResult.Duration.TotalMilliseconds}ms changes={sourcePollResult.Messages.Count}");
		if (sourcePollResult.HistoryResponseAt.HasValue && sourcePollResult.PollStartedAt.HasValue)
		{
			int value = (int)(sourcePollResult.HistoryResponseAt.Value - sourcePollResult.PollStartedAt.Value).TotalMilliseconds;
			_logger.Diagnostic($"[Gmail] history changes={sourcePollResult.Messages.Count} duration={value}ms");
		}
		if (sourcePollResult.HadChanges)
		{
			_policy.OnChangesDetected(state, DateTimeOffset.UtcNow, sourcePollResult.Messages.Count);
		}
		else if (!sourcePollResult.IsBaseline)
		{
			_policy.OnPollSuccessNoChange(state, DateTimeOffset.UtcNow);
		}
		bool flag = false;
		foreach (MessageCandidate message in sourcePollResult.Messages)
		{
			if (message.MessageFetchStartedAt.HasValue && message.MessageFetchedAt.HasValue)
			{
				int value2 = (int)(message.MessageFetchedAt.Value - message.MessageFetchStartedAt.Value).TotalMilliseconds;
				_logger.Diagnostic($"[Gmail] message fetch duration={value2}ms");
			}
			ProcessMessage(message, settings, src.AccountEmail, out var latencyRecorded);
			flag = flag || latencyRecorded;
		}
		if (!flag)
		{
			_latency.RecordPoll(sourcePollResult.Duration, sourcePollResult.Messages.Count, null);
		}
		ScheduleNext(state, DateTimeOffset.UtcNow);
	}

	private void ScheduleNext(AdaptivePollState state, DateTimeOffset now)
	{
		state.LastResolvedMode = _policy.ResolveMode(state, now);
		TimeSpan timeSpan = _policy.ResolveIntervalWithJitter(state, now);
		state.NextDueAt = now + timeSpan;
	}

	private AdaptivePollState GetOrCreateState(string accountId)
	{
		if (_pollStates.TryGetValue(accountId, out AdaptivePollState value))
		{
			return value;
		}
		value = new AdaptivePollState
		{
			AccountId = accountId
		};
		_policy.OnAppStarted(value, DateTimeOffset.UtcNow);
		_pollStates[accountId] = value;
		return value;
	}

	private async Task WaitInterruptibleAsync(TimeSpan delay, CancellationToken ct)
	{
		Task wakeTask;
		lock (_wakeGate)
		{
			wakeTask = _wake.Task;
		}
		Task delayTask = Task.Delay(delay, ct);
		if (await Task.WhenAny(delayTask, wakeTask).ConfigureAwait(continueOnCapturedContext: false) == wakeTask)
		{
			lock (_wakeGate)
			{
				if (_wake.Task.IsCompleted)
				{
					_wake = NewWake();
				}
				return;
			}
		}
		await delayTask.ConfigureAwait(continueOnCapturedContext: false);
	}

	private void PulseWake()
	{
		lock (_wakeGate)
		{
			_wake.TrySetResult();
		}
	}

	private static TaskCompletionSource NewWake()
	{
		return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private void ProcessMessage(MessageCandidate message, AppSettings settings, string? accountEmail, out bool latencyRecorded)
	{
		latencyRecorded = false;
		try
		{
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			OtpDetectionResult otpDetectionResult = _engine.Detect(message, settings.CodeValidity);
			DateTimeOffset utcNow2 = DateTimeOffset.UtcNow;
			if (otpDetectionResult == null || !otpDetectionResult.IsValid)
			{
				return;
			}
			OtpLatencyMeasurement latency = new OtpLatencyMeasurement
			{
				Source = message.Source,
				AccountKey = OtpLatencyMeasurement.AnonymizeAccountId(message.AccountId),
				EmailReceivedAt = message.ReceivedAt,
				PollStartedAt = message.PollStartedAt,
				HistoryResponseAt = message.HistoryResponseAt,
				MessageFetchStartedAt = message.MessageFetchStartedAt,
				MessageFetchedAt = message.MessageFetchedAt,
				OtpDetectedAt = utcNow2
			};
			int val = (int)(utcNow2 - (message.MessageFetchedAt ?? utcNow)).TotalMilliseconds;
			_logger.Diagnostic($"[OTP] detection duration={Math.Max(val, (int)(utcNow2 - utcNow).TotalMilliseconds)}ms confidence=high");
			otpDetectionResult = new OtpDetectionResult
			{
				Code = otpDetectionResult.Code,
				Confidence = otpDetectionResult.Confidence,
				Service = otpDetectionResult.Service,
				Sender = otpDetectionResult.Sender,
				MessageId = otpDetectionResult.MessageId,
				AccountId = otpDetectionResult.AccountId,
				Source = otpDetectionResult.Source,
				ReceivedAt = otpDetectionResult.ReceivedAt,
				ExpiresAt = otpDetectionResult.ExpiresAt,
				AccountEmail = accountEmail,
				Subject = otpDetectionResult.Subject,
				Latency = latency
			};
			if (!_dedup.ShouldProcess(otpDetectionResult.Source.ToString(), otpDetectionResult.AccountId, otpDetectionResult.MessageId, otpDetectionResult.Code))
			{
				return;
			}
			lock (_gate)
			{
				_lastCode = otpDetectionResult;
				_recentValid.Insert(0, otpDetectionResult);
				if (_recentValid.Count > 50)
				{
					_recentValid.RemoveAt(_recentValid.Count - 1);
				}
			}
			this.StateChanged?.Invoke(this, EventArgs.Empty);
			if (settings.HistoryRetention != HistoryRetention.None)
			{
				_history.Add(new HistoryEntry
				{
					Timestamp = otpDetectionResult.ReceivedAt,
					Code = otpDetectionResult.Code,
					Sender = otpDetectionResult.Sender,
					Service = otpDetectionResult.Service,
					MessageId = otpDetectionResult.MessageId,
					AccountId = otpDetectionResult.AccountId,
					AccountEmail = accountEmail,
					Source = otpDetectionResult.Source,
					ExpiresAt = otpDetectionResult.ExpiresAt
				});
			}
			if (message.ReceivedAt.HasValue)
			{
				TimeSpan timeSpan = utcNow2 - message.ReceivedAt.Value;
				if (timeSpan >= TimeSpan.Zero && timeSpan < TimeSpan.FromMinutes(30.0))
				{
					_latency.RecordPoll(TimeSpan.Zero, 1, timeSpan);
					latencyRecorded = true;
				}
			}
			bool allowCopy = !_rules.IsAutoCopyDisabled(otpDetectionResult.Service);
			SafeInvoke(otpDetectionResult, allowCopy);
		}
		catch (Exception ex)
		{
			_logger.Error("ProcessMessage failed", ex);
		}
	}

	public void RecordClipboardLatency(OtpDetectionResult result, DateTimeOffset copiedAt)
	{
		if (result.Latency != null)
		{
			result.Latency.ClipboardCopiedAt = copiedAt;
			int value = (result.Latency.ClipboardLatency.HasValue ? ((int)result.Latency.ClipboardLatency.Value.TotalMilliseconds) : 0);
			_logger.Diagnostic($"[Clipboard] copy duration={value}ms");
			if (result.Latency.TotalDetectionLatency.HasValue)
			{
				OtpLatencyMeasurement latency = result.Latency;
				_logger.Diagnostic($"[Latency] total={latency.TotalDetectionLatency.Value.TotalSeconds:F2}s history={((int?)latency.HistoryApiLatency?.TotalMilliseconds).GetValueOrDefault()}ms fetch={((int?)latency.MessageFetchLatency?.TotalMilliseconds).GetValueOrDefault()}ms detection={((int?)latency.OtpProcessingLatency?.TotalMilliseconds).GetValueOrDefault()}ms clipboard={value}ms");
			}
			_latency.Record(result.Latency);
		}
	}

	private async Task SafeInvoke(OtpDetectionResult result, bool allowCopy)
	{
		try
		{
			await _onDetected(result, allowCopy).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			_logger.Error("onDetected failed", ex);
		}
	}

	private void PruneRecentLocked()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		_recentValid.RemoveAll((OtpDetectionResult r) => r.ExpiresAt.HasValue && r.ExpiresAt <= now);
		OtpDetectionResult? lastCode = _lastCode;
		if (lastCode != null && lastCode.IsExpired)
		{
			_lastCode = _recentValid.FirstOrDefault();
		}
	}

	private OtpDetectionEngine CreateEngine(AppSettings settings)
	{
		Dictionary<string, string> serviceOverrides = _rules.Current.ServiceOverrides;
		return new OtpDetectionEngine(DetectionOptions.FromSettings(settings), new ServiceDetector(null, serviceOverrides), _rules, settings.CodeValidity);
	}

	private void SetStatus(AppStatus status)
	{
		lock (_gate)
		{
			_status = status;
		}
		this.StateChanged?.Invoke(this, EventArgs.Empty);
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync().ConfigureAwait(continueOnCapturedContext: false);
		_system.Dispose();
		foreach (IMessageSource source in _sources)
		{
			try
			{
				await source.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
			}
		}
		_sources.Clear();
	}
}
