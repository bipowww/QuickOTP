using System;

namespace QuickOTP.Core.Logging;

public interface IAppLogger
{
	void Info(string message);

	void Warn(string message);

	void Error(string message, Exception? ex = null);

	void Diagnostic(string message);
}
