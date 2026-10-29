using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using QuickOTP.Core.Logging;
using QuickOTP.Core.Security;

namespace QuickOTP.Core.Gmail;

public sealed class GmailService : IAsyncDisposable
{
	private sealed class TokenResponse
	{
		[JsonPropertyName("access_token")]
		public string AccessToken { get; set; } = "";

		[JsonPropertyName("refresh_token")]
		public string? RefreshToken { get; set; }

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }

		[JsonPropertyName("token_type")]
		public string? TokenType { get; set; }
	}

	public const string ReadonlyScope = "https://www.googleapis.com/auth/gmail.readonly";

	public const string UserInfoEmailScope = "https://www.googleapis.com/auth/userinfo.email";

	private readonly ITokenStorage _tokenStorage;

	private readonly IAppLogger _logger;

	private readonly HttpClient _http;

	private readonly OAuthClientConfig _oauthConfig;

	private string? _accessToken;

	private DateTimeOffset _accessTokenExpiry;

	private string? _email;

	public bool IsConfigured => _oauthConfig.IsValid;

	public bool IsConnected => _tokenStorage.HasTokens();

	public string? ConnectedEmail
	{
		get
		{
			string? email = _email;
			if (email == null)
			{
				StoredTokens? storedTokens = _tokenStorage.LoadTokens();
				if (storedTokens == null)
				{
					return null;
				}
				email = storedTokens.Email;
			}
			return email;
		}
	}

	public GmailService(ITokenStorage tokenStorage, OAuthClientConfig oauthConfig, IAppLogger? logger = null, HttpClient? http = null)
	{
		_tokenStorage = tokenStorage;
		_oauthConfig = oauthConfig;
		_logger = logger ?? NullLogger.Instance;
		_http = http ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30.0)
		};
	}

	public async Task<bool> TryInitializeAsync(CancellationToken ct = default(CancellationToken))
	{
		StoredTokens storedTokens = _tokenStorage.LoadTokens();
		if (storedTokens == null)
		{
			return false;
		}
		_email = storedTokens.Email;
		_accessToken = storedTokens.AccessToken;
		_accessTokenExpiry = storedTokens.AccessTokenExpiry ?? DateTimeOffset.MinValue;
		if (string.IsNullOrEmpty(_accessToken) || _accessTokenExpiry < DateTimeOffset.UtcNow.AddMinutes(2.0))
		{
			await RefreshAccessTokenAsync(storedTokens.RefreshToken, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		return !string.IsNullOrEmpty(_accessToken);
	}

	public async Task<string> AuthorizeAsync(CancellationToken ct = default(CancellationToken))
	{
		if (!_oauthConfig.IsValid)
		{
			throw new InvalidOperationException("OAuth client_secret.json manquant ou invalide. Voir README.md pour la configuration Google Cloud.");
		}
		HttpListener listener = new HttpListener();
		string redirectUri = $"http://127.0.0.1:{GetFreePort()}/";
		listener.Prefixes.Add(redirectUri);
		listener.Start();
		try
		{
			string state = Guid.NewGuid().ToString("N");
			OpenBrowser("https://accounts.google.com/o/oauth2/v2/auth?client_id=" + Uri.EscapeDataString(_oauthConfig.ClientId) + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(redirectUri) + "&scope=" + Uri.EscapeDataString("https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/userinfo.email") + "&access_type=offline&prompt=consent&state=" + state);
			HttpListenerContext context = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			string code = context.Request.QueryString["code"];
			string returnedState = context.Request.QueryString["state"];
			string error = context.Request.QueryString["error"];
			string s = "<html><body style='font-family:Segoe UI;padding:2rem'><h2>QuickOTP</h2><p>Vous pouvez fermer cette fenêtre.</p></body></html>";
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			context.Response.ContentLength64 = bytes.Length;
			context.Response.ContentType = "text/html; charset=utf-8";
			await context.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(continueOnCapturedContext: false);
			context.Response.Close();
			if (!string.IsNullOrEmpty(error))
			{
				throw new InvalidOperationException("Autorisation Google refusée: " + error);
			}
			if (returnedState != state || string.IsNullOrEmpty(code))
			{
				throw new InvalidOperationException("Réponse OAuth invalide.");
			}
			await ExchangeCodeAsync(code, redirectUri, ct).ConfigureAwait(continueOnCapturedContext: false);
			return ConnectedEmail ?? "";
		}
		finally
		{
			listener.Stop();
			listener.Close();
		}
	}

	public void Disconnect()
	{
		_tokenStorage.Clear();
		_accessToken = null;
		_email = null;
		_accessTokenExpiry = DateTimeOffset.MinValue;
	}

	public async Task<ulong> GetProfileHistoryIdAsync(CancellationToken ct = default(CancellationToken))
	{
		await EnsureAccessTokenAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		using HttpRequestMessage req = CreateRequest(HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/profile");
		using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
		_email = jsonDocument.RootElement.GetProperty("emailAddress").GetString();
		return ReadHistoryId(jsonDocument.RootElement.GetProperty("historyId"));
	}

	public async Task<GmailPollResult> PollNewMessagesAsync(ulong? startHistoryId, CancellationToken ct = default(CancellationToken))
	{
		Stopwatch sw = Stopwatch.StartNew();
		DateTimeOffset pollStarted = DateTimeOffset.UtcNow;
		int apiCalls = 0;
		int historyCalls = 0;
		int getCalls = 0;
		await EnsureAccessTokenAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if ((!startHistoryId.HasValue || startHistoryId.GetValueOrDefault() == 0L) ? true : false)
		{
			apiCalls++;
			historyCalls++;
			ulong historyId = await GetProfileHistoryIdAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			return new GmailPollResult(Array.Empty<GmailMessage>(), historyId, IsBaseline: true, sw.Elapsed, apiCalls, WasHistoryRecovery: false, pollStarted, DateTimeOffset.UtcNow, historyCalls, getCalls);
		}
		try
		{
			HashSet<string> messageIds = new HashSet<string>(StringComparer.Ordinal);
			ulong newHistoryId = startHistoryId.Value;
			DateTimeOffset historyResponseAt = DateTimeOffset.UtcNow;
			string text = null;
			for (int page = 0; page < 10; page++)
			{
				string text2 = $"https://gmail.googleapis.com/gmail/v1/users/me/history?startHistoryId={startHistoryId}" + "&historyTypes=messageAdded&labelId=INBOX&maxResults=50";
				if (!string.IsNullOrEmpty(text))
				{
					text2 = text2 + "&pageToken=" + Uri.EscapeDataString(text);
				}
				using HttpRequestMessage req = CreateRequest(HttpMethod.Get, text2);
				apiCalls++;
				historyCalls++;
				using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
				historyResponseAt = DateTimeOffset.UtcNow;
				if (resp.StatusCode == HttpStatusCode.NotFound)
				{
					_logger.Warn("historyId obsolete — minimal recovery (not full mailbox sync).");
					return await RecoverStaleHistoryAsync(sw, apiCalls, historyCalls, getCalls, pollStarted, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
				if (jsonDocument.RootElement.TryGetProperty("historyId", out var value))
				{
					newHistoryId = ReadHistoryId(value);
				}
				if (jsonDocument.RootElement.TryGetProperty("history", out var value2))
				{
					foreach (JsonElement item in value2.EnumerateArray())
					{
						if (!item.TryGetProperty("messagesAdded", out var value3))
						{
							continue;
						}
						foreach (JsonElement item2 in value3.EnumerateArray())
						{
							if (item2.TryGetProperty("message", out var value4) && value4.TryGetProperty("id", out var value5))
							{
								string text3 = value5.GetString();
								if (!string.IsNullOrEmpty(text3))
								{
									messageIds.Add(text3);
								}
							}
						}
					}
				}
				text = (jsonDocument.RootElement.TryGetProperty("nextPageToken", out var value6) ? value6.GetString() : null);
				if (string.IsNullOrEmpty(text))
				{
					break;
				}
			}
			List<GmailMessage> messages = new List<GmailMessage>();
			foreach (string item3 in messageIds)
			{
				ct.ThrowIfCancellationRequested();
				apiCalls++;
				getCalls++;
				DateTimeOffset fetchStart = DateTimeOffset.UtcNow;
				GmailMessage gmailMessage = await GetMessageAsync(item3, ct).ConfigureAwait(continueOnCapturedContext: false);
				DateTimeOffset utcNow = DateTimeOffset.UtcNow;
				if (gmailMessage != null)
				{
					messages.Add(new GmailMessage
					{
						Id = gmailMessage.Id,
						ThreadId = gmailMessage.ThreadId,
						Subject = gmailMessage.Subject,
						From = gmailMessage.From,
						PlainBody = gmailMessage.PlainBody,
						HtmlBody = gmailMessage.HtmlBody,
						InternalDate = gmailMessage.InternalDate,
						FetchStartedAt = fetchStart,
						FetchEndedAt = utcNow
					});
				}
			}
			return new GmailPollResult(messages, newHistoryId, IsBaseline: false, sw.Elapsed, apiCalls, WasHistoryRecovery: false, pollStarted, historyResponseAt, historyCalls, getCalls);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			_logger.Error("Erreur lors du polling Gmail", ex2);
			throw;
		}
	}

	private async Task<GmailPollResult> RecoverStaleHistoryAsync(Stopwatch sw, int apiCalls, int historyCalls, int getCalls, DateTimeOffset pollStarted, CancellationToken ct)
	{
		apiCalls++;
		historyCalls++;
		ulong freshHistoryId = await GetProfileHistoryIdAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		DateTimeOffset historyResponseAt = DateTimeOffset.UtcNow;
		DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddMinutes(-5.0);
		string url = "https://gmail.googleapis.com/gmail/v1/users/me/messages?labelIds=INBOX&maxResults=10&q=" + Uri.EscapeDataString("newer_than:5m");
		using HttpRequestMessage listReq = CreateRequest(HttpMethod.Get, url);
		apiCalls++;
		using HttpResponseMessage listResp = await SendWithRetryAsync(listReq, ct).ConfigureAwait(continueOnCapturedContext: false);
		string json = await listResp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		List<string> list = new List<string>();
		using (JsonDocument jsonDocument = JsonDocument.Parse(json))
		{
			if (jsonDocument.RootElement.TryGetProperty("messages", out var value))
			{
				foreach (JsonElement item in value.EnumerateArray())
				{
					string text = item.GetProperty("id").GetString();
					if (!string.IsNullOrEmpty(text))
					{
						list.Add(text);
					}
				}
			}
		}
		List<GmailMessage> messages = new List<GmailMessage>();
		foreach (string item2 in list.Distinct<string>(StringComparer.Ordinal))
		{
			ct.ThrowIfCancellationRequested();
			apiCalls++;
			getCalls++;
			DateTimeOffset fetchStart = DateTimeOffset.UtcNow;
			GmailMessage gmailMessage = await GetMessageAsync(item2, ct).ConfigureAwait(continueOnCapturedContext: false);
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			if (gmailMessage != null && (!gmailMessage.InternalDate.HasValue || !(gmailMessage.InternalDate < cutoff)))
			{
				messages.Add(new GmailMessage
				{
					Id = gmailMessage.Id,
					ThreadId = gmailMessage.ThreadId,
					Subject = gmailMessage.Subject,
					From = gmailMessage.From,
					PlainBody = gmailMessage.PlainBody,
					HtmlBody = gmailMessage.HtmlBody,
					InternalDate = gmailMessage.InternalDate,
					FetchStartedAt = fetchStart,
					FetchEndedAt = utcNow
				});
			}
		}
		return new GmailPollResult(messages, freshHistoryId, IsBaseline: false, sw.Elapsed, apiCalls, WasHistoryRecovery: true, pollStarted, historyResponseAt, historyCalls, getCalls);
	}

	public async Task<GmailMessage?> GetMessageAsync(string messageId, CancellationToken ct = default(CancellationToken))
	{
		await EnsureAccessTokenAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		string url = "https://gmail.googleapis.com/gmail/v1/users/me/messages/" + messageId + "?format=full";
		using HttpRequestMessage req = CreateRequest(HttpMethod.Get, url);
		using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!resp.IsSuccessStatusCode)
		{
			return null;
		}
		return GmailMessageParser.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
	}

	private async Task ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
	{
		using FormUrlEncodedContent content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["code"] = code,
			["client_id"] = _oauthConfig.ClientId,
			["client_secret"] = _oauthConfig.ClientSecret,
			["redirect_uri"] = redirectUri,
			["grant_type"] = "authorization_code"
		});
		using HttpResponseMessage resp = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct).ConfigureAwait(continueOnCapturedContext: false);
		string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!resp.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Échec de l'échange du code OAuth.");
		}
		TokenResponse token = JsonSerializer.Deserialize<TokenResponse>(json) ?? throw new InvalidOperationException("Réponse token invalide.");
		_accessToken = token.AccessToken;
		_accessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 60);
		if (string.IsNullOrEmpty(token.RefreshToken))
		{
			throw new InvalidOperationException("Aucun refresh token reçu. Réessayez avec prompt=consent.");
		}
		await FetchEmailAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		_tokenStorage.SaveTokens(token.RefreshToken, _accessToken, _accessTokenExpiry, _email);
		_logger.Info("Compte Gmail connecté.");
	}

	private async Task RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
	{
		using FormUrlEncodedContent content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["client_id"] = _oauthConfig.ClientId,
			["client_secret"] = _oauthConfig.ClientSecret,
			["refresh_token"] = refreshToken,
			["grant_type"] = "refresh_token"
		});
		using HttpResponseMessage resp = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct).ConfigureAwait(continueOnCapturedContext: false);
		string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!resp.IsSuccessStatusCode)
		{
			_logger.Error("Refresh token rejeté — reconnexion requise.");
			_tokenStorage.Clear();
			throw new PermanentAuthException("Session Gmail expirée. Veuillez vous reconnecter.");
		}
		TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json) ?? throw new InvalidOperationException("Réponse refresh invalide.");
		_accessToken = tokenResponse.AccessToken;
		_accessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
		string email = _email ?? _tokenStorage.LoadTokens()?.Email;
		_tokenStorage.SaveTokens(refreshToken, _accessToken, _accessTokenExpiry, email);
	}

	private async Task EnsureAccessTokenAsync(CancellationToken ct)
	{
		if (string.IsNullOrEmpty(_accessToken) || !(_accessTokenExpiry > DateTimeOffset.UtcNow.AddMinutes(1.0)))
		{
			StoredTokens storedTokens = _tokenStorage.LoadTokens() ?? throw new InvalidOperationException("Non connecté à Gmail.");
			await RefreshAccessTokenAsync(storedTokens.RefreshToken, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task FetchEmailAsync(CancellationToken ct)
	{
		using HttpRequestMessage req = CreateRequest(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
		using HttpResponseMessage resp = await SendWithRetryAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
		using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
		if (jsonDocument.RootElement.TryGetProperty("email", out var value))
		{
			_email = value.GetString();
		}
	}

	private HttpRequestMessage CreateRequest(HttpMethod method, string url)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(method, url);
		httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
		return httpRequestMessage;
	}

	private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken ct)
	{
		Exception last = null;
		for (int attempt = 0; attempt < 5; attempt++)
		{
			try
			{
				using HttpRequestMessage clone = await CloneRequestAsync(request).ConfigureAwait(continueOnCapturedContext: false);
				clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
				HttpResponseMessage resp = await _http.SendAsync(clone, ct).ConfigureAwait(continueOnCapturedContext: false);
				int statusCode = (int)resp.StatusCode;
				if (statusCode == 429 || statusCode >= 500)
				{
					resp.Dispose();
					TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2.0, attempt));
					_logger.Warn($"Gmail HTTP {statusCode}, retry dans {delay.TotalSeconds}s");
					await Task.Delay(delay, ct).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				if (resp.StatusCode == HttpStatusCode.Unauthorized)
				{
					resp.Dispose();
					StoredTokens storedTokens = _tokenStorage.LoadTokens();
					if (storedTokens == null)
					{
						throw new PermanentAuthException("Accès Gmail révoqué. Reconnectez-vous.");
					}
					await RefreshAccessTokenAsync(storedTokens.RefreshToken, ct).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				if (resp.StatusCode == HttpStatusCode.NotFound)
				{
					return resp;
				}
				if (statusCode == 403)
				{
					string responseBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
					resp.Dispose();
					_logger.Error("Gmail HTTP 403 — API probablement non activée ou accès refusé.");
					throw new GmailApiException(403, "Gmail API refusée (403). Activez « Gmail API » dans Google Cloud Console pour ce projet, attendez 1–2 min, puis Retirer + Ajouter Gmail.", responseBody);
				}
				if (!resp.IsSuccessStatusCode)
				{
					string responseBody2 = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
					resp.Dispose();
					throw new GmailApiException(statusCode, $"Gmail HTTP {statusCode}", responseBody2);
				}
				return resp;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (PermanentAuthException)
			{
				throw;
			}
			catch (GmailApiException)
			{
				throw;
			}
			catch (Exception ex4) when (attempt < 4)
			{
				last = ex4;
				TimeSpan delay2 = TimeSpan.FromSeconds(Math.Pow(2.0, attempt));
				_logger.Warn($"Erreur réseau, retry dans {delay2.TotalSeconds}s");
				await Task.Delay(delay2, ct).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		throw last ?? new HttpRequestException("Échec Gmail après retries.");
	}

	private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
	{
		HttpRequestMessage clone = new HttpRequestMessage(request.Method, request.RequestUri);
		if (request.Content != null)
		{
			clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync().ConfigureAwait(continueOnCapturedContext: false));
			foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
			{
				clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}
		}
		foreach (KeyValuePair<string, IEnumerable<string>> header2 in request.Headers)
		{
			clone.Headers.TryAddWithoutValidation(header2.Key, header2.Value);
		}
		return clone;
	}

	private static void OpenBrowser(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Impossible d'ouvrir le navigateur pour OAuth.", innerException);
		}
	}

	private static int GetFreePort()
	{
		TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
		tcpListener.Start();
		int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
		tcpListener.Stop();
		return port;
	}

	private static ulong ReadHistoryId(JsonElement el)
	{
		switch (el.ValueKind)
		{
		case JsonValueKind.Number:
			return el.GetUInt64();
		case JsonValueKind.String:
		{
			if (ulong.TryParse(el.GetString(), out var result))
			{
				return result;
			}
			break;
		}
		}
		throw new InvalidOperationException($"historyId JSON invalide: {el.ValueKind}");
	}

	public ValueTask DisposeAsync()
	{
		_http.Dispose();
		return ValueTask.CompletedTask;
	}
}
