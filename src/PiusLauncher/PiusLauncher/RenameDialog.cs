using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PiusLauncher;

public sealed class RenameDialog : Window
{
	private readonly TextBox _nameTextBox = new TextBox();

	public string ResultName { get; private set; } = string.Empty;

	public RenameDialog(string prompt, string currentName)
	{
		base.Title = AppText.Choose("이름 변경", "Rename");
		base.Width = 420.0;
		base.Height = 220.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		Border obj = new Border
		{
			Margin = new Thickness(14.0),
			Padding = new Thickness(22.0),
			Background = new SolidColorBrush(Color.FromArgb(242, 14, 16, 24)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
			BorderThickness = new Thickness(1.5),
			CornerRadius = new CornerRadius(16.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 34.0,
				Color = Color.FromRgb(37, 99, 235),
				Opacity = 0.54,
				ShadowDepth = 0.0
			}
		};
		object obj2 = obj;
		base.Content = obj;
		UIElement obj3 = (((Border)obj2).Child = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = new GridLength(1.0, GridUnitType.Star)
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				}
			}
		});
		((Panel)obj3).Children.Add(new TextBlock
		{
			Text = prompt,
			Foreground = new SolidColorBrush(Color.FromRgb(245, 243, byte.MaxValue)),
			FontFamily = new FontFamily("Segoe UI"),
			FontSize = 18.0,
			FontWeight = FontWeights.Bold
		});
		_nameTextBox.Text = currentName;
		_nameTextBox.Height = 48.0;
		_nameTextBox.Margin = new Thickness(0.0, 24.0, 0.0, 0.0);
		_nameTextBox.Padding = new Thickness(14.0, 0.0, 14.0, 0.0);
		_nameTextBox.VerticalContentAlignment = VerticalAlignment.Center;
		_nameTextBox.Background = new SolidColorBrush(Color.FromArgb(230, 26, 27, 38));
		_nameTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254));
		_nameTextBox.BorderThickness = new Thickness(2.0);
		_nameTextBox.CaretBrush = Brushes.White;
		_nameTextBox.Foreground = Brushes.White;
		_nameTextBox.FontFamily = new FontFamily("Segoe UI");
		_nameTextBox.FontSize = 18.0;
		_nameTextBox.FontWeight = FontWeights.SemiBold;
		_nameTextBox.MaxLength = 28;
		_nameTextBox.KeyDown += NameTextBox_KeyDown;
		Grid.SetRow(_nameTextBox, 1);
		((Panel)obj3).Children.Add(_nameTextBox);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 20.0, 0.0, 0.0)
		};
		Grid.SetRow(stackPanel, 2);
		((Panel)obj3).Children.Add(stackPanel);
		Button button = new Button
		{
			Width = 86.0,
			Height = 38.0,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			Content = AppText.Choose("취소", "Cancel")
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		stackPanel.Children.Add(button);
		Button button2 = new Button
		{
			Width = 104.0,
			Height = 38.0,
			Content = AppText.Choose("저장", "Save")
		};
		button2.Click += delegate
		{
			Save();
		};
		stackPanel.Children.Add(button2);
		base.Loaded += delegate
		{
			_nameTextBox.SelectAll();
			_nameTextBox.Focus();
		};
	}

	private void Save()
	{
		string text = _nameTextBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			MessageBox.Show(this, AppText.Choose("이름을 입력해 주세요.", "Please enter a name."), AppText.Choose("이름 변경", "Rename"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		ResultName = text;
		base.DialogResult = true;
	}

	private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			Save();
			e.Handled = true;
		}
		else if ((int)e.Key == 13)
		{
			base.DialogResult = false;
			e.Handled = true;
		}
	}
}
