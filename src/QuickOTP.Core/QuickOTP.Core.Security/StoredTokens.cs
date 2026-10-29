using System;

namespace QuickOTP.Core.Security;

public sealed class StoredTokens
{
	public required string RefreshToken { get; init; }

	public string? AccessToken { get; init; }

	public DateTimeOffset? AccessTokenExpiry { get; init; }

	public string? Email { get; init; }
}
