using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuickOTP.Services;

public sealed class GlobalHotkeyService : IDisposable
{
	private const int WmHotkey = 786;

	private const int HotkeyId = 31233;

	private HwndSource? _source;

	private Action? _callback;

	private bool _registered;

	public bool IsRegistered => _registered;

	public void Register(Window window, string gesture, Action callback)
	{
		Unregister();
		_callback = callback;
		if (!TryParseGesture(gesture, out var mods, out var key))
		{
			throw new ArgumentException("Raccourci invalide: " + gesture);
		}
		if (new WindowInteropHelper(window).Handle == IntPtr.Zero)
		{
			window.SourceInitialized += delegate
			{
				Attach(window, mods, key);
			};
		}
		else
		{
			Attach(window, mods, key);
		}
	}

	private void Attach(Window window, uint mods, uint key)
	{
		WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
		_source = HwndSource.FromHwnd(windowInteropHelper.Handle);
		_source?.AddHook(Hook);
		_registered = RegisterHotKey(windowInteropHelper.Handle, 31233, mods, key);
		if (!_registered)
		{
			throw new InvalidOperationException("Impossible d'enregistrer le raccourci (conflit possible).");
		}
	}

	public void Unregister()
	{
		if (_source != null)
		{
			try
			{
				UnregisterHotKey(_source.Handle, 31233);
				_source.RemoveHook(Hook);
			}
			catch
			{
			}
			_source = null;
		}
		_registered = false;
	}

	private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 786 && ((IntPtr)wParam).ToInt32() == 31233)
		{
			_callback?.Invoke();
			handled = true;
		}
		return IntPtr.Zero;
	}

	public static bool TryParseGesture(string gesture, out uint modifiers, out uint virtualKey)
	{
		modifiers = 0u;
		virtualKey = 0u;
		if (string.IsNullOrWhiteSpace(gesture))
		{
			return false;
		}
		string[] array = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string text in array)
		{
			switch (text.ToLowerInvariant())
			{
			case "ctrl":
			case "control":
				modifiers |= 2u;
				continue;
			case "alt":
				modifiers |= 1u;
				continue;
			case "shift":
				modifiers |= 4u;
				continue;
			case "win":
			case "windows":
				modifiers |= 8u;
				continue;
			}
			if (text.Length == 1)
			{
				virtualKey = char.ToUpperInvariant(text[0]);
				continue;
			}
			if (text.StartsWith("F", StringComparison.OrdinalIgnoreCase))
			{
				string text2 = text;
				if (int.TryParse(text2.Substring(1, text2.Length - 1), out var result) && result >= 1 && result <= 12)
				{
					virtualKey = (uint)(112 + result - 1);
					continue;
				}
			}
			return false;
		}
		return virtualKey != 0;
	}

	public void Dispose()
	{
		Unregister();
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(nint hWnd, int id);
}
