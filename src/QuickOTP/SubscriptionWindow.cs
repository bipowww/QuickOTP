using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickOTP;

public class SubscriptionWindow : Window
{
	public SubscriptionWindow()
	{
		base.Title = "QuickOTP · Abonnement";
		base.Width = 440.0;
		base.Height = 500.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.ResizeMode = ResizeMode.NoResize;
		base.Background = (Brush)Application.Current.FindResource("WindowBgBrush");
		base.FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(28.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "QuickOTP",
			FontSize = 28.0,
			FontWeight = FontWeights.SemiBold,
			FontFamily = (FontFamily)Application.Current.FindResource("BrandFont")
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Les codes d’e-mail, copiés pour vous.",
			Foreground = (Brush)Application.Current.FindResource("MutedBrush"),
			Margin = new Thickness(0.0, 6.0, 0.0, 24.0)
		});
		stackPanel.Children.Add(PlanCard("ANNUEL", "14,99 € / an", "≈ 1,25 € / mois · −58 %", "quickotp_annual"));
		stackPanel.Children.Add(PlanCard("MENSUEL", "2,99 € / mois", "Mêmes fonctionnalités", "quickotp_monthly"));
		Button button = new Button
		{
			Content = "Restaurer l’abonnement",
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0),
			Padding = new Thickness(14.0, 8.0, 14.0, 8.0),
			Background = (Brush)Application.Current.FindResource("SoftAccentBrush"),
			Foreground = (Brush)Application.Current.FindResource("AccentDeepBrush")
		};
		button.Click += async delegate
		{
			await App.Instance.Subscription.RestoreAsync();
			MessageBox.Show("État d'abonnement actualisé (provider de développement).", "QuickOTP");
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "Paiement réel non connecté — architecture prête pour Stripe.",
			Foreground = (Brush)Application.Current.FindResource("MutedBrush"),
			FontSize = 11.0,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
		});
		base.Content = stackPanel;
	}

	private UIElement PlanCard(string title, string price, string sub, string planId)
	{
		Border obj = new Border
		{
			Background = (Brush)Application.Current.FindResource("CardBrush"),
			BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(18.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			CornerRadius = new CornerRadius(12.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = title,
					FontWeight = FontWeights.SemiBold,
					FontSize = 12.0,
					Foreground = (Brush)Application.Current.FindResource("AccentDeepBrush")
				},
				(UIElement)new TextBlock
				{
					Text = price,
					FontSize = 22.0,
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
				},
				(UIElement)new TextBlock
				{
					Text = sub,
					Foreground = (Brush)Application.Current.FindResource("MutedBrush"),
					Margin = new Thickness(0.0, 2.0, 0.0, 12.0)
				}
			}
		};
		Button button = new Button
		{
			Content = ((title == "ANNUEL") ? "Choisir l’annuel" : "Choisir le mensuel"),
			Padding = new Thickness(14.0, 8.0, 14.0, 8.0),
			HorizontalAlignment = HorizontalAlignment.Left
		};
		button.Click += async delegate
		{
			await App.Instance.Subscription.StartCheckoutAsync(planId);
			MessageBox.Show("Abonnement simulé activé (DevSubscriptionService).", "QuickOTP");
			Close();
		};
		stackPanel.Children.Add(button);
		obj.Child = stackPanel;
		return obj;
	}
}
