namespace QuickOTP.Core.Security;

public interface IAccountTokenVault
{
	void Save(string accountId, StoredTokens tokens);

	StoredTokens? Load(string accountId);

	void Clear(string accountId);

	bool HasTokens(string accountId);

	ITokenStorage ForAccount(string accountId);
}
