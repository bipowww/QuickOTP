using System;
using System.Collections.Generic;
using System.Linq;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Detection;

public sealed class KeywordServiceRule(string serviceName, string[] keywords, int boost) : IServiceRule
{
	public void Boost(string text, List<CodeDetectionResult> candidates)
	{
		if (keywords.Any((string k) => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
		{
			for (int num = 0; num < candidates.Count; num++)
			{
				CodeDetectionResult codeDetectionResult = candidates[num];
				candidates[num] = new CodeDetectionResult
				{
					Code = codeDetectionResult.Code,
					Score = codeDetectionResult.Score + boost,
					Service = (codeDetectionResult.Service ?? serviceName),
					Context = codeDetectionResult.Context
				};
			}
		}
	}

	public string? TryDetectService(string text)
	{
		if (!keywords.Any((string k) => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
		{
			return null;
		}
		return serviceName;
	}
}
