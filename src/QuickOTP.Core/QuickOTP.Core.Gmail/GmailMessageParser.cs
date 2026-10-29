using System;
using System.Text;
using System.Text.Json;

namespace QuickOTP.Core.Gmail;

public static class GmailMessageParser
{
	public static GmailMessage? Parse(string json)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement rootElement = jsonDocument.RootElement;
			string text = rootElement.GetProperty("id").GetString();
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			string subject = null;
			string text2 = null;
			if (rootElement.TryGetProperty("payload", out var value) && value.TryGetProperty("headers", out var value2))
			{
				foreach (JsonElement item in value2.EnumerateArray())
				{
					string text3 = item.GetProperty("name").GetString();
					string text4 = item.GetProperty("value").GetString();
					if (text3 != null)
					{
						if (text3.Equals("Subject", StringComparison.OrdinalIgnoreCase))
						{
							subject = text4;
						}
						else if (text3.Equals("From", StringComparison.OrdinalIgnoreCase))
						{
							text2 = text4;
						}
					}
				}
			}
			DateTimeOffset? internalDate = null;
			if (rootElement.TryGetProperty("internalDate", out var value3) && long.TryParse(value3.GetString(), out var result))
			{
				internalDate = DateTimeOffset.FromUnixTimeMilliseconds(result);
			}
			string plain = null;
			string html = null;
			if (rootElement.TryGetProperty("payload", out var value4))
			{
				ExtractBodies(value4, ref plain, ref html);
			}
			if (string.IsNullOrWhiteSpace(plain) && string.IsNullOrWhiteSpace(html) && rootElement.TryGetProperty("snippet", out var value5))
			{
				plain = value5.GetString();
			}
			JsonElement value6;
			return new GmailMessage
			{
				Id = text,
				ThreadId = (rootElement.TryGetProperty("threadId", out value6) ? value6.GetString() : null),
				Subject = subject,
				From = text2,
				PlainBody = plain,
				HtmlBody = html,
				InternalDate = internalDate
			};
		}
		catch
		{
			return null;
		}
	}

	private static void ExtractBodies(JsonElement part, ref string? plain, ref string? html)
	{
		if (part.TryGetProperty("mimeType", out var value) && part.TryGetProperty("body", out var value2) && value2.TryGetProperty("data", out var value3))
		{
			string text = value.GetString() ?? "";
			string text2 = value3.GetString();
			if (!string.IsNullOrEmpty(text2))
			{
				string text3 = DecodeBase64Url(text2);
				if (text.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
				{
					if (plain == null)
					{
						plain = text3;
					}
				}
				else if (text.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) && html == null)
				{
					html = text3;
				}
			}
		}
		if (!part.TryGetProperty("parts", out var value4))
		{
			return;
		}
		foreach (JsonElement item in value4.EnumerateArray())
		{
			ExtractBodies(item, ref plain, ref html);
		}
	}

	internal static string DecodeBase64Url(string input)
	{
		string text = input.Replace('-', '+').Replace('_', '/');
		switch (text.Length % 4)
		{
		case 2:
			text += "==";
			break;
		case 3:
			text += "=";
			break;
		}
		try
		{
			byte[] bytes = Convert.FromBase64String(text);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return string.Empty;
		}
	}
}
