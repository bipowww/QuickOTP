using System;

namespace QuickOTP.Core.Security;

public interface ITokenStorage
{
	void SaveTokens(string refreshToken, string? accessToken, DateTimeOffset? accessTokenExpiry, string? email);

	StoredTokens? LoadTokens();

	void Clear();

	bool HasTokens();
}
