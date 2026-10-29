namespace QuickOTP.Core.Sources;

public interface ITotpOtpSource : IOtpSource
{
	string GenerateCode(string accountId);
}
