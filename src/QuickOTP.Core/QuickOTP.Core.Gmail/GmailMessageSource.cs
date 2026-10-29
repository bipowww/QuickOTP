using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using QuickOTP.Core.Accounts;
using QuickOTP.Core.Logging;
using QuickOTP.Core.Models;
using QuickOTP.Core.Security;
using QuickOTP.Core.Sources;

namespace QuickOTP.Core.Gmail;

public sealed class GmailMessageSource : IMessageSource, IAsyncDisposable
{
	private readonly GmailService _gmail;

	private readonly IAccountStore _accounts;

	private readonly AccountInfo _account;

	private readonly IAppLogger _logger;

	public string AccountId => _account.Id;

	public string? AccountEmail => _account.Email ?? _gmail.ConnectedEmail;

	public MessageSourceKind Kind => MessageSourceKind.Gmail;

	public bool IsConnected => _gmail.IsConnected;

	public bool IsEnabled
	{
		get
		{
			return _account.Enabled;
		}
		set
		{
			_account.Enabled = value;
			_accounts.Upsert(_account);
		}
	}

	public string? LastError => _account.LastError;

	public GmailService Inner => _gmail;

	public bool IsConfigured => _gmail.IsConfigured;

	public GmailMessageSource(AccountInfo account, ITokenStorage tokens, OAuthClientConfig oauth, IAccountStore accounts, IAppLogger? logger = null)
	{
		_account = account;
		_accounts = accounts;
		_logger = logger ?? NullLogger.Instance;
		_gmail = new GmailService(tokens, oauth, _logger);
	}

	public async Task<bool> InitializeAsync(CancellationToken ct = default(CancellationToken))
	{
		try
		{
			bool flag = await _gmail.TryInitializeAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			_account.Status = (flag ? AccountConnectionStatus.Connected : AccountConnectionStatus.Disconnected);
			_account.Email = _gmail.ConnectedEmail ?? _account.Email;
			_account.LastError = (flag ? null : "Non connecté");
			_accounts.Upsert(_account);
			return flag;
		}
		catch (Exception ex)
		{
			_account.Status = AccountConnectionStatus.Error;
			_account.LastError = "Erreur d'initialisation";
			_accounts.Upsert(_account);
			_logger.Error("Gmail init failed", ex);
			return false;
		}
	}

	public async Task<string> AuthorizeAsync(CancellationToken ct = default(CancellationToken))
	{
		string text = await _gmail.AuthorizeAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		_account.Email = text;
		_account.DisplayName = text;
		_account.Status = AccountConnectionStatus.Connected;
		_account.LastError = null;
		_account.SyncCursor = null;
		_accounts.Upsert(_account);
		return text;
	}

	public async Task<SourcePollResult> PollAsync(CancellationToken ct = default(CancellationToken))
	{
		if (!_account.Enabled)
		{
			return SourcePollResult.Empty;
		}
		try
		{
			ulong? startHistoryId = null;
			if (!string.IsNullOrEmpty(_account.SyncCursor) && ulong.TryParse(_account.SyncCursor, out var result))
			{
				startHistoryId = result;
			}
			GmailPollResult result2 = await _gmail.PollNewMessagesAsync(startHistoryId, ct).ConfigureAwait(continueOnCapturedContext: false);
			_account.SyncCursor = result2.HistoryId.ToString();
			_account.Email = _gmail.ConnectedEmail ?? _account.Email;
			_account.Status = AccountConnectionStatus.Connected;
			_account.LastError = null;
			_accounts.Upsert(_account);
			if (result2.IsBaseline)
			{
				return new SourcePollResult
				{
					Messages = Array.Empty<MessageCandidate>(),
					Duration = result2.Duration,
					ApiCalls = result2.ApiCalls,
					HistoryListCalls = result2.HistoryListCalls,
					MessagesGetCalls = result2.MessagesGetCalls,
					IsBaseline = true,
					PollStartedAt = result2.PollStartedAt,
					HistoryResponseAt = result2.HistoryResponseAt
				};
			}
			List<MessageCandidate> messages = result2.Messages.Select((GmailMessage m) => new MessageCandidate
			{
				Id = m.Id,
				AccountId = _account.Id,
				Source = MessageSourceKind.Gmail,
				Sender = m.From,
				SenderName = ExtractName(m.From),
				Subject = m.Subject,
				PlainText = m.PlainBody,
				HtmlText = m.HtmlBody,
				ReceivedAt = (m.InternalDate ?? DateTimeOffset.Now),
				PollStartedAt = result2.PollStartedAt,
				HistoryResponseAt = result2.HistoryResponseAt,
				MessageFetchStartedAt = m.FetchStartedAt,
				MessageFetchedAt = m.FetchEndedAt
			}).ToList();
			return new SourcePollResult
			{
				Messages = messages,
				Duration = result2.Duration,
				ApiCalls = result2.ApiCalls,
				HistoryListCalls = result2.HistoryListCalls,
				MessagesGetCalls = result2.MessagesGetCalls,
				WasHistoryRecovery = result2.WasHistoryRecovery,
				PollStartedAt = result2.PollStartedAt,
				HistoryResponseAt = result2.HistoryResponseAt
			};
		}
		catch (Exception ex)
		{
			_account.Status = AccountConnectionStatus.Error;
			AccountInfo account = _account;
			string lastError = ((ex is PermanentAuthException) ? "Compte révoqué — reconnectez-vous" : ((!(ex is GmailApiException ex2)) ? ((!(ex is HttpRequestException)) ? "Erreur Gmail" : "Erreur réseau") : ((ex2.StatusCode != 403) ? $"Gmail HTTP {ex2.StatusCode}" : "Gmail API non activée (403)")));
			account.LastError = lastError;
			_accounts.Upsert(_account);
			_logger.Error("Gmail poll failed", ex);
			throw;
		}
	}

	public Task DisconnectAsync()
	{
		_gmail.Disconnect();
		_account.Status = AccountConnectionStatus.Disconnected;
		_account.SyncCursor = null;
		_account.LastError = null;
		_accounts.Upsert(_account);
		return Task.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		await _gmail.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	private static string? ExtractName(string? from)
	{
		if (string.IsNullOrWhiteSpace(from))
		{
			return null;
		}
		int num = from.IndexOf('<');
		if (num > 0)
		{
			return from.Substring(0, num).Trim().Trim('"');
		}
		return from;
	}
}
