using System.Threading;
using System.Threading.Tasks;

namespace QuickOTP.Core.Subscription;

public interface ISubscriptionService
{
	Task<SubscriptionStatus> GetStatusAsync(CancellationToken ct = default(CancellationToken));

	Task<SubscriptionStatus> RefreshAsync(CancellationToken ct = default(CancellationToken));

	Task StartCheckoutAsync(string planId, CancellationToken ct = default(CancellationToken));

	Task RestoreAsync(CancellationToken ct = default(CancellationToken));
}
