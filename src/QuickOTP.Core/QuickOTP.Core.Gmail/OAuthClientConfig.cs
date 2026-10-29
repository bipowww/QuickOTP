using System;
using System.IO;
using System.Text.Json;

namespace QuickOTP.Core.Gmail;

public sealed class OAuthClientConfig
{
	public string ClientId { get; init; } = "";

	public string ClientSecret { get; init; } = "";

	public bool IsValid
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret))
			{
				return !ClientId.Contains("YOUR_CLIENT_ID", StringComparison.Ordinal);
			}
			return false;
		}
	}

	public static OAuthClientConfig LoadFromFile(string path)
	{
		if (!File.Exists(path))
		{
			return new OAuthClientConfig();
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			JsonElement value2;
			JsonElement jsonElement = (rootElement.TryGetProperty("installed", out value) ? value : (rootElement.TryGetProperty("web", out value2) ? value2 : rootElement));
			JsonElement value3;
			return new OAuthClientConfig
			{
				ClientId = (jsonElement.GetProperty("client_id").GetString() ?? ""),
				ClientSecret = (jsonElement.TryGetProperty("client_secret", out value3) ? (value3.GetString() ?? "") : "")
			};
		}
		catch
		{
			return new OAuthClientConfig();
		}
	}

	public static OAuthClientConfig Discover(string appDirectory)
	{
		string[] array = new string[3]
		{
			Path.Combine(appDirectory, "client_secret.json"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP", "client_secret.json"),
			Path.Combine(AppContext.BaseDirectory, "client_secret.json")
		};
		for (int i = 0; i < array.Length; i++)
		{
			OAuthClientConfig oAuthClientConfig = LoadFromFile(array[i]);
			if (oAuthClientConfig.IsValid)
			{
				return oAuthClientConfig;
			}
		}
		return new OAuthClientConfig();
	}
}
