using System;

namespace QuickOTP.Core.Monitoring;

public readonly record struct ApiEstimate(int HistoryList, int MessagesGet, int Total, int Accounts, TimeSpan Duration);
