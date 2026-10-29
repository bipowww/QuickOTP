using System.Collections.Generic;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Detection;

public interface IServiceRule
{
	void Boost(string text, List<CodeDetectionResult> candidates);

	string? TryDetectService(string text);
}
