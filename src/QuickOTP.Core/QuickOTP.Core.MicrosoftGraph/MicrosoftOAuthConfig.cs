using System;
using System.IO;
using System.Text.Json;

namespace QuickOTP.Core.MicrosoftGraph;

public sealed class MicrosoftOAuthConfig
{
	public string ClientId { get; init; } = "";

	public string? ClientSecret { get; init; }

	public bool IsValid
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ClientId))
			{
				return !ClientId.Contains("YOUR_", StringComparison.Ordinal);
			}
			return false;
		}
	}

	public static MicrosoftOAuthConfig LoadFromFile(string path)
	{
		if (!File.Exists(path))
		{
			return new MicrosoftOAuthConfig();
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			JsonElement value2;
			return new MicrosoftOAuthConfig
			{
				ClientId = (rootElement.TryGetProperty("client_id", out value) ? (value.GetString() ?? "") : ""),
				ClientSecret = (rootElement.TryGetProperty("client_secret", out value2) ? value2.GetString() : null)
			};
		}
		catch
		{
			return new MicrosoftOAuthConfig();
		}
	}

	public static MicrosoftOAuthConfig Discover()
	{
		string[] array = new string[2]
		{
			Path.Combine(AppContext.BaseDirectory, "msal_client.json"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP", "msal_client.json")
		};
		for (int i = 0; i < array.Length; i++)
		{
			MicrosoftOAuthConfig microsoftOAuthConfig = LoadFromFile(array[i]);
			if (microsoftOAuthConfig.IsValid)
			{
				return microsoftOAuthConfig;
			}
		}
		return new MicrosoftOAuthConfig();
	}
}
