using System;
using System.Threading;
using System.Threading.Tasks;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Sources;

public interface IMessageSource : IAsyncDisposable
{
	string AccountId { get; }

	string? AccountEmail { get; }

	MessageSourceKind Kind { get; }

	bool IsConnected { get; }

	bool IsEnabled { get; set; }

	string? LastError { get; }

	Task<bool> InitializeAsync(CancellationToken ct = default(CancellationToken));

	Task<SourcePollResult> PollAsync(CancellationToken ct = default(CancellationToken));

	Task DisconnectAsync();
}
