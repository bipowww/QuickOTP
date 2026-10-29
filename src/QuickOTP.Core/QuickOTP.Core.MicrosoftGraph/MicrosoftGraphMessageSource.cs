using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using QuickOTP.Core.Accounts;
using QuickOTP.Core.Logging;
using QuickOTP.Core.Models;
using QuickOTP.Core.Security;
using QuickOTP.Core.Sources;

namespace QuickOTP.Core.MicrosoftGraph;

public sealed class MicrosoftGraphMessageSource : IMessageSource, IAsyncDisposable
{
	private sealed class TokenResponse
	{
		[JsonPropertyName("access_token")]
		public string AccessToken { get; set; } = "";

		[JsonPropertyName("refresh_token")]
		public string? RefreshToken { get; set; }

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }
	}

	public const string MailReadScope = "https://graph.microsoft.com/Mail.Read";

	public const string UserReadScope = "https://graph.microsoft.com/User.Read";

	public const string OfflineAccess = "offline_access";

	private readonly ITokenStorage _tokens;

	private readonly MicrosoftOAuthConfig _oauth;

	private readonly IAccountStore _accounts;

	private readonly AccountInfo _account;

	private readonly IAppLogger _logger;

	private readonly HttpClient _http;

	private string? _accessToken;

	private DateTimeOffset _accessExpiry = DateTimeOffset.MinValue;

	public string AccountId => _account.Id;

	public string? AccountEmail => _account.Email;

	public MessageSourceKind Kind => MessageSourceKind.Microsoft;

	public bool IsConnected => _tokens.HasTokens();

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

	public bool IsConfigured => _oauth.IsValid;

	public MicrosoftGraphMessageSource(AccountInfo account, ITokenStorage tokens, MicrosoftOAuthConfig oauth, IAccountStore accounts, IAppLogger? logger = null, HttpClient? http = null)
	{
		_account = account;
		_tokens = tokens;
		_oauth = oauth;
		_accounts = accounts;
		_logger = logger ?? NullLogger.Instance;
		_http = http ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30.0)
		};
	}

	public async Task<bool> InitializeAsync(CancellationToken ct = default(CancellationToken))
	{
		StoredTokens storedTokens = _tokens.LoadTokens();
		if (storedTokens == null)
		{
			return false;
		}
		_account.Email = storedTokens.Email ?? _account.Email;
		_accessToken = storedTokens.AccessToken;
		_accessExpiry = storedTokens.AccessTokenExpiry ?? DateTimeOffset.MinValue;
		if (string.IsNullOrEmpty(_accessToken) || _accessExpiry < DateTimeOffset.UtcNow.AddMinutes(2.0))
		{
			await RefreshAsync(storedTokens.RefreshToken, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		_account.Status = AccountConnectionStatus.Connected;
		_accounts.Upsert(_account);
		return true;
	}

	public async Task<string> AuthorizeAsync(CancellationToken ct = default(CancellationToken))
	{
		if (!_oauth.IsValid)
		{
			throw new InvalidOperationException("Identifiants Microsoft OAuth manquants. Voir README (Azure AD / app registration).");
		}
		HttpListener listener = new HttpListener();
		string redirect = $"http://127.0.0.1:{GetFreePort()}/";
		listener.Prefixes.Add(redirect);
		listener.Start();
		try
		{
			string text = Guid.NewGuid().ToString("N");
			string stringToEscape = "https://graph.microsoft.com/User.Read https://graph.microsoft.com/Mail.Read offline_access";
			string fileName = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=" + Uri.EscapeDataString(_oauth.ClientId) + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(redirect) + "&response_mode=query&scope=" + Uri.EscapeDataString(stringToEscape) + "&state=" + text;
			Process.Start(new ProcessStartInfo
			{
				FileName = fileName,
				UseShellExecute = true
			});
			HttpListenerContext obj = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			string text2 = obj.Request.QueryString["code"];
			string text3 = obj.Request.QueryString["error"];
			byte[] bytes = Encoding.UTF8.GetBytes("<html><body style='font-family:Segoe UI;padding:2rem'><h2>QuickOTP</h2><p>Vous pouvez fermer cette fenêtre.</p></body></html>");
			obj.Response.ContentType = "text/html; charset=utf-8";
			obj.Response.OutputStream.Write(bytes);
			obj.Response.Close();
			if (!string.IsNullOrEmpty(text3) || string.IsNullOrEmpty(text2))
			{
				throw new InvalidOperationException("Autorisation Microsoft refusée: " + text3);
			}
			await ExchangeCodeAsync(text2, redirect, ct).ConfigureAwait(continueOnCapturedContext: false);
			_account.Email = AccountEmail;
			_account.DisplayName = AccountEmail;
			_account.Status = AccountConnectionStatus.Connected;
			_account.SyncCursor = null;
			_account.LastError = null;
			_accounts.Upsert(_account);
			return AccountEmail ?? "";
		}
		finally
		{
			listener.Stop();
			listener.Close();
		}
	}

	public async Task<SourcePollResult> PollAsync(CancellationToken ct = default(CancellationToken))
	{
		if (!_account.Enabled)
		{
			return SourcePollResult.Empty;
		}
		Stopwatch sw = Stopwatch.StartNew();
		int apiCalls = 0;
		await EnsureTokenAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (string.IsNullOrEmpty(_account.SyncCursor))
			{
				apiCalls++;
				string text = await GetNewestReceivedAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				_account.SyncCursor = text ?? DateTimeOffset.UtcNow.ToString("O");
				_account.Status = AccountConnectionStatus.Connected;
				_accounts.Upsert(_account);
				return new SourcePollResult
				{
					Messages = Array.Empty<MessageCandidate>(),
					Duration = sw.Elapsed,
					ApiCalls = apiCalls,
					IsBaseline = true
				};
			}
			if (!DateTimeOffset.TryParse(_account.SyncCursor, out var since))
			{
				since = DateTimeOffset.UtcNow.AddMinutes(-5.0);
			}
			string text2 = Uri.EscapeDataString($"receivedDateTime gt {since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");
			string requestUri = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?$filter=" + text2 + "&$select=id,subject,from,receivedDateTime,bodyPreview,body&$orderby=receivedDateTime asc&$top=25";
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, requestUri);
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
			apiCalls++;
			using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
			string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			List<MessageCandidate> list = new List<MessageCandidate>();
			DateTimeOffset? dateTimeOffset = since;
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			if (jsonDocument.RootElement.TryGetProperty("value", out var value))
			{
				foreach (JsonElement item in value.EnumerateArray())
				{
					string text3 = item.GetProperty("id").GetString();
					if (string.IsNullOrEmpty(text3))
					{
						continue;
					}
					DateTimeOffset? receivedAt = null;
					if (item.TryGetProperty("receivedDateTime", out var value2) && DateTimeOffset.TryParse(value2.GetString(), out var result))
					{
						receivedAt = result;
						DateTimeOffset value3 = result;
						DateTimeOffset? dateTimeOffset2 = dateTimeOffset;
						if (value3 > dateTimeOffset2)
						{
							dateTimeOffset = result;
						}
					}
					string sender = null;
					string senderName = null;
					if (item.TryGetProperty("from", out var value4) && value4.TryGetProperty("emailAddress", out var value5))
					{
						sender = (value5.TryGetProperty("address", out var value6) ? value6.GetString() : null);
						senderName = (value5.TryGetProperty("name", out var value7) ? value7.GetString() : null);
					}
					string text4 = null;
					string text5 = null;
					if (item.TryGetProperty("body", out var value8))
					{
						JsonElement value9;
						string text6 = (value8.TryGetProperty("content", out value9) ? value9.GetString() : null);
						if (string.Equals(value8.TryGetProperty("contentType", out var value10) ? value10.GetString() : "text", "html", StringComparison.OrdinalIgnoreCase))
						{
							text5 = text6;
						}
						else
						{
							text4 = text6;
						}
					}
					if (string.IsNullOrWhiteSpace(text4) && string.IsNullOrWhiteSpace(text5) && item.TryGetProperty("bodyPreview", out var value11))
					{
						text4 = value11.GetString();
					}
					list.Add(new MessageCandidate
					{
						Id = text3,
						AccountId = _account.Id,
						Source = MessageSourceKind.Microsoft,
						Sender = sender,
						SenderName = senderName,
						Subject = (item.TryGetProperty("subject", out var value12) ? value12.GetString() : null),
						PlainText = text4,
						HtmlText = text5,
						ReceivedAt = receivedAt
					});
				}
			}
			if (dateTimeOffset.HasValue)
			{
				_account.SyncCursor = dateTimeOffset.Value.ToString("O");
			}
			_account.Status = AccountConnectionStatus.Connected;
			_account.LastError = null;
			_accounts.Upsert(_account);
			return new SourcePollResult
			{
				Messages = list,
				Duration = sw.Elapsed,
				ApiCalls = apiCalls
			};
		}
		catch (Exception ex)
		{
			_account.Status = AccountConnectionStatus.Error;
			_account.LastError = "Erreur réseau Microsoft";
			_accounts.Upsert(_account);
			_logger.Error("Graph poll failed", ex);
			throw;
		}
	}

	public Task DisconnectAsync()
	{
		_tokens.Clear();
		_account.Status = AccountConnectionStatus.Disconnected;
		_account.SyncCursor = null;
		_accounts.Upsert(_account);
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		_http.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task<string?> GetNewestReceivedAsync(CancellationToken ct)
	{
		string requestUri = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?$select=receivedDateTime&$orderby=receivedDateTime desc&$top=1";
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, requestUri);
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
		using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
		if (jsonDocument.RootElement.TryGetProperty("value", out var value) && value.GetArrayLength() > 0)
		{
			return value[0].GetProperty("receivedDateTime").GetString();
		}
		return null;
	}

	private async Task ExchangeCodeAsync(string code, string redirect, CancellationToken ct)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			["client_id"] = _oauth.ClientId,
			["scope"] = "https://graph.microsoft.com/User.Read https://graph.microsoft.com/Mail.Read offline_access",
			["code"] = code,
			["redirect_uri"] = redirect,
			["grant_type"] = "authorization_code"
		};
		if (!string.IsNullOrEmpty(_oauth.ClientSecret))
		{
			dictionary["client_secret"] = _oauth.ClientSecret;
		}
		using FormUrlEncodedContent content = new FormUrlEncodedContent(dictionary);
		using HttpResponseMessage resp = await _http.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", content, ct).ConfigureAwait(continueOnCapturedContext: false);
		string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!resp.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Échec échange code Microsoft OAuth.");
		}
		TokenResponse token = JsonSerializer.Deserialize<TokenResponse>(json) ?? throw new InvalidOperationException("Token Microsoft invalide.");
		_accessToken = token.AccessToken;
		_accessExpiry = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 60);
		if (string.IsNullOrEmpty(token.RefreshToken))
		{
			throw new InvalidOperationException("Pas de refresh token Microsoft (vérifiez offline_access).");
		}
		string email = await FetchEmailAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		_account.Email = email;
		_tokens.SaveTokens(token.RefreshToken, _accessToken, _accessExpiry, email);
	}

	private async Task RefreshAsync(string refreshToken, CancellationToken ct)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			["client_id"] = _oauth.ClientId,
			["scope"] = "https://graph.microsoft.com/User.Read https://graph.microsoft.com/Mail.Read offline_access",
			["refresh_token"] = refreshToken,
			["grant_type"] = "refresh_token"
		};
		if (!string.IsNullOrEmpty(_oauth.ClientSecret))
		{
			dictionary["client_secret"] = _oauth.ClientSecret;
		}
		using FormUrlEncodedContent content = new FormUrlEncodedContent(dictionary);
		using HttpResponseMessage resp = await _http.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", content, ct).ConfigureAwait(continueOnCapturedContext: false);
		string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!resp.IsSuccessStatusCode)
		{
			_tokens.Clear();
			throw new InvalidOperationException("Session Microsoft expirée. Reconnectez-vous.");
		}
		TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);
		_accessToken = tokenResponse.AccessToken;
		_accessExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
		string email = _account.Email ?? _tokens.LoadTokens()?.Email;
		_tokens.SaveTokens(tokenResponse.RefreshToken ?? refreshToken, _accessToken, _accessExpiry, email);
	}

	private async Task EnsureTokenAsync(CancellationToken ct)
	{
		if (string.IsNullOrEmpty(_accessToken) || !(_accessExpiry > DateTimeOffset.UtcNow.AddMinutes(1.0)))
		{
			StoredTokens storedTokens = _tokens.LoadTokens() ?? throw new InvalidOperationException("Non connecté à Microsoft.");
			await RefreshAsync(storedTokens.RefreshToken, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task<string?> FetchEmailAsync(CancellationToken ct)
	{
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me?$select=mail,userPrincipalName");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
		using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
		object result;
		if (jsonDocument.RootElement.TryGetProperty("mail", out var value))
		{
			string text = value.GetString();
			if (text != null && text.Length > 0)
			{
				result = text;
				goto IL_01b2;
			}
		}
		result = (jsonDocument.RootElement.TryGetProperty("userPrincipalName", out var value2) ? value2.GetString() : null);
		goto IL_01b2;
		IL_01b2:
		return (string?)result;
	}

	private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken ct)
	{
		Exception last = null;
		for (int attempt = 0; attempt < 5; attempt++)
		{
			try
			{
				HttpRequestMessage httpRequestMessage = new HttpRequestMessage(request.Method, request.RequestUri);
				httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
				HttpResponseMessage httpResponseMessage = await _http.SendAsync(httpRequestMessage, ct).ConfigureAwait(continueOnCapturedContext: false);
				if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests || httpResponseMessage.StatusCode >= HttpStatusCode.InternalServerError)
				{
					httpResponseMessage.Dispose();
					await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2.0, attempt)), ct).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				httpResponseMessage.EnsureSuccessStatusCode();
				return httpResponseMessage;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2) when (attempt < 4)
			{
				last = ex2;
				await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2.0, attempt)), ct).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		throw last ?? new HttpRequestException("Échec Microsoft Graph.");
	}

	private static int GetFreePort()
	{
		TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
		tcpListener.Start();
		int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
		tcpListener.Stop();
		return port;
	}
}
