using System;
using System.Collections.Generic;

namespace QuickOTP.Core.Rules;

public sealed class UserRules
{
	public List<string> AlwaysAcceptSenders { get; set; } = new List<string>();

	public List<string> AlwaysAcceptDomains { get; set; } = new List<string>();

	public List<string> NeverDetectSenders { get; set; } = new List<string>();

	public List<string> NeverDetectDomains { get; set; } = new List<string>();

	public Dictionary<string, string> ServiceOverrides { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public List<string> DisableAutoCopyServices { get; set; } = new List<string>();

	public List<FalsePositiveRecord> FalsePositives { get; set; } = new List<FalsePositiveRecord>();

	public static UserRules Empty { get; } = new UserRules();
}
