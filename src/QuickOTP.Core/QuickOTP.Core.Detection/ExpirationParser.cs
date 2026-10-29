using System;

using System.Text.RegularExpressions;


namespace QuickOTP.Core.Detection;

public static partial class ExpirationParser
{
	public static DateTimeOffset? TryParseExpiry(string? text, DateTimeOffset receivedAt, TimeSpan defaultValidity)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return receivedAt + defaultValidity;
		}
		Match match = ExpiresInRegex().Match(text);
		if (match.Success && int.TryParse(match.Groups["n"].Value, out var result))
		{
			string text2 = match.Groups["unit"].Value.ToLowerInvariant();
			TimeSpan timeSpan = ((text2.StartsWith("h") || text2.StartsWith("heure")) ? TimeSpan.FromHours(result) : ((text2.StartsWith("s") && !text2.StartsWith("min")) ? TimeSpan.FromSeconds(result) : TimeSpan.FromMinutes(result)));
			return receivedAt + timeSpan;
		}
		match = ValidForRegex().Match(text);
		if (match.Success && int.TryParse(match.Groups["n"].Value, out result))
		{
			string text3 = match.Groups["unit"].Value.ToLowerInvariant();
			TimeSpan timeSpan2 = ((text3.StartsWith("h") || text3.StartsWith("heure")) ? TimeSpan.FromHours(result) : TimeSpan.FromMinutes(result));
			return receivedAt + timeSpan2;
		}
		return receivedAt + defaultValidity;
	}

	[GeneratedRegex("(?:expires?\\s+in|expire\\s+dans|valable\\s+(?:pendant|pour)|valid\\s+for|ce\\s+code\\s+expire\\s+dans)\\s+(?<n>\\d+)\\s*(?<unit>minutes?|mins?|min|hours?|hrs?|h|secondes?|secs?|s|heures?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ExpiresInRegex();

	[GeneratedRegex("(?:valid\\s+for|valable\\s+(?:pendant|pour))\\s+(?<n>\\d+)\\s*(?<unit>minutes?|mins?|hours?|hrs?|heures?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ValidForRegex();

}
