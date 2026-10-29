using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace QuickOTP.Core.Detection;

public static class DefaultServiceRules
{
	public static List<IServiceRule> Create()
	{
		int num = 10;
		List<IServiceRule> list = new List<IServiceRule>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<IServiceRule> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new KeywordServiceRule("Discord", new string[1] { "discord" }, 20);
		num2++;
		span[num2] = new KeywordServiceRule("Amazon", new string[2] { "amazon", "aws" }, 20);
		num2++;
		span[num2] = new KeywordServiceRule("PayPal", new string[1] { "paypal" }, 20);
		num2++;
		span[num2] = new KeywordServiceRule("Microsoft", new string[4] { "microsoft", "outlook", "xbox", "azure" }, 15);
		num2++;
		span[num2] = new KeywordServiceRule("Google", new string[3] { "google", "gmail", "youtube" }, 15);
		num2++;
		span[num2] = new KeywordServiceRule("GitHub", new string[1] { "github" }, 20);
		num2++;
		span[num2] = new KeywordServiceRule("Apple", new string[2] { "apple", "icloud" }, 15);
		num2++;
		span[num2] = new KeywordServiceRule("Steam", new string[1] { "steam" }, 20);
		num2++;
		span[num2] = new KeywordServiceRule("Slack", new string[1] { "slack" }, 20);
		num2++;
		span[num2] = new KeywordServiceRule("Bank", new string[5] { "bank", "banque", "revolut", "wise", "n26" }, 10);
		num2++;
		return list;
	}
}
