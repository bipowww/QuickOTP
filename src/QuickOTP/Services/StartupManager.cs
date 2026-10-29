using System;
using System.IO;
using Microsoft.Win32;

namespace QuickOTP.Services;

public static class StartupManager
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "QuickOTP";

	public static void Apply(bool enable)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true) ?? Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (enable)
			{
				string text = ResolveAppExecutable();
				registryKey.SetValue("QuickOTP", "\"" + text + "\" --autostart");
			}
			else
			{
				registryKey.DeleteValue("QuickOTP", throwOnMissingValue: false);
			}
		}
		catch
		{
		}
	}

	public static bool IsEnabled()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			return registryKey?.GetValue("QuickOTP") != null;
		}
		catch
		{
			return false;
		}
	}

	public static string ResolveAppExecutable()
	{
		string installedExecutable = WindowsShellIntegration.InstalledExecutable;
		if (File.Exists(installedExecutable))
		{
			return installedExecutable;
		}
		string text = Path.Combine(AppContext.BaseDirectory, "QuickOTP.exe");
		if (File.Exists(text))
		{
			return text;
		}
		string processPath = Environment.ProcessPath;
		if (!string.IsNullOrEmpty(processPath) && Path.GetFileName(processPath).Equals("QuickOTP.exe", StringComparison.OrdinalIgnoreCase))
		{
			return processPath;
		}
		return text;
	}
}
