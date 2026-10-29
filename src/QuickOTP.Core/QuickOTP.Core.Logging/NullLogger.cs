using System;

namespace QuickOTP.Core.Logging;

public sealed class NullLogger : IAppLogger
{
	public static NullLogger Instance { get; } = new NullLogger();

	public void Info(string message)
	{
	}

	public void Warn(string message)
	{
	}

	public void Error(string message, Exception? ex = null)
	{
	}

	public void Diagnostic(string message)
	{
	}
}
