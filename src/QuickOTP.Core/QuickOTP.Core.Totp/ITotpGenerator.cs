using System;

namespace QuickOTP.Core.Totp;

public interface ITotpGenerator
{
	string GenerateCode(byte[] secret, DateTimeOffset utcNow);
}
