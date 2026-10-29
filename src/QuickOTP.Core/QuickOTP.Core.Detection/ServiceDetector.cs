using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Detection;

public sealed class ServiceDetector
{
	private readonly List<ServiceMapping> _mappings;

	private readonly Dictionary<string, string> _overrides;

	public ServiceDetector(IEnumerable<ServiceMapping>? mappings = null, IDictionary<string, string>? overrides = null)
	{
		_mappings = mappings?.ToList() ?? DefaultMappings();
		_overrides = new Dictionary<string, string>(overrides ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
	}

	public string? Detect(MessageCandidate message, string? fallbackFromBody = null)
	{
		string text = message.Sender ?? "";
		string text2 = ExtractEmail(text);
		string domain = ExtractDomain(text2);
		if (!string.IsNullOrEmpty(text2) && _overrides.TryGetValue(text2, out string value))
		{
			return value;
		}
		if (!string.IsNullOrEmpty(domain) && _overrides.TryGetValue(domain, out string value2))
		{
			return value2;
		}
		string haystack = string.Join(' ', message.SenderName ?? "", text, message.Subject ?? "", fallbackFromBody ?? "");
		foreach (ServiceMapping mapping in _mappings)
		{
			if (mapping.Domains.Any((string d) => domain.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
			{
				return mapping.Name;
			}
			if (mapping.Keywords.Any((string k) => haystack.Contains(k, StringComparison.OrdinalIgnoreCase)))
			{
				return mapping.Name;
			}
		}
		if (!string.IsNullOrEmpty(domain))
		{
			string text3 = domain.Split('.')[0];
			if (text3.Length >= 3)
			{
				ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(char.ToUpperInvariant(text3[0]));
				string text4 = text3;
				return string.Concat(readOnlySpan, text4.Substring(1, text4.Length - 1));
			}
		}
		return null;
	}

	public static string ExtractEmail(string from)
	{
		Match match = Regex.Match(from, "[\\w.+\\-]+@[\\w.\\-]+\\.\\w+");
		if (!match.Success)
		{
			return from.Trim().ToLowerInvariant();
		}
		return match.Value.ToLowerInvariant();
	}

	public static string ExtractDomain(string email)
	{
		int num = email.LastIndexOf('@');
		if (num < 0)
		{
			return "";
		}
		int num2 = num + 1;
		return email.Substring(num2, email.Length - num2).TrimEnd('>');
	}

	public static List<ServiceMapping> DefaultMappings()
	{
		int num = 25;
		List<ServiceMapping> list = new List<ServiceMapping>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<ServiceMapping> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new ServiceMapping("Discord", new string[2] { "discord.com", "discordapp.com" }, new string[1] { "discord" });
		num2++;
		span[num2] = new ServiceMapping("Google", new string[3] { "google.com", "gmail.com", "accounts.google.com" }, new string[2] { "google verification", "google" });
		num2++;
		span[num2] = new ServiceMapping("Microsoft", new string[4] { "microsoft.com", "live.com", "outlook.com", "accountprotection.microsoft.com" }, new string[3] { "microsoft", "xbox", "azure" });
		num2++;
		span[num2] = new ServiceMapping("Amazon", new string[3] { "amazon.com", "amazon.fr", "amazonaws.com" }, new string[2] { "amazon", "aws" });
		num2++;
		span[num2] = new ServiceMapping("PayPal", new string[2] { "paypal.com", "paypal.fr" }, new string[1] { "paypal" });
		num2++;
		span[num2] = new ServiceMapping("Steam", new string[2] { "steampowered.com", "steamcommunity.com" }, new string[1] { "steam" });
		num2++;
		span[num2] = new ServiceMapping("GitHub", new string[2] { "github.com", "noreply.github.com" }, new string[1] { "github" });
		num2++;
		span[num2] = new ServiceMapping("Apple", new string[2] { "apple.com", "icloud.com" }, new string[2] { "apple", "icloud" });
		num2++;
		span[num2] = new ServiceMapping("Reddit", new string[1] { "reddit.com" }, new string[1] { "reddit" });
		num2++;
		span[num2] = new ServiceMapping("LinkedIn", new string[1] { "linkedin.com" }, new string[1] { "linkedin" });
		num2++;
		span[num2] = new ServiceMapping("Instagram", new string[2] { "instagram.com", "mail.instagram.com" }, new string[1] { "instagram" });
		num2++;
		span[num2] = new ServiceMapping("Facebook", new string[3] { "facebookmail.com", "facebook.com", "meta.com" }, new string[2] { "facebook", "meta" });
		num2++;
		span[num2] = new ServiceMapping("X", new string[2] { "x.com", "twitter.com" }, new string[2] { "twitter", "x.com" });
		num2++;
		span[num2] = new ServiceMapping("TikTok", new string[1] { "tiktok.com" }, new string[1] { "tiktok" });
		num2++;
		span[num2] = new ServiceMapping("Stripe", new string[1] { "stripe.com" }, new string[1] { "stripe" });
		num2++;
		span[num2] = new ServiceMapping("Shopify", new string[1] { "shopify.com" }, new string[1] { "shopify" });
		num2++;
		span[num2] = new ServiceMapping("OpenAI", new string[1] { "openai.com" }, new string[2] { "openai", "chatgpt" });
		num2++;
		span[num2] = new ServiceMapping("Slack", new string[1] { "slack.com" }, new string[1] { "slack" });
		num2++;
		span[num2] = new ServiceMapping("Dropbox", new string[1] { "dropbox.com" }, new string[1] { "dropbox" });
		num2++;
		span[num2] = new ServiceMapping("Netflix", new string[1] { "netflix.com" }, new string[1] { "netflix" });
		num2++;
		span[num2] = new ServiceMapping("Spotify", new string[1] { "spotify.com" }, new string[1] { "spotify" });
		num2++;
		span[num2] = new ServiceMapping("Uber", new string[1] { "uber.com" }, new string[1] { "uber" });
		num2++;
		span[num2] = new ServiceMapping("Airbnb", new string[1] { "airbnb.com" }, new string[1] { "airbnb" });
		num2++;
		span[num2] = new ServiceMapping("Coinbase", new string[1] { "coinbase.com" }, new string[1] { "coinbase" });
		num2++;
		span[num2] = new ServiceMapping("Binance", new string[1] { "binance.com" }, new string[1] { "binance" });
		num2++;
		return list;
	}
}
