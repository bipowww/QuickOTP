using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuickOTP.Core.Models;

namespace QuickOTP;

public class MiniWindow : Window
{
	private readonly DispatcherTimer _timer;

	private readonly StackPanel _codesPanel;

	private readonly TextBlock _footer;

	public MiniWindow()
	{
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Expected O, but got Unknown
		base.Title = "QuickOTP — compact";
		base.Width = 300.0;
		base.Height = 380.0;
		base.WindowStyle = WindowStyle.ToolWindow;
		base.ResizeMode = ResizeMode.CanMinimize;
		base.ShowInTaskbar = false;
		base.Topmost = true;
		base.Background = (Brush)Application.Current.FindResource("WindowBgBrush");
		base.FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(16.0)
		};
		TextBlock element = new TextBlock
		{
			Text = "Codes prêts",
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			FontFamily = (FontFamily)Application.Current.FindResource("BrandFont"),
			Margin = new Thickness(0.0, 0.0, 0.0, 2.0)
		};
		TextBlock element2 = new TextBlock
		{
			Text = "Cliquez pour copier",
			Foreground = (Brush)Application.Current.FindResource("MutedBrush"),
			FontSize = 12.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		};
		StackPanel element3 = new StackPanel
		{
			Children = 
			{
				(UIElement)element,
				(UIElement)element2
			}
		};
		DockPanel.SetDock(element3, Dock.Top);
		dockPanel.Children.Add(element3);
		_footer = new TextBlock
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			Foreground = (Brush)Application.Current.FindResource("SuccessBrush"),
			FontSize = 12.0,
			FontWeight = FontWeights.SemiBold
		};
		DockPanel.SetDock(_footer, Dock.Bottom);
		dockPanel.Children.Add(_footer);
		_codesPanel = new StackPanel();
		ScrollViewer element4 = new ScrollViewer
		{
			Content = _codesPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		dockPanel.Children.Add(element4);
		base.Content = dockPanel;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_timer.Tick += delegate
		{
			Refresh();
		};
		base.Loaded += delegate
		{
			base.Opacity = 0.0;
			BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(280.0)));
			Refresh();
			_timer.Start();
		};
		base.Closed += delegate
		{
			_timer.Stop();
		};
	}

	public void Refresh()
	{
		_codesPanel.Children.Clear();
		App app = App.Instance;
		bool privacyMode = app.SettingsStore.Load().PrivacyMode;
		List<OtpDetectionResult> list = app.Orchestrator?.RecentValid.Take(8).ToList() ?? new List<OtpDetectionResult>();
		Brush background = (Brush)Application.Current.FindResource("SoftAccentBrush");
		Brush foreground = (Brush)Application.Current.FindResource("MutedBrush");
		Brush foreground2 = (Brush)Application.Current.FindResource("AccentDeepBrush");
		Brush background2 = (Brush)Application.Current.FindResource("CardBrush");
		Brush borderBrush = (Brush)Application.Current.FindResource("BorderBrush");
		if (list.Count == 0)
		{
			_codesPanel.Children.Add(new TextBlock
			{
				Text = "Aucun code valide pour le moment.",
				Foreground = foreground,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
				TextWrapping = TextWrapping.Wrap
			});
		}
		else
		{
			foreach (OtpDetectionResult item in list)
			{
				Border border = new Border
				{
					Padding = new Thickness(12.0, 10.0, 12.0, 10.0),
					Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
					Background = background2,
					BorderBrush = borderBrush,
					BorderThickness = new Thickness(1.0),
					CornerRadius = new CornerRadius(10.0)
				};
				StackPanel stackPanel = new StackPanel();
				stackPanel.Children.Add(new TextBlock
				{
					Text = (item.Service ?? "OTP"),
					FontWeight = FontWeights.SemiBold,
					FontSize = 12.0
				});
				stackPanel.Children.Add(new TextBlock
				{
					Text = (privacyMode ? new string('·', Math.Clamp(item.Code.Length, 4, 12)) : FormatCode(item.Code)),
					FontSize = 22.0,
					FontFamily = (FontFamily)Application.Current.FindResource("MonoFont"),
					Foreground = foreground2,
					Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
				});
				stackPanel.Children.Add(new TextBlock
				{
					Text = FormatAgo(item.ReceivedAt),
					Foreground = foreground,
					FontSize = 11.0,
					Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
				});
				Button copyBtn = new Button
				{
					Content = "Copier",
					Padding = new Thickness(12.0, 5.0, 12.0, 5.0),
					Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
					HorizontalAlignment = HorizontalAlignment.Left,
					Background = background,
					Foreground = foreground2,
					Tag = item.Code
				};
				copyBtn.Click += async delegate
				{
					if (copyBtn.Tag is string code)
					{
						await app.Clipboard.CopyLastAsync(code);
					}
				};
				stackPanel.Children.Add(copyBtn);
				border.Child = stackPanel;
				_codesPanel.Children.Add(border);
			}
		}
		AppStatus appStatus = app.Orchestrator?.Status ?? AppStatus.Idle;
		_footer.Text = appStatus switch
		{
			AppStatus.NotConnected => "Sans compte", 
			AppStatus.Paused => "En pause", 
			AppStatus.Monitoring => "Surveillance active", 
			_ => $"État · {appStatus}", 
		};
	}

	private static string FormatCode(string code)
	{
		if (string.IsNullOrEmpty(code) || !code.All(char.IsDigit))
		{
			return code;
		}
		switch (code.Length)
		{
		case 6:
		{
			string text4 = code.Substring(0, 3);
			string text2 = code;
			return text4 + " " + text2.Substring(3, text2.Length - 3);
		}
		case 8:
		{
			string text3 = code.Substring(0, 4);
			string text2 = code;
			return text3 + " " + text2.Substring(4, text2.Length - 4);
		}
		case 9:
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 3);
			defaultInterpolatedStringHandler.AppendFormatted(code.Substring(0, 3));
			defaultInterpolatedStringHandler.AppendLiteral(" ");
			defaultInterpolatedStringHandler.AppendFormatted(code.Substring(3, 3));
			defaultInterpolatedStringHandler.AppendLiteral(" ");
			string text2 = code;
			defaultInterpolatedStringHandler.AppendFormatted(text2.Substring(6, text2.Length - 6));
			return defaultInterpolatedStringHandler.ToStringAndClear();
		}
		case 10:
		{
			string text = code.Substring(0, 5);
			string text2 = code;
			return text + " " + text2.Substring(5, text2.Length - 5);
		}
		default:
			return code;
		}
	}

	private static string FormatAgo(DateTimeOffset when)
	{
		TimeSpan timeSpan = DateTimeOffset.Now - when;
		if (timeSpan.TotalSeconds < 60.0)
		{
			return $"il y a {(int)timeSpan.TotalSeconds} s";
		}
		if (timeSpan.TotalMinutes < 60.0)
		{
			return $"il y a {(int)timeSpan.TotalMinutes} min";
		}
		return when.ToLocalTime().ToString("t");
	}
}
