namespace QuickOTP.Core.Models;

public sealed class AccountInfo
{
	public required string Id { get; set; }

	public required AccountProvider Provider { get; set; }

	public string? Email { get; set; }

	public string? DisplayName { get; set; }

	public bool Enabled { get; set; } = true;

	public string? LastError { get; set; }

	public AccountConnectionStatus Status { get; set; }

	public string? SyncCursor { get; set; }
}
