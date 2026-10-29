namespace QuickOTP.Core.Subscription;

public enum SubscriptionState
{
	Unknown,
	Trial,
	Active,
	GracePeriod,
	Expired,
	Cancelled
}
