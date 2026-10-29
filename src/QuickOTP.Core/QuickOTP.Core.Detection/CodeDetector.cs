using System;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using QuickOTP.Core.Models;

namespace QuickOTP.Core.Detection;

public sealed partial class CodeDetector
{
	private readonly List<IServiceRule> _serviceRules;

	private readonly DetectionOptions _options;

	private static readonly (string Pattern, string Name)[] KnownServices = new(string, string)[22]
	{
		("discord", "Discord"),
		("amazon", "Amazon"),
		("paypal", "PayPal"),
		("microsoft", "Microsoft"),
		("google", "Google"),
		("github", "GitHub"),
		("apple", "Apple"),
		("facebook", "Facebook"),
		("instagram", "Instagram"),
		("twitter", "Twitter"),
		("x.com", "X"),
		("linkedin", "LinkedIn"),
		("steam", "Steam"),
		("twitch", "Twitch"),
		("slack", "Slack"),
		("dropbox", "Dropbox"),
		("netflix", "Netflix"),
		("spotify", "Spotify"),
		("uber", "Uber"),
		("airbnb", "Airbnb"),
		("binance", "Binance"),
		("coinbase", "Coinbase")
	};

	public CodeDetector(DetectionOptions? options = null, IEnumerable<IServiceRule>? serviceRules = null)
	{
		_options = options ?? DetectionOptions.Default;
		_serviceRules = serviceRules?.ToList() ?? DefaultServiceRules.Create();
	}

	public CodeDetectionResult? Detect(string? subject, string? plainBody, string? htmlBody = null)
	{
		string text = BuildSearchText(subject, plainBody, htmlBody);
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string text2 = NormalizeText(text);
		List<CodeDetectionResult> list = FindCandidates(text2);
		if (list.Count == 0)
		{
			return null;
		}
		foreach (IServiceRule serviceRule in _serviceRules)
		{
			serviceRule.Boost(text2, list);
		}
		return (from c in list
			where c.Score >= 60
			orderby c.Score descending, c.Code.Length descending
			select c).FirstOrDefault();
	}

	public CodeDetectionResult? DetectFromEmail(string? subject, string? body)
	{
		return Detect(subject, body);
	}

