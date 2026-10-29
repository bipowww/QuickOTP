namespace QuickOTP.Core.Monitoring;

public enum PollingMode
{
	BudgetCooldown,
	Backoff,
	WakeBoost,
	NetworkRecovery,
	StartupBoost,
	RecentActivity,
	Normal,
	Idle,
	DeepIdle
}
