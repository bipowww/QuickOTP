using System;
using System.IO;
using System.Text.RegularExpressions;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Logging;

public sealed class FileAppLogger : IAppLogger
{
	private readonly string _path;

	private readonly object _lock = new object();

	private readonly LogLevelSetting _level;

	public FileAppLogger(LogLevelSetting level, string? path = null)
	{
		_level = level;
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");
		Directory.CreateDirectory(text);
		_path = path ?? Path.Combine(text, "app.log");
	}

	public FileAppLogger(bool enabled, string? path = null)
		: this(enabled ? LogLevelSetting.Diagnostic : LogLevelSetting.Off, path)
	{
	}

	public void Info(string message)
	{
		if (_level == LogLevelSetting.Diagnostic)
		{
			Write("INFO", Sanitize(message));
		}
	}

	public void Warn(string message)
	{
		if (_level >= LogLevelSetting.ErrorsOnly)
		{
			Write("WARN", Sanitize(message));
		}
	}

	public void Error(string message, Exception? ex = null)
	{
		if (_level != LogLevelSetting.Off)
		{
			Write("ERROR", Sanitize(message) + ((ex == null) ? "" : (" | " + ex.GetType().Name + ": " + Sanitize(ex.Message))));
		}
	}

	public void Diagnostic(string message)
	{
		if (_level == LogLevelSetting.Diagnostic)
		{
			Write("DIAG", Sanitize(message));
		}
	}

	private void Write(string level, string message)
	{
		lock (_lock)
		{
			try
			{
				File.AppendAllText(_path, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
			}
			catch
			{
			}
		}
	}

	public static string Sanitize(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return input;
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(input, "\\b\\d{4,10}\\b", "[REDACTED]"), "\\b[A-Z0-9]{4,10}\\b", "[REDACTED]"), "ya29\\.[A-Za-z0-9_\\-\\.]+", "[TOKEN]"), "1//[A-Za-z0-9_\\-\\.]+", "[TOKEN]"), "eyJ[A-Za-z0-9_\\-]+\\.[A-Za-z0-9_\\-]+", "[TOKEN]");
	}
}
