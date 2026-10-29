using QuickOTP.Core.Models;

namespace QuickOTP.Core.Detection;

public sealed class DetectionOptions
{
	public bool EnableDigitCodes { get; init; } = true;

	public bool EnableAlphanumericCodes { get; init; } = true;

	public bool EnableHyphenatedCodes { get; init; } = true;

	public static DetectionOptions Default { get; } = new DetectionOptions();

	public static DetectionOptions FromSettings(AppSettings settings)
	{
		return new DetectionOptions
		{
			EnableDigitCodes = settings.EnableDigitCodes,
			EnableAlphanumericCodes = settings.EnableAlphanumericCodes,
			EnableHyphenatedCodes = settings.EnableHyphenatedCodes
		};
	}
}
