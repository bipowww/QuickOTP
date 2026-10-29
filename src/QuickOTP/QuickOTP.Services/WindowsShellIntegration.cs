using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace QuickOTP.Services;

public static class WindowsShellIntegration
{
	public const string AppName = "QuickOTP";

	private const string AppPathsKey = "Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\QuickOTP.exe";

	private const string UninstallKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\QuickOTP";

	public static string InstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");

	public static string InstalledExecutable => Path.Combine(InstallDirectory, "QuickOTP.exe");

	public static string StartMenuShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "QuickOTP.lnk");

	public static void EnsureSearchRegistration()
	{
		try
		{
			string text = ResolveSearchTarget();
			if (!string.IsNullOrEmpty(text) && File.Exists(text))
			{
				EnsureStartMenuShortcut(text);
				EnsureAppPaths(text);
				EnsureUninstallMetadata(text);
				NotifyShellChanged();
			}
		}
		catch
		{
		}
	}

	public static string? ResolveSearchTarget()
	{
		if (File.Exists(InstalledExecutable))
		{
			return InstalledExecutable;
		}
		return StartupManager.ResolveAppExecutable();
	}

	public static void EnsureStartMenuShortcut(string targetExe)
	{
		string startMenuShortcutPath = StartMenuShortcutPath;
		Directory.CreateDirectory(Path.GetDirectoryName(startMenuShortcutPath));
		string workDir = Path.GetDirectoryName(targetExe) ?? InstallDirectory;
		CreateShortcut(startMenuShortcutPath, targetExe, workDir, "QuickOTP — codes OTP Gmail & Outlook");
	}

	private static void EnsureAppPaths(string targetExe)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\QuickOTP.exe");
		registryKey.SetValue("", targetExe);
		registryKey.SetValue("Path", Path.GetDirectoryName(targetExe) ?? "");
	}

	private static void EnsureUninstallMetadata(string targetExe)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\QuickOTP");
		registryKey.SetValue("DisplayName", "QuickOTP");
		registryKey.SetValue("DisplayIcon", targetExe);
		registryKey.SetValue("Publisher", "QuickOTP");
		registryKey.SetValue("InstallLocation", Path.GetDirectoryName(targetExe) ?? "");
		registryKey.SetValue("DisplayVersion", "1.0.0");
		registryKey.SetValue("NoModify", 1);
		registryKey.SetValue("NoRepair", 1);
	}

	private static void CreateShortcut(string linkPath, string targetExe, string workDir, string description)
	{
		dynamic val = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell unavailable"));
		dynamic val2 = val.CreateShortcut(linkPath);
		val2.TargetPath = targetExe;
		val2.WorkingDirectory = workDir;
		val2.Description = description;
		val2.IconLocation = targetExe + ",0";
		val2.Save();
		Marshal.FinalReleaseComObject(val2);
		Marshal.FinalReleaseComObject(val);
	}

	[DllImport("shell32.dll")]
	private static extern void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);

	private static void NotifyShellChanged()
	{
		SHChangeNotify(134217728u, 0u, IntPtr.Zero, IntPtr.Zero);
	}
}
