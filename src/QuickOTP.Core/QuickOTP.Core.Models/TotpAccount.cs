namespace QuickOTP.Core.Models;

public sealed class TotpAccount
{
	public required string Id { get; init; }

	public required string Issuer { get; init; }

	public required string AccountName { get; init; }
}
