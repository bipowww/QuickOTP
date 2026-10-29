using System;
using System.Linq;
using QuickOTP.Core.Detection;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Rules;

public sealed class UserRulesEngine
{
	private UserRules _rules;

	public UserRules Current => _rules;

	public UserRulesEngine(UserRules rules)
	{
		_rules = rules;
	}

	public void Replace(UserRules rules)
	{
		_rules = rules;
	}

	public bool ShouldIgnore(MessageCandidate message)
	{
		string email = ServiceDetector.ExtractEmail(message.Sender ?? "");
		string domain = ServiceDetector.ExtractDomain(email);
		if (_rules.NeverDetectSenders.Any((string s) => email.Equals(s, StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		if (_rules.NeverDetectDomains.Any((string d) => domain.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		return false;
	}

	public bool IsFalsePositive(MessageCandidate message, string code)
	{
		string email = ServiceDetector.ExtractEmail(message.Sender ?? "");
		string domain = ServiceDetector.ExtractDomain(email);
		return _rules.FalsePositives.Any((FalsePositiveRecord fp) => (!string.IsNullOrEmpty(fp.Sender) && email.Equals(fp.Sender, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrEmpty(fp.Domain) && domain.EndsWith(fp.Domain, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrEmpty(fp.CodePattern) && code.Equals(fp.CodePattern, StringComparison.OrdinalIgnoreCase)));
	}

	public string? ResolveServiceOverride(MessageCandidate message)
	{
		string text = ServiceDetector.ExtractEmail(message.Sender ?? "");
		string key = ServiceDetector.ExtractDomain(text);
		if (_rules.ServiceOverrides.TryGetValue(text, out string value))
		{
			return value;
		}
		if (_rules.ServiceOverrides.TryGetValue(key, out value))
		{
			return value;
		}
		return null;
	}

	public bool IsAutoCopyDisabled(string? service)
	{
		if (string.IsNullOrEmpty(service))
		{
			return false;
		}
		return _rules.DisableAutoCopyServices.Any((string x) => x.Equals(service, StringComparison.OrdinalIgnoreCase));
	}

	public void MarkFalsePositive(MessageCandidate? message, string code)
	{
		string text = ServiceDetector.ExtractEmail(message?.Sender ?? "");
		string text2 = ServiceDetector.ExtractDomain(text);
		_rules.FalsePositives.Add(new FalsePositiveRecord
		{
			Sender = (string.IsNullOrEmpty(text) ? null : text),
			Domain = (string.IsNullOrEmpty(text2) ? null : text2),
			CodePattern = code
		});
	}

	public bool IsAlwaysAccept(MessageCandidate message)
	{
		string email = ServiceDetector.ExtractEmail(message.Sender ?? "");
		string domain = ServiceDetector.ExtractDomain(email);
		if (!_rules.AlwaysAcceptSenders.Any((string s) => email.Equals(s, StringComparison.OrdinalIgnoreCase)))
		{
			return _rules.AlwaysAcceptDomains.Any((string d) => domain.EndsWith(d, StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}
}
