namespace QuickOTP.Core.Models;

public sealed class CodeDetectionResult
{
	public const int MinConfidenceScore = 60;

	public required string Code { get; init; }

	public required int Score { get; init; }

	public string? Service { get; init; }

	public string? Context { get; init; }

	public bool IsConfident => Score >= 60;
}
