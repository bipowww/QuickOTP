namespace QuickOTP.Core.Totp;

public interface ITotpSecretStore
{
	void SaveEncryptedSecret(string accountId, byte[] secret);

	byte[]? LoadSecret(string accountId);

	void Delete(string accountId);
}