	private static string BuildSearchText(string? subject, string? plainBody, string? htmlBody)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (!string.IsNullOrWhiteSpace(subject))
		{
			stringBuilder.AppendLine(subject);
			stringBuilder.AppendLine();
		}
		if (!string.IsNullOrWhiteSpace(plainBody))
		{
			stringBuilder.AppendLine(plainBody);
		}
		else if (!string.IsNullOrWhiteSpace(htmlBody))
		{
			stringBuilder.AppendLine(HtmlToText(htmlBody));
		}
		return stringBuilder.ToString();
	}

	public static string HtmlToText(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return string.Empty;
		}
		try
		{
			string input = html;
			input = ScriptStyleRegex().Replace(input, " ");
			input = TagRegex().Replace(input, " ");
			input = WebUtility.HtmlDecode(input);
			input = WhitespaceRegex().Replace(input, " ");
			return input.Trim();
		}
		catch
		{
			return WhitespaceRegex().Replace(TagRegex().Replace(html, " "), " ").Trim();
		}
	}

	private static string NormalizeText(string text)
	{
		string input = text.Normalize(NormalizationForm.FormKC);
		input = WhitespaceRegex().Replace(input, " ");
		return input.Trim();
	}

	private List<CodeDetectionResult> FindCandidates(string text)
	{
		List<CodeDetectionResult> list = new List<CodeDetectionResult>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (_options.EnableDigitCodes)
		{
			CollectMatches(text, GroupedDigitCodeRegex(), list, seen, 55, normalizeDigits: true);
			CollectMatches(text, DigitCodeRegex(), list, seen, 40);
		}
		if (_options.EnableHyphenatedCodes)
		{
			CollectMatches(text, HyphenCodeRegex(), list, seen, 45);
		}
		if (_options.EnableAlphanumericCodes)
		{
			CollectMatches(text, AlphaNumCodeRegex(), list, seen, 35);
		}
		return list;
	}

	private void CollectMatches(string text, Regex regex, List<CodeDetectionResult> results, HashSet<string> seen, int baseScore, bool normalizeDigits = false)
	{
		foreach (Match item in regex.Matches(text))
		{
			string text2 = item.Value.Trim();
			string text3 = (normalizeDigits ? new string(text2.Where(char.IsDigit).ToArray()) : text2);
			if (!string.IsNullOrEmpty(text3) && seen.Add(text3) && (!normalizeDigits || (text3.Length >= 4 && text3.Length <= 10)) && !IsLikelyFalsePositive(text3, text, item.Index))
			{
				int score = baseScore + ScoreContext(text, item.Index, item.Length, text3);
				string service = DetectService(text);
				int num = Math.Max(0, item.Index - 40);
				int length = Math.Min(text.Length - num, item.Length + 80);
				string context = text.Substring(num, length);
				results.Add(new CodeDetectionResult
				{
					Code = text3,
					Score = score,
					Service = service,
					Context = context
				});
			}
		}
	}

	private int ScoreContext(string text, int index, int length, string code)
	{
		int num = 0;
		int num2 = Math.Max(0, index - 80);
		int num3 = Math.Min(text.Length, index + length + 40);
		int num4 = num2;
		string text2 = text.Substring(num4, index - num4);
		num4 = index;
		text.Substring(num4, num3 - num4);
		num4 = num2;
		string text3 = text.Substring(num4, num3 - num4);
		string text4 = text2.ToLowerInvariant();
		string text5 = text3.ToLowerInvariant();
		num += ScorePhrase(text4, "verification code", 100);
		num += ScorePhrase(text4, "security code", 90);
		num += ScorePhrase(text4, "authentication code", 90);
		num += ScorePhrase(text4, "confirmation code", 85);
		num += ScorePhrase(text4, "login code", 85);
		num += ScorePhrase(text4, "sign-in code", 85);
		num += ScorePhrase(text4, "signin code", 85);
		num += ScorePhrase(text4, "sign in code", 85);
		num += ScorePhrase(text4, "one-time code", 90);
		num += ScorePhrase(text4, "one time code", 90);
		num += ScorePhrase(text4, "one-time password", 95);
		num += ScorePhrase(text4, "one time password", 95);
		num += ScorePhrase(text4, "temporary code", 80);
		num += ScorePhrase(text4, "passcode", 80);
		num += ScorePhrase(text4, "otp", 80);
		num += ScorePhrase(text4, "2fa", 75);
		num += ScorePhrase(text4, "mfa", 75);
		num += ScorePhrase(text4, "verify", 50);
		num += ScorePhrase(text4, "verification", 55);
		num += ScorePhrase(text4, "code de vÃ©rification", 100);
		num += ScorePhrase(text4, "code de verification", 100);
		num += ScorePhrase(text4, "code de sÃ©curitÃ©", 90);
		num += ScorePhrase(text4, "code de securite", 90);
		num += ScorePhrase(text4, "code de connexion", 85);
		num += ScorePhrase(text4, "code d'authentification", 90);
		num += ScorePhrase(text4, "code d authentification", 90);
		num += ScorePhrase(text4, "code de confirmation", 85);
		num += ScorePhrase(text4, "code temporaire", 80);
		num += ScorePhrase(text4, "mot de passe Ã  usage unique", 95);
		num += ScorePhrase(text4, "mot de passe a usage unique", 95);
		num += ScorePhrase(text4, "votre code", 70);
		num += ScorePhrase(text4, "le code", 40);
		if (Regex.IsMatch(text5, "\\b(use|enter|saisir|utilisez)\\s+[\\d\\s\\-]{4,14}\\s+to\\s+(sign\\s*in|log\\s*in|continue|verify)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			num += 85;
		}
		if (CodeIsPattern().IsMatch(text4))
		{
			num += 70;
		}
		if (UseCodePattern().IsMatch(text4 + " " + code.ToLowerInvariant()))
		{
			num += 65;
		}
		if (EnterCodePattern().IsMatch(text4))
		{
			num += 60;
		}
		if (Regex.IsMatch(code, "^\\d{6,10}$") && IsIsolated(text, index, length))
		{
			num += 50;
		}
		else if (Regex.IsMatch(code, "^\\d{4,5}$") && IsIsolated(text, index, length))
		{
			num += 25;
		}
		num += ScoreNegative(text5, "order", -50);
		num += ScoreNegative(text5, "invoice", -50);
		num += ScoreNegative(text5, "facture", -50);
		num += ScoreNegative(text5, "phone", -50);
		num += ScoreNegative(text5, "tÃ©lÃ©phone", -50);
		num += ScoreNegative(text5, "telephone", -50);
		num += ScoreNegative(text5, "tel:", -60);
		num += ScoreNegative(text5, "shipping", -40);
		num += ScoreNegative(text5, "tracking", -50);
		num += ScoreNegative(text5, "commande", -50);
		num += ScoreNegative(text5, "receipt", -40);
		num += ScoreNegative(text5, "transaction", -40);
		num += ScoreNegative(text5, "account number", -60);
		num += ScoreNegative(text5, "card ending", -70);
		num += ScoreNegative(text5, "customer id", -60);
		num += ScoreNegative(text5, "reference", -30);
		num += ScoreNegative(text5, "zip", -40);
		num += ScoreNegative(text5, "postal", -40);
		if (PriceNearRegex().IsMatch(text3))
		{
			num -= 70;
		}
		if (LooksLikeDate(code, text5))
		{
			num -= 70;
		}
		if (Regex.IsMatch(code, "^(19|20)\\d{2}$"))
		{
			num -= 80;
		}
		if (PhoneNearRegex().IsMatch(text3))
		{
			num -= 50;
		}
		if (OrderHashRegex().IsMatch(text4 + code))
		{
			num -= 80;
		}
		return num;
	}

	private static int ScorePhrase(string beforeLower, string phrase, int points)
	{
		int num = beforeLower.LastIndexOf(phrase, StringComparison.Ordinal);
		if (num < 0)
		{
			return 0;
		}
		int num2 = beforeLower.Length - (num + phrase.Length);
		if (num2 > 60)
		{
			return points / 3;
		}
		if (num2 > 30)
		{
			return points / 2;
		}
		return points;
	}

	private static int ScoreNegative(string windowLower, string phrase, int points)
	{
		if (!windowLower.Contains(phrase, StringComparison.Ordinal))
		{
			return 0;
		}
		return points;
	}

	private static bool IsIsolated(string text, int index, int length)
	{
		bool num = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
		int num2 = index + length;
		bool flag = num2 >= text.Length || !char.IsLetterOrDigit(text[num2]);
		return num && flag;
	}

	private bool IsLikelyFalsePositive(string code, string text, int index)
	{
		if (Regex.IsMatch(code, "^(19|20)\\d{2}$"))
		{
			return true;
		}
		if (Regex.IsMatch(code, "^\\d{3}-\\d{3}$") && !text.Contains("code", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (code.Length <= 4 && code.All(char.IsLetter))
		{
			return true;
		}
		if (Regex.IsMatch(code, "^[A-Fa-f0-9]{6}$") && index > 0 && text[index - 1] == '#')
		{
			return true;
		}
		return false;
	}

	private static bool LooksLikeDate(string code, string windowLower)
	{
		if (DateWordRegex().IsMatch(windowLower))
		{
			return true;
		}
		if (Regex.IsMatch(code, "^\\d{1,2}[-/]\\d{1,2}$"))
		{
			return true;
		}
		return false;
	}

	private string? DetectService(string text)
	{
		foreach (IServiceRule serviceRule in _serviceRules)
		{
			string text2 = serviceRule.TryDetectService(text);
			if (!string.IsNullOrEmpty(text2))
			{
				return text2;
			}
		}
		(string, string)[] knownServices = KnownServices;
		for (int i = 0; i < knownServices.Length; i++)
		{
			var (value, result) = knownServices[i];
			if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
			{
				return result;
			}
		}
		return null;
	}

	[GeneratedRegex("\\b\\d{4,10}\\b", RegexOptions.CultureInvariant)]
	private static partial Regex DigitCodeRegex();

	[GeneratedRegex("\\b(?:\\d{4}[\\s\\-]\\d{4}|\\d{2}(?:[\\s\\-]\\d{2}){3}|\\d{3}[\\s\\-]\\d{3}[\\s\\-]\\d{2,4}|\\d{5}[\\s\\-]\\d{5})\\b", RegexOptions.CultureInvariant)]
	private static partial Regex GroupedDigitCodeRegex();

	[GeneratedRegex("\\b(?=[A-Z0-9]*\\d)[A-Z0-9]{4,10}\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex AlphaNumCodeRegex();

	[GeneratedRegex("\\b(?:\\d{3}-\\d{3}|\\d{4}-\\d{4}|[A-Z0-9]{2,6}-[A-Z0-9]{2,4})\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex HyphenCodeRegex();

	[GeneratedRegex("<(script|style)[^>]*>[\\s\\S]*?</\\1>", RegexOptions.IgnoreCase)]
	private static partial Regex ScriptStyleRegex();

	[GeneratedRegex("<[^>]+>")]
	private static partial Regex TagRegex();

	[GeneratedRegex("\\s+")]
	private static partial Regex WhitespaceRegex();

	[GeneratedRegex("(code|otp|passcode)\\s*(is|:|=|\\-|â€“)?\\s*$", RegexOptions.IgnoreCase)]
	private static partial Regex CodeIsPattern();

	[GeneratedRegex("\\b(use|enter|saisir|utilisez)\\s+[\\d\\s\\-]{4,14}\\b", RegexOptions.IgnoreCase)]
	private static partial Regex UseCodePattern();

	[GeneratedRegex("(enter|saisir|tapez|type)\\s+(the\\s+)?(code|otp)", RegexOptions.IgnoreCase)]
	private static partial Regex EnterCodePattern();

	[GeneratedRegex("[â‚¬$Â£Â¥]\\s*\\d|(\\d+[.,]\\d{2}\\s*[â‚¬$Â£Â¥])|total.*(is|:)", RegexOptions.IgnoreCase)]
	private static partial Regex PriceNearRegex();

	[GeneratedRegex("\\b(january|february|march|april|may|june|july|august|september|october|november|december|janvier|fÃ©vrier|fevrier|mars|avril|mai|juin|juillet|aoÃ»t|aout|septembre|octobre|novembre|dÃ©cembre|decembre)\\b", RegexOptions.IgnoreCase)]
	private static partial Regex DateWordRegex();

	[GeneratedRegex("(\\+?\\d[\\d\\s\\-().]{8,}\\d)")]
	private static partial Regex PhoneNearRegex();

	[GeneratedRegex("order\\s*#?\\s*\\d+", RegexOptions.IgnoreCase)]
	private static partial Regex OrderHashRegex();

}
