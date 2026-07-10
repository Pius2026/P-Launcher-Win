using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using DragDropEffects = System.Windows.DragDropEffects;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;

namespace PiusLauncher;

public class MainWindow : Window, INotifyPropertyChanged, IComponentConnector
{
	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private sealed class WindowHandle : System.Windows.Forms.IWin32Window
	{
		public nint Handle { get; }

		public WindowHandle(Window window)
		{
			Handle = new WindowInteropHelper(window).Handle;
		}
	}

	private sealed class PidlHandle : IDisposable
	{
		public nint Handle { get; private set; }

		private PidlHandle(nint handle)
		{
			Handle = handle;
		}

		public static PidlHandle FromCida(byte[] source, int offset)
		{
			if (offset < 0 || offset >= source.Length)
			{
				return new PidlHandle(IntPtr.Zero);
			}
			int pidlLength = GetPidlLength(source, offset);
			nint num = Marshal.AllocHGlobal(pidlLength);
			Marshal.Copy(source, offset, num, pidlLength);
			return new PidlHandle(num);
		}

		private static int GetPidlLength(byte[] source, int offset)
		{
			ushort num;
			for (int i = offset; i + 2 <= source.Length; i += num)
			{
				num = BitConverter.ToUInt16(source, i);
				if (num == 0)
				{
					return Math.Max(2, i - offset + 2);
				}
			}
			return 2;
		}

		public void Dispose()
		{
			if (Handle != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(Handle);
				Handle = IntPtr.Zero;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct ShellFileInfo
	{
		public nint hIcon;

		public int iIcon;

		public uint dwAttributes;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}

	private const int WidgetCount = 3;

	private const int SlotsPerWidget = 10;

	private const double WindowCompactWidth = 720.0;

	private const double WindowCompactHeight = 520.0;

	private const double SlotWidth = 100.0;

	private const double SlotHeight = 122.0;

	private const double SlotOrbSize = 50.0;

	private const double SlotOrbIconSize = 42.0;

	private const double SlotOrbGlassCapsuleWidth = 28.0;

	private const double SlotOrbGlassCapsuleHeight = 12.0;

	private const double WidgetTabWidth = 154.0;

	private const double RenameButtonWidth = 77.0;

	private const double RenameButtonHeight = 25.0;

	private const double HeaderActionButtonWidth = 116.0;

	private const double HeaderActionButtonHeight = 38.0;

	private const double SettingsButtonWidth = 70.0;

	private const double SettingsButtonHeight = 58.0;

	private static readonly Thickness SettingsButtonMargin = new Thickness(64.5, 3.0, 0.0, 0.0);

	private const double SettingsDialogWidth = 360.0;

	private const double SettingsDialogHeight = 470.0;

	private const double SlotContextMenuMinWidth = 190.0;

	private const double SlotContextMenuOuterPadding = 5.0;

	private const int SlotContextMenuBubblePopMilliseconds = 180;

	private const int SlotContextMenuBubblePopPollMilliseconds = 70;

	private const double SlotContextMenuBubblePopScale = 0.18;

	private const string SlotContextMenuBubblePopClosingTag = "bubble-pop-closing";

	private const double SlotMenuHeaderWidth = 175.0;

	private const double SlotMenuActionMinHeight = 24.0;

	private const double SlotMenuActionFontSize = 10.5;

	private const double SlotMenuTargetTitleFontSize = 10.5;

	private const double SlotMenuHeaderTitleFontSize = 12.0;

	private const double SlotMenuHeaderSubtitleFontSize = 9.5;

	private const double SlotMenuBadgeFontSize = 12.0;

	private const double SlotMenuItemsHeaderFontSize = 10.0;

	private const double SlotMenuTargetRowWidth = 170.0;

	private const double SlotMenuTargetRowMinHeight = 34.0;

	private const double SlotMenuTargetGridWidth = 156.0;

	private const double SlotMenuTargetPathFontSize = 9.0;

	private const double SlotMenuDeleteButtonSize = 18.0;

	private const double SlotMenuSeparatorVerticalMargin = 4.0;

	private const int SlotReorderAnimationMilliseconds = 170;

	private const double SlotDragLiftScale = 1.06;

	private const int SlotDragLiftZIndex = 50;

	private readonly List<ObservableCollection<LauncherItem>> _widgets = new List<ObservableCollection<LauncherItem>>();

	private readonly List<System.Windows.Controls.Button> _tabButtons = new List<System.Windows.Controls.Button>();

	private readonly List<ToolStripMenuItem> _trayWidgetItems = new List<ToolStripMenuItem>();

	private readonly HashSet<LauncherTarget> _targetsBeingRemoved = new HashSet<LauncherTarget>();

	private readonly UniformGrid _slotGrid = new UniformGrid
	{
		Columns = 5,
		Rows = 2
	};

	private readonly TextBlock _statusText = new TextBlock();

	private readonly LauncherStore _store = new LauncherStore();

	private readonly UiPreferencesStore _preferencesStore = new UiPreferencesStore();

	private readonly Random _random = new Random();

	private readonly DispatcherTimer _hoverHideTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(100.0)
	};

	private DateTime _lastTrayHoverShowUtc = DateTime.MinValue;

	private DateTime _lastPointerSafeUtc = DateTime.UtcNow;

	private DateTime _lastTrayPointerUtc = DateTime.MinValue;

	private Border _shell;

	private Canvas _bubbleCanvas;

	private ScaleTransform _shellScale;

	private TranslateTransform _shellTranslate;

	private OutlinedTextBlock _titleText;

	private System.Windows.Shapes.Path _trayConnector;

	private ScaleTransform _trayConnectorScale;

	private TranslateTransform _trayConnectorTranslate;

	private NotifyIcon? _notifyIcon;

	private System.Windows.Controls.Button _editModeButton;

	private System.Windows.Controls.Button _renameLauncherButton;

	private TextBlock _editHintText;

	private TextBlock _totalSlotsText;

	private System.Windows.Controls.Button _exitButton;

	private System.Windows.Controls.Button _settingsButton;

	private ToolStripMenuItem _trayOpenItem;

	private ToolStripMenuItem _trayExitItem;

	private string _launcherTitle = LauncherStore.DefaultLauncherTitle;

	private int _selectedWidgetIndex;

	private int _bubbleAnimationGeneration;

	private int _hideAnimationGeneration;

	private bool _isHideAnimationRunning;

	private bool _isExitRequested;

	private bool _wasMinimized;

	private bool _isEditMode;

	private bool _isTrayMenuOpen;

	private bool _isSettingsInteractionActive;

	private Point _slotDragStartPoint;

	private LauncherItem? _slotDragSourceItem;

	private System.Windows.Controls.Button? _slotDragSourceButton;

	private bool _isSlotDragActive;

	private bool _suppressNextSlotClick;

	private int _slotDragPreviewIndex = -1;

	internal Grid RootScene;

	private bool _contentLoaded;

	public ObservableCollection<WidgetTab> WidgetTabs { get; } = new ObservableCollection<WidgetTab>();

	private IEnumerable<LauncherItem> AllItems => _widgets.SelectMany((ObservableCollection<LauncherItem> widget) => widget);

	private string LauncherTitle
	{
		get
		{
			return _launcherTitle;
		}
		set
		{
			string text = LauncherStore.NormalizeLauncherTitle(value);
			if (SetField(ref _launcherTitle, text, "LauncherTitle"))
			{
				base.Title = text;
				if (_titleText != null)
				{
					_titleText.Text = text;
				}
				UpdateTrayLabels();
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public MainWindow()
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		InitializeComponent();
		base.Width = 720.0;
		base.Height = 520.0;
		base.MinWidth = 720.0;
		base.MinHeight = 520.0;
		AppText.CurrentLanguage = _preferencesStore.LoadLanguage();
		BuildLauncherModel();
		LauncherDisplaySettings launcherDisplaySettings = _store.LoadInto(((IEnumerable<ObservableCollection<LauncherItem>>)_widgets).Select((Func<ObservableCollection<LauncherItem>, IReadOnlyList<LauncherItem>>)((ObservableCollection<LauncherItem> widget) => widget)).ToList());
		ApplyDisplaySettings(launcherDisplaySettings);
		BuildInterface();
		SelectWidget(launcherDisplaySettings.SelectedWidgetIndex);
		UpdateWidgetCounts();
		NormalizeExistingShortcutItems();
		CreateTrayIcon();
		InstallStartupShortcut();
		_hoverHideTimer.Tick += HoverHideTimer_Tick;
		base.Loaded += delegate
		{
			PositionNearTaskbar();
			if (string.Equals(Environment.GetEnvironmentVariable("P_EDU_LAUNCHER_QA_SHOW"), "1", StringComparison.Ordinal))
			{
				BeginBubbleShowAnimation();
				_hoverHideTimer.Stop();
				base.Dispatcher.BeginInvoke(new Action(CompleteQaShowAnimation), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
				return;
			}
			Hide();
			_hoverHideTimer.Start();
		};
		base.MouseEnter += delegate
		{
			_hoverHideTimer.Start();
		};
		base.MouseLeave += delegate
		{
			_hoverHideTimer.Start();
		};
		base.Closing += Window_Closing;
		base.StateChanged += Window_StateChanged;
		base.KeyDown += delegate(object _, System.Windows.Input.KeyEventArgs e)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Invalid comparison between Unknown and I4
			if ((int)e.Key == 13)
			{
				BeginHideToTaskbarAnimation();
			}
		};
	}

	private void BuildLauncherModel()
	{
		for (int i = 0; i < 3; i++)
		{
			ObservableCollection<LauncherItem> observableCollection = new ObservableCollection<LauncherItem>();
			for (int j = 0; j < 10; j++)
			{
				observableCollection.Add(new LauncherItem(i, j));
			}
			_widgets.Add(observableCollection);
			WidgetTabs.Add(new WidgetTab(i));
		}
	}

	private void ApplyDisplaySettings(LauncherDisplaySettings settings)
	{
		LauncherTitle = settings.LauncherTitle;
		for (int i = 0; i < WidgetTabs.Count; i++)
		{
			if (i < settings.WidgetNames.Count && !string.IsNullOrWhiteSpace(settings.WidgetNames[i]))
			{
				string savedName = settings.WidgetNames[i];
				WidgetTabs[i].Name = AppText.IsDefaultWidgetName(savedName, i) ? AppText.DefaultWidgetName(i) : savedName;
			}
			else
			{
				WidgetTabs[i].Name = string.Empty;
			}
		}
	}

	private bool ChangeLanguage(UiLanguage language)
	{
		UiLanguage normalizedLanguage = AppText.Normalize(language);
		if (AppText.CurrentLanguage == normalizedLanguage)
		{
			return false;
		}
		bool[] defaultWidgetNames = WidgetTabs.Select(tab => AppText.IsDefaultWidgetName(tab.Name, tab.Index)).ToArray();
		AppText.CurrentLanguage = normalizedLanguage;
		for (int i = 0; i < WidgetTabs.Count; i++)
		{
			if (defaultWidgetNames[i])
			{
				WidgetTabs[i].Name = string.Empty;
			}
		}
		_preferencesStore.SaveLanguage(normalizedLanguage);
		RefreshLocalizedInterface();
		PersistLauncherState();
		return true;
	}

	private void RefreshLocalizedInterface()
	{
		if (_renameLauncherButton != null)
		{
			_renameLauncherButton.Content = AppText.Choose("이름 바꾸기", "Rename");
		}
		if (_editHintText != null)
		{
			_editHintText.Text = AppText.Choose("[[앱 수정]버튼을 누르면, 파일, 폴더를 끌어다 놓을 수 있어요!]", "Click [Edit Apps] to drag and drop files or folders.");
		}
		if (_totalSlotsText != null)
		{
			_totalSlotsText.Text = AppText.Choose(" / 총 30개 슬롯", " / 30 slots total");
		}
		if (_exitButton != null)
		{
			_exitButton.Content = AppText.Choose("종료", "Exit");
		}
		if (_settingsButton != null)
		{
			_settingsButton.Content = CreateSettingsGearContent();
			System.Windows.Automation.AutomationProperties.SetName(_settingsButton, AppText.Choose("설정", "Settings"));
		}
		UpdateEditModeButton();
		RefreshTabs();
		RefreshSlots();
		RefreshStatus();
		UpdateTrayLabels();
	}

	private void BuildInterface()
	{
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		RootScene.Children.Clear();
		Border border = CreateGlowBorder();
		border.Height = double.NaN;
		border.VerticalAlignment = VerticalAlignment.Stretch;
		RootScene.Children.Add(border);
		_shellScale = new ScaleTransform(0.92, 0.92);
		_shellTranslate = new TranslateTransform(0.0, 34.0);
		_shell = new Border
		{
			Margin = new Thickness(12.0),
			Height = double.NaN,
			VerticalAlignment = VerticalAlignment.Stretch,
			CornerRadius = new CornerRadius(24.0),
			Background = CreateShellBrush(),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 165, 180, 252)),
			BorderThickness = new Thickness(1.0),
			Opacity = 0.0,
			RenderTransformOrigin = new Point(0.5, 1.0),
			RenderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)_shellScale,
					(Transform)_shellTranslate
				}
			},
			Effect = new DropShadowEffect
			{
				BlurRadius = 32.0,
				Color = System.Windows.Media.Color.FromRgb(2, 6, 23),
				Opacity = 0.68,
				ShadowDepth = 0.0
			}
		};
		_shell.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			TryDragWindow(e);
		};
		System.Windows.Controls.Panel.SetZIndex(_shell, 2);
		RootScene.Children.Add(_shell);
		Grid grid = new Grid
		{
			ClipToBounds = true
		};
		_shell.Child = grid;
		grid.Children.Add(CreateRibbon(-8.0, 168.0, new Thickness(-70.0, 10.0, -60.0, 0.0), 0.58));
		grid.Children.Add(CreateRibbon(10.0, 78.0, new Thickness(-130.0, 320.0, -110.0, 0.0), 0.22));
		grid.Children.Add(CreateMainLayout());
		_bubbleCanvas = new Canvas
		{
			Margin = new Thickness(12.0),
			IsHitTestVisible = false,
			ClipToBounds = false
		};
		System.Windows.Controls.Panel.SetZIndex(_bubbleCanvas, 4);
		RootScene.Children.Add(_bubbleCanvas);
	}

	private System.Windows.Shapes.Path CreateTrayConnector()
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		_trayConnectorScale = new ScaleTransform(1.0, 1.0);
		_trayConnectorTranslate = new TranslateTransform(0.0, 0.0);
		TransformGroup transformGroup = new TransformGroup();
		transformGroup.Children.Add(_trayConnectorScale);
		transformGroup.Children.Add(_trayConnectorTranslate);
		LinearGradientBrush fill = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops = 
			{
				new GradientStop(System.Windows.Media.Color.FromArgb(248, 8, 10, 16), 0.0),
				new GradientStop(System.Windows.Media.Color.FromArgb(244, 17, 24, 39), 0.46),
				new GradientStop(System.Windows.Media.Color.FromArgb(238, 22, 43, 68), 1.0)
			}
		};
		return new System.Windows.Shapes.Path
		{
			Width = 720.0,
			Height = 600.0,
			Stretch = Stretch.None,
			Data = Geometry.Parse("M 556 507 C 590 528 632 558 708 594 C 650 579 606 553 582 529 C 573 520 563 515 550 513 C 553 511 555 509 556 507 Z"),
			Fill = fill,
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 191, 219, 254)),
			StrokeThickness = 1.8,
			Opacity = 0.98,
			IsHitTestVisible = false,
			RenderTransformOrigin = new Point(0.96, 1.0),
			RenderTransform = transformGroup,
			Effect = new DropShadowEffect
			{
				BlurRadius = 18.0,
				Color = System.Windows.Media.Color.FromRgb(96, 165, 250),
				Opacity = 0.48,
				ShadowDepth = 0.0
			}
		};
	}

	private Border CreateGlowBorder()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		RotateTransform rotateTransform = new RotateTransform(0.0, 0.5, 0.5);
		LinearGradientBrush borderBrush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			RelativeTransform = rotateTransform,
			GradientStops = 
			{
				new GradientStop(Colors.White, 0.0),
				new GradientStop(System.Windows.Media.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.28),
				new GradientStop(System.Windows.Media.Color.FromRgb(96, 165, 250), 0.52),
				new GradientStop(System.Windows.Media.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.78),
				new GradientStop(Colors.White, 1.0)
			}
		};
		DoubleAnimation animation = new DoubleAnimation(0.0, 360.0, TimeSpan.FromSeconds(4L))
		{
			RepeatBehavior = RepeatBehavior.Forever
		};
		rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
		return new Border
		{
			Margin = new Thickness(8.0),
			CornerRadius = new CornerRadius(26.0),
			BorderThickness = new Thickness(3.0),
			BorderBrush = borderBrush,
			IsHitTestVisible = false,
			Opacity = 0.95,
			Effect = new BlurEffect
			{
				Radius = 4.0
			}
		};
	}

	private Grid CreateMainLayout()
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(18.0, 14.0, 18.0, 12.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(130.0)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(38.0)
		});
		Grid grid2 = new Grid();
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid2.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			TryDragWindow(e);
		};
		Grid.SetRow(grid2, 0);
		grid.Children.Add(grid2);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Vertical
		};
		Grid.SetColumn(stackPanel, 0);
		Grid.SetColumnSpan(stackPanel, 2);
		grid2.Children.Add(stackPanel);
		Grid grid3 = new Grid();
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		stackPanel.Children.Add(grid3);
		_titleText = new OutlinedTextBlock
		{
			Width = 328.0,
			Height = 46.0,
			Text = LauncherTitle,
			Fill = CreateTitleBrush(),
			Stroke = System.Windows.Media.Brushes.White,
			GlowStroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			StrokeThickness = 1.5,
			GlowThickness = 3.8,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
			FontSize = 33.0,
			FontWeight = FontWeights.Black
		};
		_titleText.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (e.ClickCount >= 2)
			{
				RenameLauncherTitle();
				e.Handled = true;
			}
			else
			{
				TryDragWindow(e);
			}
		};
		Grid.SetColumn(_titleText, 0);
		grid3.Children.Add(_titleText);
		System.Windows.Controls.Button button = CreateHeaderActionButton(AppText.Choose("이름 바꾸기", "Rename"), RenameButtonWidth, RenameButtonHeight);
		_renameLauncherButton = button;
		button.Margin = new Thickness(18.0, 10.5, 12.0, 0.0);
		button.Click += delegate
		{
			RenameLauncherTitle();
		};
		Grid.SetColumn(button, 1);
		grid3.Children.Add(button);
		_editModeButton = CreateEditModeButton();
		_editModeButton.Click += delegate
		{
			ToggleEditMode();
		};
		Grid.SetColumn(_editModeButton, 2);
		grid3.Children.Add(_editModeButton);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(stackPanel2);
		for (int num = 0; num < WidgetTabs.Count; num++)
		{
			System.Windows.Controls.Button button2 = CreateTabButton(WidgetTabs[num]);
			_tabButtons.Add(button2);
			stackPanel2.Children.Add(button2);
		}
		System.Windows.Controls.Button settingsButton = CreateSettingsButton();
		_settingsButton = settingsButton;
		stackPanel2.Children.Add(settingsButton);
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Top
		};
		Grid.SetColumn(stackPanel3, 1);
		grid2.Children.Add(stackPanel3);
		System.Windows.Controls.Button button3 = CreateChromeButton("_", 36.0, 32.0);
		button3.Click += delegate
		{
			base.WindowState = WindowState.Minimized;
		};
		stackPanel3.Children.Add(button3);
		System.Windows.Controls.Button button4 = CreateChromeButton("×", 36.0, 32.0);
		button4.Click += delegate
		{
			BeginHideToTaskbarAnimation();
		};
		stackPanel3.Children.Add(button4);
		_slotGrid.Margin = new Thickness(0.0, 6.0, 0.0, 4.0);
		Grid.SetRow(_slotGrid, 1);
		grid.Children.Add(_slotGrid);
		Grid grid4 = new Grid();
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid4.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Grid.SetRow(grid4, 2);
		grid.Children.Add(grid4);
		TextBlock element = new TextBlock
		{
			Text = AppText.Choose("[[앱 수정]버튼을 누르면, 파일, 폴더를 끌어다 놓을 수 있어요!]", "Click [Edit Apps] to drag and drop files or folders."),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(218, 224, 242, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
					FontSize = SlotMenuItemsHeaderFontSize,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			IsHitTestVisible = false
		};
		_editHintText = element;
		Grid.SetColumn(element, 1);
		System.Windows.Controls.Panel.SetZIndex(element, 0);
		grid4.Children.Add(element);
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		};
		System.Windows.Controls.Panel.SetZIndex(stackPanel4, 1);
		_statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
		_statusText.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
		_statusText.FontSize = 12.0;
		_statusText.FontWeight = FontWeights.SemiBold;
		_statusText.MaxWidth = 120.0;
		_statusText.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel4.Children.Add(_statusText);
		_totalSlotsText = new TextBlock
		{
			Text = AppText.Choose(" / 총 30개 슬롯", " / 30 slots total"),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(175, 199, 210, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 12.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel4.Children.Add(_totalSlotsText);
		grid4.Children.Add(stackPanel4);
		StackPanel stackPanel5 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		System.Windows.Controls.Panel.SetZIndex(stackPanel5, 1);
		Grid.SetColumn(stackPanel5, 2);
		grid4.Children.Add(stackPanel5);
		System.Windows.Controls.Button button5 = CreateChromeButton(AppText.Choose("종료", "Exit"), 64.0, 34.0);
		_exitButton = button5;
		button5.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		button5.Padding = new Thickness(2.0, 0.0, 2.0, 0.0);
		button5.Click += delegate
		{
			RequestExit();
		};
		stackPanel5.Children.Add(button5);
		return grid;
	}

	private System.Windows.Controls.Button CreateTabButton(WidgetTab tab)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button();
		button.Width = WidgetTabWidth;
		button.Height = 64.0;
		button.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		button.Padding = new Thickness(8.0, 0.0, 8.0, 0.0);
		button.BorderThickness = new Thickness(2.0);
		button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 191, 219, 254));
		button.Foreground = System.Windows.Media.Brushes.White;
		button.Cursor = System.Windows.Input.Cursors.Hand;
		button.Background = CreateTabBrush(selected: false);
		button.Click += delegate
		{
			SelectWidget(tab.Index);
			BeginBubbleShowAnimation();
		};
		button.MouseEnter += delegate
		{
			// Hovering a tab switches to that widget automatically, no click required. Guarded so
			// re-entering the already-active tab does not redo the selection/persist. No bubble
			// animation here: the launcher is already on screen while the pointer is over its tab.
			if (_selectedWidgetIndex != tab.Index)
			{
				SelectWidget(tab.Index);
			}
		};
		button.MouseDoubleClick += delegate(object _, MouseButtonEventArgs e)
		{
			RenameWidget(tab);
			e.Handled = true;
		};
		ContextMenu contextMenu = new ContextMenu();
		MenuItem menuItem = new MenuItem
		{
			Header = AppText.Choose("이름 바꾸기...", "Rename...")
		};
		menuItem.Click += delegate
		{
			RenameWidget(tab);
		};
		contextMenu.Items.Add(menuItem);
		button.ContextMenu = contextMenu;
		button.Content = CreateTabContent(tab);
		button.Tag = tab;
		System.Windows.Automation.AutomationProperties.SetAutomationId(button, "widget-tab-" + tab.Index);
		return button;
	}

	private UIElement CreateTabContent(WidgetTab tab)
	{
		Grid obj = new Grid
		{
			Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = tab.Name,
			Foreground = System.Windows.Media.Brushes.White,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 18.0,
			FontWeight = FontWeights.Bold,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = AppText.Choose("10개 슬롯 모음", "10 slots"),
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 242, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 12.0,
			FontWeight = FontWeights.SemiBold
		});
		obj.Children.Add(stackPanel);
		Border element = new Border
		{
			MinWidth = 44.0,
			Height = 30.0,
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 224, 242, 254)),
			BorderThickness = new Thickness(2.0),
			CornerRadius = new CornerRadius(8.0),
			Child = new TextBlock
			{
				Text = tab.CountText,
				Foreground = System.Windows.Media.Brushes.White,
				FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = 15.0,
				FontWeight = FontWeights.Bold,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		Grid.SetColumn(element, 1);
		obj.Children.Add(element);
		return obj;
	}

	private System.Windows.Controls.Button CreateSlotButton(LauncherItem item)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button();
		button.Width = 100.0;
		button.Height = 122.0;
		button.Margin = new Thickness(4.0);
		button.Padding = new Thickness(0.0);
		button.BorderThickness = new Thickness(1.0);
		button.BorderBrush = item.IsGroup ? CreateGroupSlotBorderBrush() : new SolidColorBrush(System.Windows.Media.Color.FromArgb(86, 124, 142, 168));
		button.Background = item.IsGroup ? CreateGroupSlotBrush() : CreateSlotBrush();
		button.Cursor = System.Windows.Input.Cursors.Hand;
		button.ToolTip = item.ToolTip;
		button.Tag = item;
		button.AllowDrop = _isEditMode;
		button.Click += SlotButton_Click;
		button.PreviewDragOver += SlotButton_PreviewDragOver;
		button.Drop += SlotButton_Drop;
		if (_isEditMode)
		{
			button.PreviewMouseLeftButtonDown += SlotButton_PreviewMouseLeftButtonDown;
			button.PreviewMouseMove += SlotButton_PreviewMouseMove;
			button.PreviewMouseLeftButtonUp += SlotButton_PreviewMouseLeftButtonUp;
			button.ContextMenu = CreateSlotContextMenu(item);
		}
		button.Content = CreateSlotContent(item);
		return button;
	}

	private ContextMenu CreateSlotContextMenu(LauncherItem item)
	{
		ContextMenu contextMenu = new ContextMenu
		{
				MinWidth = SlotContextMenuMinWidth,
				Padding = new Thickness(SlotContextMenuOuterPadding),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderBrush = CreateGroupSlotBorderBrush(),
			BorderThickness = new Thickness(0.0),
			HasDropShadow = true,
			Effect = new DropShadowEffect
			{
					BlurRadius = 22.0,
					Color = System.Windows.Media.Color.FromRgb(8, 13, 31),
					Opacity = 0.64,
					ShadowDepth = 6.0
			}
		};
		contextMenu.Template = CreateSlotContextMenuTemplate();
		contextMenu.Resources.Add(typeof(MenuItem), CreateSlotMenuItemStyle());
		ConfigureSlotContextMenuBubblePop(contextMenu);
		RebuildSlotContextMenuItems(contextMenu, item);
		return contextMenu;
	}

	private void ConfigureSlotContextMenuBubblePop(ContextMenu contextMenu)
	{
		bool pointerWasInside = false;
		DispatcherTimer pollTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(SlotContextMenuBubblePopPollMilliseconds)
		};
		pollTimer.Tick += delegate
		{
			if (!contextMenu.IsOpen || string.Equals(contextMenu.Tag as string, SlotContextMenuBubblePopClosingTag, StringComparison.Ordinal))
			{
				return;
			}
			if (IsPointerInsideSlotContextMenu(contextMenu))
			{
				pointerWasInside = true;
				return;
			}
			if (pointerWasInside)
			{
				BeginSlotContextMenuBubblePop(contextMenu);
			}
		};
		contextMenu.RenderTransformOrigin = new Point(0.5, 0.08);
		contextMenu.RenderTransform = new ScaleTransform(1.0, 1.0);
		contextMenu.Opened += delegate
		{
			ResetSlotContextMenuBubblePop(contextMenu);
			pointerWasInside = IsPointerInsideSlotContextMenu(contextMenu);
			pollTimer.Start();
		};
		contextMenu.Closed += delegate
		{
			pollTimer.Stop();
			pointerWasInside = false;
			ResetSlotContextMenuBubblePop(contextMenu);
		};
		contextMenu.MouseEnter += delegate
		{
			pointerWasInside = true;
		};
		contextMenu.MouseLeave += delegate
		{
			if (pointerWasInside)
			{
				BeginSlotContextMenuBubblePop(contextMenu);
			}
		};
	}

	private static bool IsPointerInsideSlotContextMenu(ContextMenu contextMenu)
	{
		if (!contextMenu.IsOpen || contextMenu.ActualWidth <= 0.0 || contextMenu.ActualHeight <= 0.0)
		{
			return false;
		}
		System.Drawing.Point cursorPoint = System.Windows.Forms.Cursor.Position;
		Point screenPoint = new Point(cursorPoint.X, cursorPoint.Y);
		Point localPoint = contextMenu.PointFromScreen(screenPoint);
		Rect menuBounds = new Rect(0.0, 0.0, contextMenu.ActualWidth, contextMenu.ActualHeight);
		return menuBounds.Contains(localPoint);
	}

	private void BeginSlotContextMenuBubblePop(ContextMenu contextMenu)
	{
		if (!contextMenu.IsOpen || string.Equals(contextMenu.Tag as string, SlotContextMenuBubblePopClosingTag, StringComparison.Ordinal))
		{
			return;
		}
		contextMenu.Tag = SlotContextMenuBubblePopClosingTag;
		contextMenu.IsHitTestVisible = false;
		ScaleTransform scaleTransform = contextMenu.RenderTransform as ScaleTransform;
		if (scaleTransform == null)
		{
			scaleTransform = new ScaleTransform(1.0, 1.0);
			contextMenu.RenderTransform = scaleTransform;
		}
		BackEase scaleEase = new BackEase
		{
			Amplitude = 0.46,
			EasingMode = EasingMode.EaseIn
		};
		DoubleAnimation opacityAnimation = CreateAnimation(contextMenu.Opacity, 0.0, SlotContextMenuBubblePopMilliseconds, new QuinticEase
		{
			EasingMode = EasingMode.EaseIn
		});
		opacityAnimation.Completed += delegate
		{
			if (string.Equals(contextMenu.Tag as string, SlotContextMenuBubblePopClosingTag, StringComparison.Ordinal))
			{
				contextMenu.IsOpen = false;
			}
		};
		contextMenu.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
		scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(scaleTransform.ScaleX, SlotContextMenuBubblePopScale, SlotContextMenuBubblePopMilliseconds, scaleEase));
		scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(scaleTransform.ScaleY, SlotContextMenuBubblePopScale, SlotContextMenuBubblePopMilliseconds, scaleEase));
	}

	private static void ResetSlotContextMenuBubblePop(ContextMenu contextMenu)
	{
		contextMenu.BeginAnimation(UIElement.OpacityProperty, null);
		contextMenu.Opacity = 1.0;
		contextMenu.Tag = null;
		contextMenu.IsHitTestVisible = true;
		ScaleTransform scaleTransform = contextMenu.RenderTransform as ScaleTransform;
		if (scaleTransform == null)
		{
			scaleTransform = new ScaleTransform(1.0, 1.0);
			contextMenu.RenderTransform = scaleTransform;
		}
		scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		scaleTransform.ScaleX = 1.0;
		scaleTransform.ScaleY = 1.0;
	}

	private void RebuildSlotContextMenuItems(ContextMenu contextMenu, LauncherItem item)
	{
		contextMenu.Items.Clear();
		contextMenu.Items.Add(CreateSlotMenuHeader(item));
		contextMenu.Items.Add(CreateStyledSeparator());
		contextMenu.Items.Add(CreateSlotActionMenuItem(AppText.Choose("슬롯 이름 바꾸기...", "Rename slot..."), true, delegate
		{
			RenameSlot(item);
		}));
		contextMenu.Items.Add(CreateSlotActionMenuItem(item.TargetCount >= LauncherItem.MaxTargetsPerSlot ? AppText.Choose("슬롯 가득 참", "Slot is full") : AppText.Choose("프로그램/파일 추가...", "Add programs/files..."), item.TargetCount < LauncherItem.MaxTargetsPerSlot, delegate
		{
			BrowseForFile(item);
		}));
		contextMenu.Items.Add(CreateSlotActionMenuItem(item.TargetCount >= LauncherItem.MaxTargetsPerSlot ? AppText.Choose("폴더 추가 불가", "Cannot add folder") : AppText.Choose("폴더 추가...", "Add folder..."), item.TargetCount < LauncherItem.MaxTargetsPerSlot, delegate
		{
			BrowseForFolder(item);
		}));
		contextMenu.Items.Add(CreateSlotItemsHeader(item));
		foreach (LauncherTarget target in item.Targets)
		{
			contextMenu.Items.Add(CreateSlotTargetMenuItem(contextMenu, item, target));
		}
		if (item.TargetCount == 0)
		{
			contextMenu.Items.Add(CreateEmptySlotMenuItem());
		}
		contextMenu.Items.Add(CreateStyledSeparator());
		contextMenu.Items.Add(CreateSlotActionMenuItem(AppText.Choose("슬롯 비우기", "Clear slot"), item.HasTarget, delegate
		{
			ClearItem(contextMenu, item);
		}));
	}

	private MenuItem CreateSlotActionMenuItem(string header, bool isEnabled, Action click)
	{
		System.Windows.Controls.Button actionButton = new System.Windows.Controls.Button
		{
			Content = header,
				MinHeight = SlotMenuActionMinHeight,
				MinWidth = 0.0,
				Padding = new Thickness(8.0, 4.0, 8.0, 4.0),
				Margin = new Thickness(1.0, 1.0, 1.0, 1.0),
			HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
			Foreground = System.Windows.Media.Brushes.White,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(48, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 224, 242, 254)),
			BorderThickness = new Thickness(1.0),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = SlotMenuActionFontSize,
			FontWeight = FontWeights.SemiBold,
			Cursor = isEnabled ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
			IsEnabled = isEnabled
		};
		actionButton.Click += delegate(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			click();
		};
		MenuItem menuItem = new MenuItem
		{
			Header = actionButton,
			IsEnabled = true,
			StaysOpenOnClick = false
		};
		menuItem.PreviewMouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
		{
			if (isEnabled)
			{
				e.Handled = true;
				click();
			}
		};
		menuItem.Click += delegate
		{
			if (isEnabled)
			{
				click();
			}
		};
		return menuItem;
	}

	private static ControlTemplate CreateSlotContextMenuTemplate()
	{
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12.0));
		border.SetValue(Border.PaddingProperty, new Thickness(SlotContextMenuOuterPadding));
		border.SetValue(Border.BackgroundProperty, CreateContextMenuBrush());
		border.SetValue(Border.BorderBrushProperty, CreateGroupSlotBorderBrush());
		border.SetValue(Border.BorderThicknessProperty, new Thickness(1.1));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
		presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0));
		border.AppendChild(presenter);
		return new ControlTemplate(typeof(ContextMenu))
		{
			VisualTree = border
		};
	}

	private static MenuItem CreateSlotMenuHeader(LauncherItem item)
	{
		Grid grid = new Grid
		{
				Width = SlotMenuHeaderWidth,
				Margin = new Thickness(1.0, 1.0, 1.0, 2.0),
			ColumnDefinitions =
			{
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Vertical
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = item.DisplayName,
			Foreground = System.Windows.Media.Brushes.White,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuHeaderTitleFontSize,
			FontWeight = FontWeights.Black,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = item.HasTarget ? item.Subtitle : AppText.Choose("비어 있는 슬롯", "Empty slot"),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 230, 253)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuHeaderSubtitleFontSize,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		});
		grid.Children.Add(stackPanel);
		Border badge = new Border
		{
				MinWidth = 25.0,
				Height = 17.0,
				Padding = new Thickness(5.0, 0.0, 5.0, 0.0),
			Background = item.IsGroup ? CreateGroupBadgeBrush() : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(8.0),
			Child = new TextBlock
			{
				Text = item.TargetCount.ToString(),
				Foreground = System.Windows.Media.Brushes.White,
				FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuBadgeFontSize,
				FontWeight = FontWeights.Black,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		Grid.SetColumn(badge, 1);
		grid.Children.Add(badge);
		return new MenuItem
		{
			Header = grid,
			IsEnabled = false
		};
	}

	private static Border CreateActionMenuHeader(string header)
	{
		return new Border
		{
				MinHeight = SlotMenuActionMinHeight,
				Padding = new Thickness(8.0, 4.0, 8.0, 4.0),
				Margin = new Thickness(1.0, 1.0, 1.0, 1.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(28, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(42, 224, 242, 254)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(9.0),
			Child = new TextBlock
			{
				Text = header,
				Foreground = System.Windows.Media.Brushes.White,
				FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuActionFontSize,
				FontWeight = FontWeights.SemiBold
			}
		};
	}

	private MenuItem CreateSlotItemsHeader(LauncherItem item)
	{
		return new MenuItem
		{
			Header = new TextBlock
			{
				Text = item.TargetCount == 0 ? AppText.Choose("슬롯 안 항목 없음", "No items in this slot") : AppText.Choose($"슬롯 안 항목 {item.TargetCount}개", $"{item.TargetCount} items in this slot"),
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 230, 253)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = SlotMenuItemsHeaderFontSize,
			FontWeight = FontWeights.Bold
			},
			IsEnabled = false
		};
	}

	private MenuItem CreateSlotTargetMenuItem(ContextMenu contextMenu, LauncherItem item, LauncherTarget target)
	{
		Border rowBorder = new Border
		{
				Width = SlotMenuTargetRowWidth,
				MinHeight = SlotMenuTargetRowMinHeight,
				Margin = new Thickness(1.0, 2.0, 1.0, 2.0),
				Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(38, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(62, 186, 230, 253)),
			BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(8.0)
		};
		System.Windows.Controls.Button deleteButton = new System.Windows.Controls.Button
		{
			Content = "x",
				Width = SlotMenuDeleteButtonSize,
				Height = SlotMenuDeleteButtonSize,
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Padding = new Thickness(0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = CreateContextDeleteBrush(),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuTargetTitleFontSize,
			FontWeight = FontWeights.Black,
			Cursor = System.Windows.Input.Cursors.Hand,
			Focusable = false
		};
		deleteButton.Click += delegate(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			BeginSoftRemoveSlotTarget(contextMenu, item, target, rowBorder);
		};
		deleteButton.PreviewMouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			BeginSoftRemoveSlotTarget(contextMenu, item, target, rowBorder);
		};
		Grid grid = new Grid
		{
				Width = SlotMenuTargetGridWidth,
			ColumnDefinitions =
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				}
			}
		};
		grid.Children.Add(deleteButton);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Vertical
		};
		Grid.SetColumn(stackPanel, 1);
		stackPanel.Children.Add(new TextBlock
		{
			Text = target.DisplayName,
			Foreground = System.Windows.Media.Brushes.White,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuTargetTitleFontSize,
			FontWeight = FontWeights.Bold,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = $"{GetTargetKindLabel(target.Kind)} · {target.TargetPath}",
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = SlotMenuTargetPathFontSize,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		grid.Children.Add(stackPanel);
		rowBorder.Child = grid;
		return new MenuItem
		{
			Header = rowBorder,
			StaysOpenOnClick = true
		};
	}

	private static MenuItem CreateEmptySlotMenuItem()
	{
		return new MenuItem
		{
			Header = AppText.Choose("아직 등록된 항목이 없습니다.", "No items have been added yet."),
			IsEnabled = false
		};
	}

	private static Separator CreateStyledSeparator()
	{
		return new Separator
		{
				Margin = new Thickness(3.0, SlotMenuSeparatorVerticalMargin, 3.0, SlotMenuSeparatorVerticalMargin),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(92, 125, 211, 252))
		};
	}

	private static Style CreateSlotMenuItemStyle()
	{
		Style style = new Style(typeof(MenuItem));
		style.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, System.Windows.Media.Brushes.White));
		style.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(18, byte.MaxValue, byte.MaxValue, byte.MaxValue))));
		style.Setters.Add(new Setter(System.Windows.Controls.Control.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI")));
		style.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty, SlotMenuActionFontSize));
		style.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(7.0, 4.0, 7.0, 4.0)));
		style.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0.0)));
		return style;
	}

	private static string GetTargetKindLabel(LauncherItemKind kind)
	{
		return kind switch
		{
			LauncherItemKind.Program => AppText.Choose("프로그램", "Program"),
			LauncherItemKind.Shortcut => AppText.Choose("바로가기", "Shortcut"),
			LauncherItemKind.File => AppText.Choose("파일", "File"),
			LauncherItemKind.Folder => AppText.Choose("폴더", "Folder"),
			LauncherItemKind.Site => AppText.Choose("사이트", "Website"),
			_ => AppText.Choose("항목", "Item"),
		};
	}

	private UIElement CreateSlotContent(LauncherItem item)
	{
		Grid obj = new Grid
		{
			Margin = new Thickness(6.0, 6.0, 6.0, 6.0),
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
		};
		Border element = new Border
		{
			Width = 26.0,
			Height = 20.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(105, 124, 142, 168)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(8.0),
			Child = new TextBlock
			{
				Text = item.SlotLabel,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
				FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = SlotMenuTargetPathFontSize,
				FontWeight = FontWeights.Bold,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		obj.Children.Add(element);
		if (item.IsGroup)
		{
			Border groupBadge = new Border
			{
				Width = 34.0,
				Height = 22.0,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				Background = CreateGroupBadgeBrush(),
				BorderBrush = System.Windows.Media.Brushes.White,
				BorderThickness = new Thickness(1.2),
				CornerRadius = new CornerRadius(7.0),
				Effect = new DropShadowEffect
				{
					BlurRadius = 13.0,
					Color = System.Windows.Media.Color.FromRgb(250, 204, 21),
					Opacity = 0.78,
					ShadowDepth = 0.0
				},
				Child = new TextBlock
				{
					Text = $"x{item.TargetCount}",
					Foreground = System.Windows.Media.Brushes.White,
					FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
					FontSize = 9.0,
					FontWeight = FontWeights.Black,
					HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				}
			};
			obj.Children.Add(groupBadge);
		}
		Grid grid = new Grid
		{
			Width = SlotOrbSize,
			Height = SlotOrbSize,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetRow(grid, 1);
		grid.Children.Add(new Ellipse
		{
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(93, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			StrokeThickness = 1.4,
			Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(16, byte.MaxValue, byte.MaxValue, byte.MaxValue))
		});
		grid.Children.Add(CreateSlotOrbIconChrome(item));
		grid.Children.Add(CreateSlotOrbGlassCapsule());
		obj.Children.Add(grid);
		StackPanel stackPanel = new StackPanel();
		Grid.SetRow(stackPanel, 2);
		stackPanel.Children.Add(new TextBlock
		{
			Text = item.DisplayName,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = item.Subtitle,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(205, 224, 242, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 11.0,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		obj.Children.Add(stackPanel);
		return obj;
	}

	private Border CreateSlotOrbIconChrome(LauncherItem item)
	{
		Border border = new Border
		{
			Width = SlotOrbIconSize,
			Height = SlotOrbIconSize,
			CornerRadius = new CornerRadius(SlotOrbIconSize / 2.0),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Background = (item.HasTarget ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue))),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(82, 124, 142, 168)),
			BorderThickness = new Thickness(1.0),
			Clip = new EllipseGeometry(new Rect(0.0, 0.0, SlotOrbIconSize, SlotOrbIconSize))
		};
		Grid grid = new Grid();
		border.Child = grid;
		ImageSource imageSource = (item.HasTarget ? FileIconService.GetIcon(item.TargetPath) : null);
		if (imageSource != null)
		{
			grid.Children.Add(new System.Windows.Controls.Image
			{
				Width = SlotOrbIconSize,
				Height = SlotOrbIconSize,
				Source = imageSource,
				Stretch = Stretch.Fill,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		else
		{
			grid.Children.Add(new TextBlock
			{
				Text = "+",
				Foreground = System.Windows.Media.Brushes.White,
				FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = 34.0,
				FontWeight = FontWeights.Light,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		return border;
	}

	private static FrameworkElement CreateSlotOrbGlassCapsule()
	{
		Grid capsule = new Grid
		{
			Width = SlotOrbSize,
			Height = SlotOrbSize,
			IsHitTestVisible = false,
			Opacity = 0.78
		};
		capsule.SetValue(System.Windows.Controls.Panel.ZIndexProperty, 3);
		capsule.Children.Add(new Ellipse
		{
			Width = SlotOrbSize,
			Height = SlotOrbSize,
			Fill = new RadialGradientBrush
			{
				GradientOrigin = new Point(0.32, 0.18),
				Center = new Point(0.36, 0.26),
				RadiusX = 0.72,
				RadiusY = 0.58,
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(150, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(52, 186, 230, 253), 0.42),
					new GradientStop(System.Windows.Media.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0)
				}
			}
		});
		capsule.Children.Add(new Border
		{
			Width = SlotOrbGlassCapsuleWidth,
			Height = SlotOrbGlassCapsuleHeight,
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			CornerRadius = new CornerRadius(SlotOrbGlassCapsuleHeight / 2.0),
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(178, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(88, 240, 249, byte.MaxValue), 0.48),
					new GradientStop(System.Windows.Media.Color.FromArgb(8, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0)
				}
			}
		});
		return capsule;
	}

	private void SelectWidget(int widgetIndex)
	{
		if (widgetIndex >= 0 && widgetIndex < 3)
		{
			_selectedWidgetIndex = widgetIndex;
			for (int i = 0; i < WidgetTabs.Count; i++)
			{
				WidgetTabs[i].IsSelected = i == widgetIndex;
			}
			RefreshTabs();
			RefreshSlots();
			RefreshStatus();
			PersistLauncherState();
		}
	}

	private void RefreshTabs()
	{
		for (int i = 0; i < _tabButtons.Count; i++)
		{
			WidgetTab widgetTab = WidgetTabs[i];
			_tabButtons[i].Background = CreateTabBrush(widgetTab.IsSelected);
			_tabButtons[i].BorderBrush = (widgetTab.IsSelected ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 191, 219, 254)));
			_tabButtons[i].Content = CreateTabContent(widgetTab);
			if (_tabButtons[i].ContextMenu?.Items.Count > 0 && _tabButtons[i].ContextMenu.Items[0] is MenuItem renameMenuItem)
			{
				renameMenuItem.Header = AppText.Choose("이름 바꾸기...", "Rename...");
			}
			ApplyTabGlow(_tabButtons[i], widgetTab.IsSelected);
		}
	}

	private static void ApplyTabGlow(System.Windows.Controls.Button button, bool selected)
	{
		if (!selected)
		{
			if (button.Effect is DropShadowEffect dropShadowEffect)
			{
				dropShadowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
				dropShadowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, null);
			}
			button.Effect = null;
			return;
		}
		DropShadowEffect obj = new DropShadowEffect
		{
			Color = System.Windows.Media.Color.FromRgb(96, 165, 250),
			ShadowDepth = 0.0,
			BlurRadius = 24.0,
			Opacity = 0.82
		};
		Effect effect = obj;
		button.Effect = obj;
		DropShadowEffect obj2 = (DropShadowEffect)effect;
		DoubleAnimation animation = new DoubleAnimation(16.0, 34.0, TimeSpan.FromMilliseconds(980.0))
		{
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever,
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		DoubleAnimation animation2 = new DoubleAnimation(0.45, 1.0, TimeSpan.FromMilliseconds(980.0))
		{
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever,
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		obj2.BeginAnimation(DropShadowEffect.BlurRadiusProperty, animation);
		obj2.BeginAnimation(DropShadowEffect.OpacityProperty, animation2);
	}

	private void RefreshSlots()
	{
		_slotGrid.Children.Clear();
		foreach (LauncherItem item in _widgets[_selectedWidgetIndex])
		{
			_slotGrid.Children.Add(CreateSlotButton(item));
		}
	}

	private void ToggleEditMode()
	{
		_isEditMode = !_isEditMode;
		if (_isEditMode)
		{
			EnterEditModePinned();
		}
		else
		{
			LeaveEditModeHover();
		}
		UpdateEditModeButton();
		RefreshSlots();
		RefreshStatus();
	}

	private void EnterEditModePinned()
	{
		_hideAnimationGeneration++;
		_isHideAnimationRunning = false;
		_lastPointerSafeUtc = DateTime.UtcNow;
		_hoverHideTimer.Stop();
		if (!base.IsVisible)
		{
			Show();
			PositionNearTaskbar();
		}
		if (base.WindowState == WindowState.Minimized)
		{
			base.WindowState = WindowState.Normal;
		}
		Activate();
		base.Topmost = true;
		Focus();
	}

	private void LeaveEditModeHover()
	{
		base.Topmost = false;
		_lastPointerSafeUtc = DateTime.UtcNow;
		_hoverHideTimer.Start();
	}

	private void UpdateEditModeButton()
	{
		if (_editModeButton != null)
		{
			_editModeButton.Content = _isEditMode ? AppText.Choose("수정 종료", "Finish Editing") : AppText.Choose("앱 수정", "Edit Apps");
		}
	}

	private void RefreshStatus()
	{
		_statusText.Text = AppText.Choose(
			$"{WidgetTabs[_selectedWidgetIndex].Name}: {WidgetTabs[_selectedWidgetIndex].ConfiguredCount}/10 준비됨" + (_isEditMode ? " · 수정 중" : ""),
			$"{WidgetTabs[_selectedWidgetIndex].Name}: {WidgetTabs[_selectedWidgetIndex].ConfiguredCount}/10 ready" + (_isEditMode ? " · editing" : ""));
	}

	private void UpdateWidgetCounts()
	{
		for (int i = 0; i < 3; i++)
		{
			WidgetTabs[i].ConfiguredCount = _widgets[i].Count((LauncherItem item) => item.HasTarget);
		}
		RefreshTabs();
		RefreshStatus();
	}

	private void PersistLauncherState()
	{
		UpdateWidgetCounts();
		_store.Save(AllItems, LauncherTitle, WidgetTabs, _selectedWidgetIndex);
	}

	private string SaveCurrentSettingsSnapshot(string snapshotFile)
	{
		PersistLauncherState();
		return _store.SaveCurrentSettingsSnapshotFile(snapshotFile, AllItems, LauncherTitle, WidgetTabs, _selectedWidgetIndex);
	}

	private LauncherDisplaySettings LoadSavedSettingsSnapshot(string snapshotFile)
	{
		LauncherDisplaySettings settings = _store.LoadSavedSettingsSnapshotFile(snapshotFile, ((IEnumerable<ObservableCollection<LauncherItem>>)_widgets).Select((Func<ObservableCollection<LauncherItem>, IReadOnlyList<LauncherItem>>)((ObservableCollection<LauncherItem> widget) => widget)).ToList());
		ApplyDisplaySettings(settings);
		SelectWidget(settings.SelectedWidgetIndex);
		UpdateWidgetCounts();
		RefreshSlots();
		RefreshStatus();
		UpdateTrayLabels();
		return settings;
	}

	private void ResetLauncherStateToDefaults()
	{
		foreach (ObservableCollection<LauncherItem> widget in _widgets)
		{
			foreach (LauncherItem item in widget)
			{
				item.Clear();
				item.SetSlotName(null);
			}
		}
		LauncherTitle = LauncherStore.DefaultLauncherTitle;
		foreach (WidgetTab tab in WidgetTabs)
		{
			tab.Name = string.Empty;
		}
		SelectWidget(0);
		UpdateWidgetCounts();
		RefreshSlots();
		RefreshStatus();
		UpdateTrayLabels();
		PersistLauncherState();
	}

	private void ResetWidgetToDefaults(int widgetIndex)
	{
		if (widgetIndex < 0 || widgetIndex >= _widgets.Count)
		{
			return;
		}
		foreach (LauncherItem item in _widgets[widgetIndex])
		{
			item.Clear();
			item.SetSlotName(null);
		}
		WidgetTabs[widgetIndex].Name = string.Empty;
		UpdateWidgetCounts();
		RefreshTabs();
		RefreshSlots();
		RefreshStatus();
		UpdateTrayLabels();
		PersistLauncherState();
	}

	private static string GetAppTopLevelDirectory()
	{
		return AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
	}

	private void RenameLauncherTitle()
	{
		RenameDialog renameDialog = new RenameDialog(AppText.Choose("맨 위 제목 이름", "Top title"), LauncherTitle)
		{
			Owner = this
		};
		if (renameDialog.ShowDialog() == true)
		{
			LauncherTitle = renameDialog.ResultName;
			PersistLauncherState();
		}
	}

	private void RenameWidget(WidgetTab tab)
	{
		RenameDialog renameDialog = new RenameDialog(AppText.Choose(tab.Name + " 이름", tab.Name + " name"), tab.Name)
		{
			Owner = this
		};
		if (renameDialog.ShowDialog() == true)
		{
			tab.Name = renameDialog.ResultName;
			UpdateTrayLabels();
			PersistLauncherState();
			RefreshTabs();
			RefreshStatus();
		}
	}

	private void SetItemTarget(LauncherItem item, string targetPath)
	{
		if (item.TargetCount >= LauncherItem.MaxTargetsPerSlot)
		{
			System.Windows.MessageBox.Show(this, AppText.Choose("이 슬롯에는 최대 10개까지 넣을 수 있습니다.", "You can add up to 10 items to this slot."), LauncherStore.DefaultLauncherTitle, MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		item.AddTarget(targetPath);
		PersistLauncherState();
		RefreshSlots();
		BeginBubbleShowAnimation(fromTray: true);
	}

	private void SetItemTargetFromDrop(LauncherItem item, string droppedPath)
	{
		if (_isEditMode && !string.IsNullOrWhiteSpace(droppedPath))
		{
			string targetPath = PrepareDroppedPathForSlot(item, droppedPath);
			SetItemTarget(item, targetPath);
		}
	}

	private void NormalizeExistingShortcutItems()
	{
		bool flag = false;
		foreach (LauncherItem allItem in AllItems)
		{
			if (allItem.HasTarget && allItem.Targets.Count == 1 && !string.IsNullOrWhiteSpace(allItem.TargetPath) && IsShortcutFile(allItem.TargetPath) && !IsManagedShortcutPath(allItem.TargetPath) && File.Exists(allItem.TargetPath))
			{
				try
				{
					allItem.SetTarget(MoveShortcutIntoSlot(allItem, allItem.TargetPath));
					flag = true;
				}
				catch
				{
				}
			}
		}
		if (flag)
		{
			PersistLauncherState();
			RefreshSlots();
		}
	}

	private void ClearItem(ContextMenu contextMenu, LauncherItem item)
	{
		if (_isEditMode)
		{
			foreach (LauncherTarget target in item.Targets)
			{
				DeleteManagedShortcutIfNeeded(target.TargetPath);
			}
			item.Clear();
			PersistLauncherState();
			RefreshSlots();
			if (contextMenu != null)
			{
				RebuildSlotContextMenuItems(contextMenu, item);
				contextMenu.IsOpen = false;
			}
		}
	}

	private void RenameSlot(LauncherItem item)
	{
		if (!_isEditMode)
		{
			return;
		}
		RenameDialog renameDialog = new RenameDialog(AppText.Choose("슬롯 이름", "Slot name"), item.SlotName ?? item.DisplayName)
		{
			Owner = this
		};
		if (renameDialog.ShowDialog() == true)
		{
			item.SetSlotName(renameDialog.ResultName);
			PersistLauncherState();
			RefreshSlots();
		}
	}

	private void BeginSoftRemoveSlotTarget(ContextMenu contextMenu, LauncherItem item, LauncherTarget target, FrameworkElement rowElement)
	{
		if (!_isEditMode || !_targetsBeingRemoved.Add(target))
		{
			return;
		}
		double startHeight = rowElement.ActualHeight > 1.0 ? rowElement.ActualHeight : 50.0;
		rowElement.MaxHeight = startHeight;
		Duration duration = new Duration(TimeSpan.FromMilliseconds(210.0));
		DoubleAnimation opacityAnimation = new DoubleAnimation(0.0, duration)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		DoubleAnimation heightAnimation = new DoubleAnimation(startHeight, 0.0, duration)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		opacityAnimation.Completed += delegate
		{
			_targetsBeingRemoved.Remove(target);
			RemoveSlotTarget(contextMenu, item, target);
		};
		rowElement.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
		rowElement.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnimation);
	}

	private void RemoveSlotTarget(ContextMenu contextMenu, LauncherItem item, LauncherTarget target)
	{
		int targetIndex = item.Targets.ToList().IndexOf(target);
		if (targetIndex < 0)
		{
			return;
		}
		string targetPath = target.TargetPath;
		if (item.RemoveTargetAt(targetIndex))
		{
			DeleteManagedShortcutIfNeeded(targetPath);
			PersistLauncherState();
			RefreshSlots();
			if (contextMenu != null && contextMenu.IsOpen)
			{
				RebuildSlotContextMenuItems(contextMenu, item);
			}
		}
	}

	private string PrepareDroppedPathForSlot(LauncherItem item, string droppedPath)
	{
		if (!File.Exists(droppedPath) && !Directory.Exists(droppedPath))
		{
			return droppedPath;
		}
		if (IsShortcutFile(droppedPath))
		{
			return MoveShortcutIntoSlot(item, droppedPath);
		}
		return CreateShortcutIntoSlot(item, droppedPath);
	}

	private string MoveShortcutIntoSlot(LauncherItem item, string shortcutPath)
	{
		string text = BuildManagedShortcutPath(item, System.IO.Path.GetFileNameWithoutExtension(shortcutPath), System.IO.Path.GetExtension(shortcutPath));
		if (PathsEqual(shortcutPath, text))
		{
			return text;
		}
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		File.Copy(shortcutPath, text, overwrite: false);
		DeleteOriginalShortcutFile(shortcutPath);
		return text;
	}

	private string CreateShortcutIntoSlot(LauncherItem item, string targetPath)
	{
		string displayName = (Directory.Exists(targetPath) ? new DirectoryInfo(targetPath).Name : System.IO.Path.GetFileNameWithoutExtension(targetPath));
		string text = BuildManagedShortcutPath(item, displayName, ".lnk");
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		string workingDirectory = (Directory.Exists(targetPath) ? targetPath : System.IO.Path.GetDirectoryName(targetPath));
		CreateWindowsShortcut(text, targetPath, workingDirectory, AppText.Choose("PIUS 슬롯 바로가기", "PIUS slot shortcut"));
		return text;
	}

	private bool TrySetItemTargetFromVirtualDrop(LauncherItem item, System.Windows.IDataObject data)
	{
		if (!TryGetVirtualFileName(data, out var fileName))
		{
			if (TryGetShellIdListNames(data, out var displayName, out var parsingName))
			{
				if (!TryCreateKnownShellShortcutFromName(item, parsingName, data))
				{
					return TryCreateKnownShellShortcutFromName(item, displayName, data);
				}
				return true;
			}
			return false;
		}
		if (!data.GetDataPresent("FileContents"))
		{
			if (TryCreateKnownShellShortcutFromName(item, fileName, data))
			{
				return true;
			}
			if (TryGetShellIdListNames(data, out var displayName2, out var parsingName2))
			{
				if (!TryCreateKnownShellShortcutFromName(item, parsingName2, data))
				{
					return TryCreateKnownShellShortcutFromName(item, displayName2, data);
				}
				return true;
			}
			return false;
		}
		if (TryCreateKnownShellShortcutFromName(item, fileName, data))
		{
			return true;
		}
		if (TryGetShellIdListNames(data, out var displayName3, out var parsingName3))
		{
			if (!TryCreateKnownShellShortcutFromName(item, parsingName3, data))
			{
				return TryCreateKnownShellShortcutFromName(item, displayName3, data);
			}
			return true;
		}
		string text = System.IO.Path.GetExtension(fileName);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = ".lnk";
			fileName += text;
		}
		string text2 = BuildManagedShortcutPath(item, System.IO.Path.GetFileNameWithoutExtension(fileName), text);
		if (File.Exists(text2))
		{
			File.Delete(text2);
		}
		if (!TrySaveVirtualFileContents(data, text2))
		{
			return TryCreateKnownShellShortcutFromName(item, fileName, data);
		}
		SetItemTarget(item, text2);
		return true;
	}

	private static bool HasVirtualShortcutDropData(System.Windows.IDataObject data)
	{
		if (!data.GetDataPresent("FileGroupDescriptorW") && !data.GetDataPresent("FileGroupDescriptor") && !data.GetDataPresent("FileContents"))
		{
			return data.GetDataPresent("Shell IDList Array");
		}
		return true;
	}

	private bool TryCreateKnownShellShortcutFromName(LauncherItem item, string fileName, System.Windows.IDataObject? data = null)
	{
		if (!TryResolveKnownShellShortcut(fileName, out var displayName, out var shellArgument, out var iconLocation))
		{
			return false;
		}
		string text = BuildManagedShortcutPath(item, displayName, ".lnk");
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		if (data != null && TrySaveShellIconFromData(item, data, displayName, out var iconPath))
		{
			iconLocation = iconPath;
		}
		CreateWindowsShortcut(text, "explorer.exe", null, AppText.Choose("PIUS 시스템 바로가기", "PIUS system shortcut"), shellArgument, iconLocation);
		SetItemTarget(item, text);
		return true;
	}

	private static bool TryResolveKnownShellShortcut(string fileName, out string displayName, out string shellArgument, out string iconLocation)
	{
		string text = (displayName = NormalizeShellItemName(fileName));
		shellArgument = string.Empty;
		iconLocation = string.Empty;
		if (text.Contains("20D04FE03AEA1069A2D808002B30309D", StringComparison.OrdinalIgnoreCase) || text.Contains("내pc", StringComparison.OrdinalIgnoreCase) || text.Contains("내컴퓨터", StringComparison.OrdinalIgnoreCase) || text.Contains("thispc", StringComparison.OrdinalIgnoreCase) || text.Contains("mycomputer", StringComparison.OrdinalIgnoreCase) || text.Equals("computer", StringComparison.OrdinalIgnoreCase))
		{
			displayName = AppText.Choose("내 PC", "This PC");
			shellArgument = "shell:MyComputerFolder";
			iconLocation = "imageres.dll,109";
			return true;
		}
		if (text.Contains("26EE0668A00A44D79371BEB064C98683", StringComparison.OrdinalIgnoreCase) || text.Contains("21EC20203AEA1069A2DD08002B30309D", StringComparison.OrdinalIgnoreCase) || text.Contains("5399E6946CE54D6C8FCE1D8870FDCBA0", StringComparison.OrdinalIgnoreCase) || text.Contains("제어판", StringComparison.OrdinalIgnoreCase) || text.Contains("controlpanel", StringComparison.OrdinalIgnoreCase))
		{
			displayName = AppText.Choose("제어판", "Control Panel");
			shellArgument = "shell:ControlPanelFolder";
			iconLocation = "imageres.dll,27";
			return true;
		}
		if (text.Contains("645FF0405081101B9F0800AA002F954E", StringComparison.OrdinalIgnoreCase) || text.Contains("휴지통", StringComparison.OrdinalIgnoreCase) || text.Contains("recyclebin", StringComparison.OrdinalIgnoreCase))
		{
			displayName = AppText.Choose("휴지통", "Recycle Bin");
			shellArgument = "shell:RecycleBinFolder";
			iconLocation = "imageres.dll,54";
			return true;
		}
		if (text.Contains("F02C1A0DBE21435088B07367FC96EF3C", StringComparison.OrdinalIgnoreCase) || text.Contains("네트워크", StringComparison.OrdinalIgnoreCase) || text.Equals("network", StringComparison.OrdinalIgnoreCase))
		{
			displayName = AppText.Choose("네트워크", "Network");
			shellArgument = "shell:NetworkPlacesFolder";
			iconLocation = "imageres.dll,25";
			return true;
		}
		if (text.Contains("문서", StringComparison.OrdinalIgnoreCase) || text.Equals("documents", StringComparison.OrdinalIgnoreCase))
		{
			displayName = AppText.Choose("문서", "Documents");
			shellArgument = "shell:Personal";
			iconLocation = "imageres.dll,112";
			return true;
		}
		return false;
	}

	private static string NormalizeShellItemName(string fileName)
	{
		string text = System.IO.Path.GetFileNameWithoutExtension(fileName);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = fileName;
		}
		int num = text.IndexOf(".{", StringComparison.Ordinal);
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		return new string(text.Where((char c) => !char.IsWhiteSpace(c) && c != '-' && c != '_' && c != '{' && c != '}' && c != ':' && c != '\\' && c != '/').ToArray());
	}

	private static bool TryGetShellIdListNames(System.Windows.IDataObject data, out string displayName, out string parsingName)
	{
		displayName = string.Empty;
		parsingName = string.Empty;
		try
		{
			if (!TryGetAbsolutePidlFromData(data, out var absolute))
			{
				return false;
			}
			try
			{
				displayName = GetNameFromPidl(absolute, 0u);
				parsingName = GetNameFromPidl(absolute, 2147647488u);
				return !string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(parsingName);
			}
			finally
			{
				ILFree(absolute);
			}
		}
		catch
		{
			displayName = string.Empty;
			parsingName = string.Empty;
			return false;
		}
	}

	private bool TrySaveShellIconFromData(LauncherItem item, System.Windows.IDataObject data, string displayName, out string iconPath)
	{
		iconPath = string.Empty;
		if (!TryGetAbsolutePidlFromData(data, out var absolute))
		{
			return false;
		}
		try
		{
			ShellFileInfo psfi = default(ShellFileInfo);
			if (SHGetFileInfo(absolute, 0u, ref psfi, (uint)Marshal.SizeOf<ShellFileInfo>(), 264u) == IntPtr.Zero || psfi.hIcon == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				iconPath = BuildManagedShortcutPath(item, displayName + "-아이콘", ".ico");
				if (File.Exists(iconPath))
				{
					File.Delete(iconPath);
				}
				using DrawingIcon icon = (DrawingIcon)DrawingIcon.FromHandle(psfi.hIcon).Clone();
				using FileStream outputStream = File.Create(iconPath);
				icon.Save(outputStream);
				return File.Exists(iconPath) && new FileInfo(iconPath).Length > 0;
			}
			finally
			{
				DestroyIcon(psfi.hIcon);
			}
		}
		catch
		{
			iconPath = string.Empty;
			return false;
		}
		finally
		{
			ILFree(absolute);
		}
	}

	private static bool TryGetAbsolutePidlFromData(System.Windows.IDataObject data, out nint absolute)
	{
		absolute = IntPtr.Zero;
		try
		{
			if (!(data.GetData("Shell IDList Array") is MemoryStream memoryStream))
			{
				return false;
			}
			byte[] array = memoryStream.ToArray();
			if (array.Length < 12 || BitConverter.ToUInt32(array, 0) < 1)
			{
				return false;
			}
			int offset = (int)BitConverter.ToUInt32(array, 4);
			int offset2 = (int)BitConverter.ToUInt32(array, 8);
			using PidlHandle pidlHandle = PidlHandle.FromCida(array, offset);
			using PidlHandle pidlHandle2 = PidlHandle.FromCida(array, offset2);
			absolute = ILCombine(pidlHandle.Handle, pidlHandle2.Handle);
			return absolute != IntPtr.Zero;
		}
		catch
		{
			if (absolute != IntPtr.Zero)
			{
				ILFree(absolute);
				absolute = IntPtr.Zero;
			}
			return false;
		}
	}

	private static string GetNameFromPidl(nint pidl, uint sigdn)
	{
		nint ppszName = IntPtr.Zero;
		try
		{
			if (SHGetNameFromIDList(pidl, sigdn, out ppszName) != 0 || ppszName == IntPtr.Zero)
			{
				return string.Empty;
			}
			return Marshal.PtrToStringUni(ppszName) ?? string.Empty;
		}
		finally
		{
			if (ppszName != IntPtr.Zero)
			{
				CoTaskMemFree(ppszName);
			}
		}
	}

	private static bool TryGetVirtualFileName(System.Windows.IDataObject data, out string fileName)
	{
		fileName = string.Empty;
		try
		{
			if (!((data.GetData("FileGroupDescriptorW") ?? data.GetData("FileGroupDescriptor")) is MemoryStream memoryStream))
			{
				return false;
			}
			byte[] array = memoryStream.ToArray();
			int num = 76;
			if (array.Length <= num)
			{
				return false;
			}
			int count = Math.Min(520, array.Length - num);
			string text = (data.GetDataPresent("FileGroupDescriptorW") ? Encoding.Unicode.GetString(array, num, count) : Encoding.Default.GetString(array, num, Math.Min(260, array.Length - num)));
			int num2 = text.IndexOf('\0');
			fileName = ((num2 >= 0) ? text.Substring(0, num2) : text).Trim();
			if (string.IsNullOrWhiteSpace(fileName))
			{
				fileName = "시스템 아이콘.lnk";
			}
			return true;
		}
		catch
		{
			fileName = string.Empty;
			return false;
		}
	}

	private static bool TrySaveVirtualFileContents(System.Windows.IDataObject data, string targetPath)
	{
		try
		{
			object data2 = data.GetData("FileContents");
			if (!(data2 is MemoryStream memoryStream))
			{
				if (!(data2 is Stream stream))
				{
					if (data2 is byte[] array)
					{
						File.WriteAllBytes(targetPath, array);
						return array.Length != 0;
					}
					return false;
				}
				if (stream.CanSeek)
				{
					stream.Position = 0L;
				}
				using (FileStream destination = File.Create(targetPath))
				{
					stream.CopyTo(destination);
				}
				return new FileInfo(targetPath).Length > 0;
			}
			memoryStream.Position = 0L;
			using (FileStream destination2 = File.Create(targetPath))
			{
				memoryStream.CopyTo(destination2);
			}
			return new FileInfo(targetPath).Length > 0;
		}
		catch
		{
			try
			{
				if (File.Exists(targetPath))
				{
					File.Delete(targetPath);
				}
			}
			catch
			{
			}
			return false;
		}
	}

	private string BuildManagedShortcutPath(LauncherItem item, string displayName, string extension)
	{
		string managedShortcutDirectory = GetManagedShortcutDirectory();
		string value = CreateSafeFileName(string.IsNullOrWhiteSpace(displayName) ? $"슬롯 {item.SlotIndex + 1:00}" : displayName);
		if (string.IsNullOrWhiteSpace(extension) || extension.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
		{
			extension = ".lnk";
		}
		if (!extension.StartsWith(".", StringComparison.Ordinal))
		{
			extension = "." + extension;
		}
		return System.IO.Path.Combine(managedShortcutDirectory, $"위젯{item.WidgetIndex + 1:00}-슬롯{item.SlotIndex + 1:00}-항목{Math.Min(item.TargetCount + 1, LauncherItem.MaxTargetsPerSlot):00}-{value}{extension}");
	}

	private static string GetManagedShortcutDirectory()
	{
		string text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiusLauncher", "SlotShortcuts");
		Directory.CreateDirectory(text);
		return text;
	}

	private static string CreateSafeFileName(string name)
	{
		char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			name = name.Replace(oldChar, '_');
		}
		if (name.Length > 56)
		{
			return name.Substring(0, 56);
		}
		return name;
	}

	private static bool IsManagedShortcutPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		string value = System.IO.Path.GetFullPath(GetManagedShortcutDirectory()).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
		return System.IO.Path.GetFullPath(path).StartsWith(value, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsShortcutFile(string path)
	{
		string extension = System.IO.Path.GetExtension(path);
		if (!string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".url", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(extension, ".appref-ms", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static void DeleteOriginalShortcutFile(string shortcutPath)
	{
		if (IsManagedShortcutPath(shortcutPath) || !File.Exists(shortcutPath))
		{
			return;
		}
		try
		{
			File.SetAttributes(shortcutPath, File.GetAttributes(shortcutPath) & ~FileAttributes.ReadOnly);
			File.Delete(shortcutPath);
		}
		catch
		{
		}
	}

	private static void DeleteManagedShortcutIfNeeded(string? path)
	{
		try
		{
			if (IsManagedShortcutPath(path) && File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private void LaunchItem(LauncherItem item)
	{
		if (!item.HasTarget)
		{
			return;
		}
		List<string> errors = new List<string>();
		foreach (LauncherTarget target in item.Targets)
		{
			try
			{
				LaunchTarget(target.TargetPath);
			}
			catch (Exception ex)
			{
				errors.Add($"{target.DisplayName}: {ex.Message}");
			}
		}
		if (errors.Count > 0)
		{
			System.Windows.MessageBox.Show(this, AppText.Choose("일부 항목을 열 수 없습니다.\n\n", "Some items could not be opened.\n\n") + string.Join("\n", errors), LauncherStore.DefaultLauncherTitle, MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void LaunchTarget(string targetPath)
	{
		if (string.IsNullOrWhiteSpace(targetPath))
		{
			return;
		}
		try
		{
			if (TryActivateRunningProgram(targetPath))
			{
				return;
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo(targetPath)
			{
				UseShellExecute = true
			};
			if (File.Exists(targetPath))
			{
				string directoryName = System.IO.Path.GetDirectoryName(targetPath);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					processStartInfo.WorkingDirectory = directoryName;
				}
			}
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(ex.Message, ex);
		}
	}

	private bool TryActivateRunningProgram(string targetPath)
	{
		if (IsShortcutWithArguments(targetPath))
		{
			return false;
		}
		string text = ResolveExecutableTargetPath(targetPath);
		if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
		{
			return false;
		}
		if (!string.Equals(System.IO.Path.GetExtension(text), ".exe", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string text2;
		try
		{
			text2 = System.IO.Path.GetFullPath(text);
		}
		catch
		{
			text2 = text;
		}
		string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text2);
		if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
		{
			return false;
		}
		bool result = false;
		Process[] processesByName = Process.GetProcessesByName(fileNameWithoutExtension);
		foreach (Process process in processesByName)
		{
			using (process)
			{
				if (process.Id == Environment.ProcessId)
				{
					continue;
				}
				string text3 = TryGetProcessPath(process);
				if (string.IsNullOrWhiteSpace(text3) || PathsEqual(text3, text2))
				{
					result = true;
					if (TryBringProcessWindowToFront(process))
					{
						return true;
					}
				}
			}
		}
		return result;
	}

	private static bool IsShortcutWithArguments(string targetPath)
	{
		if (!string.Equals(System.IO.Path.GetExtension(targetPath), ".lnk", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return !string.IsNullOrWhiteSpace(ResolveShortcutArguments(targetPath));
	}

	private static string? ResolveExecutableTargetPath(string targetPath)
	{
		if (string.Equals(System.IO.Path.GetExtension(targetPath), ".lnk", StringComparison.OrdinalIgnoreCase))
		{
			string text = ResolveShortcutTarget(targetPath);
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		return targetPath;
	}

	private static string? TryGetProcessPath(Process process)
	{
		try
		{
			return process.MainModule?.FileName;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryBringProcessWindowToFront(Process process)
	{
		nint num = process.MainWindowHandle;
		if (num == 0)
		{
			num = FindProcessWindow(process.Id);
		}
		if (num == 0)
		{
			return false;
		}
		ShowWindow(num, 9);
		return SetForegroundWindow(num);
	}

	private static nint FindProcessWindow(int processId)
	{
		nint foundHandle = 0;
		EnumWindows(delegate(nint hWnd, nint _)
		{
			GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
			if (lpdwProcessId != processId || !IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
			{
				return true;
			}
			foundHandle = hWnd;
			return false;
		}, 0);
		return foundHandle;
	}

	private static bool PathsEqual(string firstPath, string secondPath)
	{
		try
		{
			firstPath = System.IO.Path.GetFullPath(firstPath);
			secondPath = System.IO.Path.GetFullPath(secondPath);
		}
		catch
		{
		}
		return string.Equals(firstPath.TrimEnd(System.IO.Path.DirectorySeparatorChar), secondPath.TrimEnd(System.IO.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
	}

	private void BrowseForFile(LauncherItem item)
	{
		if (_isEditMode)
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Title = AppText.Choose("프로그램, 바로가기 또는 파일 선택", "Select programs, shortcuts, or files"),
				Filter = AppText.Choose("프로그램, 바로가기, 파일|*.exe;*.lnk;*.url;*.bat;*.cmd;*.com;*.msi;*.appref-ms;*.*|모든 파일|*.*", "Programs, shortcuts, and files|*.exe;*.lnk;*.url;*.bat;*.cmd;*.com;*.msi;*.appref-ms;*.*|All files|*.*"),
				CheckFileExists = true,
				Multiselect = true
			};
			if (openFileDialog.ShowDialog(this) == true)
			{
				foreach (string fileName in openFileDialog.FileNames.Take(LauncherItem.MaxTargetsPerSlot - item.TargetCount))
				{
					SetItemTargetFromDrop(item, fileName);
				}
			}
		}
	}

	private void BrowseForFolder(LauncherItem item)
	{
		if (!_isEditMode)
		{
			return;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = AppText.Choose("폴더 선택", "Select a folder"),
			UseDescriptionForTitle = true,
			ShowNewFolderButton = false
		};
		if (folderBrowserDialog.ShowDialog(new WindowHandle(this)) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
		{
			SetItemTargetFromDrop(item, folderBrowserDialog.SelectedPath);
		}
	}

	private void SlotButton_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is System.Windows.Controls.Button { Tag: LauncherItem tag } button))
		{
			return;
		}
		if (_suppressNextSlotClick)
		{
			_suppressNextSlotClick = false;
			return;
		}
		if (_isEditMode)
		{
			if (button.ContextMenu != null)
			{
				button.ContextMenu.PlacementTarget = button;
				button.ContextMenu.IsOpen = true;
			}
		}
		else if (tag.HasTarget)
		{
			LaunchItem(tag);
		}
	}

	private void SlotButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (!_isEditMode || sender is not System.Windows.Controls.Button { Tag: LauncherItem item } button)
		{
			return;
		}
		_slotDragStartPoint = e.GetPosition(_slotGrid);
		_slotDragSourceItem = item;
		_slotDragSourceButton = button;
		_isSlotDragActive = false;
		_suppressNextSlotClick = false;
		_slotDragPreviewIndex = item.SlotIndex;
	}

	private void SlotButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (!_isEditMode || sender is not System.Windows.Controls.Button { Tag: LauncherItem item } button || !ReferenceEquals(_slotDragSourceItem, item))
		{
			return;
		}
		if (e.LeftButton != MouseButtonState.Pressed)
		{
			if (_isSlotDragActive)
			{
				CompleteSlotDrag(commit: false);
			}
			return;
		}
		Point currentPoint = e.GetPosition(_slotGrid);
		if (!_isSlotDragActive)
		{
			if (Math.Abs(currentPoint.X - _slotDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(currentPoint.Y - _slotDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
			{
				return;
			}
			BeginSlotDrag(button);
		}
		UpdateSlotDrag(currentPoint);
		e.Handled = true;
	}

	private void SlotButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!_isSlotDragActive)
		{
			return;
		}
		CompleteSlotDrag(commit: true);
		e.Handled = true;
	}

	private void BeginSlotDrag(System.Windows.Controls.Button button)
	{
		_isSlotDragActive = true;
		_suppressNextSlotClick = true;
		_slotDragPreviewIndex = (button.Tag is LauncherItem item) ? item.SlotIndex : -1;
		if (button.ContextMenu != null)
		{
			button.ContextMenu.IsOpen = false;
		}
		button.CaptureMouse();
		button.LostMouseCapture += SlotDragSourceButton_LostMouseCapture;
		System.Windows.Controls.Panel.SetZIndex(button, SlotDragLiftZIndex);
		button.RenderTransformOrigin = new Point(0.5, 0.5);
		button.RenderTransform = new TransformGroup
		{
			Children =
			{
				(Transform)new ScaleTransform(1.0, 1.0),
				(Transform)new TranslateTransform(0.0, 0.0)
			}
		};
		AnimateSlotDragLift(button);
	}

	private void UpdateSlotDrag(Point pointInGrid)
	{
		if (!_isSlotDragActive || _slotDragSourceButton == null || _slotDragSourceItem == null)
		{
			return;
		}
		if (_slotDragSourceButton.RenderTransform is TransformGroup group && group.Children.Count >= 2 && group.Children[1] is TranslateTransform translate)
		{
			translate.X = pointInGrid.X - _slotDragStartPoint.X;
			translate.Y = pointInGrid.Y - _slotDragStartPoint.Y;
		}
		int targetIndex = ResolveSlotIndexFromPoint(pointInGrid);
		if (targetIndex != _slotDragPreviewIndex)
		{
			_slotDragPreviewIndex = targetIndex;
			UpdateSlotReorderPreview(_slotDragSourceItem.SlotIndex, targetIndex);
		}
	}

	private int ResolveSlotIndexFromPoint(Point pointInGrid)
	{
		int columns = Math.Max(1, _slotGrid.Columns);
		int rows = Math.Max(1, _slotGrid.Rows);
		int maxIndex = _slotGrid.Children.Count - 1;
		if (maxIndex < 0)
		{
			return 0;
		}
		double width = _slotGrid.ActualWidth;
		double height = _slotGrid.ActualHeight;
		if (width <= 0.0 || height <= 0.0)
		{
			return (_slotDragSourceItem != null) ? _slotDragSourceItem.SlotIndex : 0;
		}
		int column = Math.Clamp((int)Math.Floor(pointInGrid.X / (width / (double)columns)), 0, columns - 1);
		int row = Math.Clamp((int)Math.Floor(pointInGrid.Y / (height / (double)rows)), 0, rows - 1);
		return Math.Clamp(row * columns + column, 0, maxIndex);
	}

	private void UpdateSlotReorderPreview(int sourceIndex, int targetIndex)
	{
		int columns = Math.Max(1, _slotGrid.Columns);
		int rows = Math.Max(1, _slotGrid.Rows);
		double cellWidth = (_slotGrid.ActualWidth > 0.0) ? _slotGrid.ActualWidth / (double)columns : 0.0;
		double cellHeight = (_slotGrid.ActualHeight > 0.0) ? _slotGrid.ActualHeight / (double)rows : 0.0;
		foreach (System.Windows.Controls.Button slotButton in _slotGrid.Children.OfType<System.Windows.Controls.Button>())
		{
			if (ReferenceEquals(slotButton, _slotDragSourceButton) || slotButton.Tag is not LauncherItem item)
			{
				continue;
			}
			int position = item.SlotIndex;
			int displayIndex = ResolveDisplayIndex(position, sourceIndex, targetIndex);
			double offsetX = (double)(displayIndex % columns - position % columns) * cellWidth;
			double offsetY = (double)(displayIndex / columns - position / columns) * cellHeight;
			AnimateSlotPush(slotButton, offsetX, offsetY);
		}
	}

	private static int ResolveDisplayIndex(int position, int sourceIndex, int targetIndex)
	{
		if (sourceIndex < targetIndex)
		{
			return (position > sourceIndex && position <= targetIndex) ? position - 1 : position;
		}
		if (sourceIndex > targetIndex)
		{
			return (position >= targetIndex && position < sourceIndex) ? position + 1 : position;
		}
		return position;
	}

	private static void AnimateSlotPush(System.Windows.Controls.Button slotButton, double offsetX, double offsetY)
	{
		if (slotButton.RenderTransform is not TranslateTransform transform)
		{
			transform = new TranslateTransform();
			slotButton.RenderTransform = transform;
			slotButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}
		TimeSpan duration = TimeSpan.FromMilliseconds(SlotReorderAnimationMilliseconds);
		DoubleAnimation animationX = new DoubleAnimation(offsetX, duration)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		DoubleAnimation animationY = new DoubleAnimation(offsetY, duration)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		transform.BeginAnimation(TranslateTransform.XProperty, animationX, HandoffBehavior.SnapshotAndReplace);
		transform.BeginAnimation(TranslateTransform.YProperty, animationY, HandoffBehavior.SnapshotAndReplace);
	}

	private void CompleteSlotDrag(bool commit)
	{
		if (!_isSlotDragActive)
		{
			return;
		}
		_isSlotDragActive = false;
		System.Windows.Controls.Button sourceButton = _slotDragSourceButton;
		LauncherItem sourceItem = _slotDragSourceItem;
		int targetIndex = _slotDragPreviewIndex;
		if (sourceButton != null)
		{
			sourceButton.LostMouseCapture -= SlotDragSourceButton_LostMouseCapture;
			if (sourceButton.IsMouseCaptured)
			{
				sourceButton.ReleaseMouseCapture();
			}
		}
		bool reordered = false;
		if (commit && sourceItem != null && targetIndex >= 0 && targetIndex != sourceItem.SlotIndex)
		{
			LauncherItem targetItem = _widgets[_selectedWidgetIndex].FirstOrDefault((LauncherItem candidate) => candidate.SlotIndex == targetIndex);
			if (targetItem != null && CanReorderSlot(sourceItem, targetItem))
			{
				reordered = MoveSlotContentsWithinSelectedWidget(sourceItem, targetItem);
			}
		}
		_slotDragSourceItem = null;
		_slotDragSourceButton = null;
		_slotDragPreviewIndex = -1;
		if (!reordered)
		{
			ResetSlotDragVisuals(sourceButton);
		}
	}

	private void ResetSlotDragVisuals(System.Windows.Controls.Button sourceButton)
	{
		foreach (System.Windows.Controls.Button slotButton in _slotGrid.Children.OfType<System.Windows.Controls.Button>())
		{
			if (!ReferenceEquals(slotButton, sourceButton))
			{
				AnimateSlotPush(slotButton, 0.0, 0.0);
			}
		}
		if (sourceButton != null)
		{
			AnimateSlotDragReturn(sourceButton);
		}
	}

	private static void AnimateSlotDragLift(System.Windows.Controls.Button button)
	{
		if (button.RenderTransform is not TransformGroup group || group.Children.Count < 1 || group.Children[0] is not ScaleTransform scale)
		{
			return;
		}
		DoubleAnimation animation = new DoubleAnimation(SlotDragLiftScale, TimeSpan.FromMilliseconds(120.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
	}

	private static void AnimateSlotDragReturn(System.Windows.Controls.Button button)
	{
		TimeSpan duration = TimeSpan.FromMilliseconds(SlotReorderAnimationMilliseconds);
		if (button.RenderTransform is TransformGroup group && group.Children.Count >= 2 && group.Children[0] is ScaleTransform scale && group.Children[1] is TranslateTransform translate)
		{
			DoubleAnimation scaleAnimation = new DoubleAnimation(1.0, duration)
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation translateX = new DoubleAnimation(0.0, duration)
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			DoubleAnimation translateY = new DoubleAnimation(0.0, duration)
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			translateY.Completed += delegate
			{
				System.Windows.Controls.Panel.SetZIndex(button, 0);
			};
			scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
			scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
			translate.BeginAnimation(TranslateTransform.XProperty, translateX);
			translate.BeginAnimation(TranslateTransform.YProperty, translateY);
		}
		else
		{
			System.Windows.Controls.Panel.SetZIndex(button, 0);
		}
	}

	private void SlotDragSourceButton_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_isSlotDragActive)
		{
			CompleteSlotDrag(commit: false);
		}
	}

	private void SlotButton_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
	{
		DragDropEffects effects = DragDropEffects.None;
		if (_isEditMode && sender is System.Windows.Controls.Button { Tag: LauncherItem item } && item.TargetCount < LauncherItem.MaxTargetsPerSlot && e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] array && array.Length != 0)
		{
			effects = (IsShortcutFile(array[0]) ? DragDropEffects.Move : DragDropEffects.Link);
		}
		else if (_isEditMode && sender is System.Windows.Controls.Button { Tag: LauncherItem item2 } && item2.TargetCount < LauncherItem.MaxTargetsPerSlot && HasVirtualShortcutDropData(e.Data))
		{
			effects = DragDropEffects.Link;
		}
		e.Effects = effects;
		e.Handled = true;
	}

	private void SlotButton_Drop(object sender, System.Windows.DragEventArgs e)
	{
		if (_isEditMode && sender is System.Windows.Controls.Button { Tag: LauncherItem tag } && e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] array && array.Length != 0)
		{
			foreach (string path in array.Take(LauncherItem.MaxTargetsPerSlot - tag.TargetCount))
			{
				SetItemTargetFromDrop(tag, path);
			}
		}
		else if (_isEditMode && sender is System.Windows.Controls.Button { Tag: LauncherItem tag2 })
		{
			TrySetItemTargetFromVirtualDrop(tag2, e.Data);
		}
		e.Handled = true;
	}

	private bool MoveSlotContentsWithinSelectedWidget(LauncherItem sourceItem, LauncherItem targetItem)
	{
		bool moved = LauncherItem.MoveContentsWithinWidget(_widgets[_selectedWidgetIndex], sourceItem.SlotIndex, targetItem.SlotIndex);
		if (!moved)
		{
			return false;
		}
		PersistLauncherState();
		RefreshSlots();
		return true;
	}

	private bool CanReorderSlot(LauncherItem sourceItem, LauncherItem targetItem)
	{
		return _isEditMode && sourceItem.WidgetIndex == targetItem.WidgetIndex && sourceItem.WidgetIndex == _selectedWidgetIndex && sourceItem.SlotIndex != targetItem.SlotIndex;
	}

	private void CreateTrayIcon()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Opening += delegate
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)BeginTrayMenuInteraction);
		};
		contextMenuStrip.Closed += delegate
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)EndTrayMenuInteraction);
		};
		_trayWidgetItems.Clear();
		_trayOpenItem = (ToolStripMenuItem)contextMenuStrip.Items.Add(AppText.Choose("런처 열기", "Open launcher"), null, delegate
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
			{
				ShowFromTray();
			});
		});
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		for (int num = 0; num < 3; num++)
		{
			int widgetIndex = num;
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem
			{
				Text = AppText.Choose(WidgetTabs[num].Name + " 열기", "Open " + WidgetTabs[num].Name)
			};
			toolStripMenuItem.Click += delegate
			{
				((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
				{
					ShowFromTray(widgetIndex);
				});
			};
			contextMenuStrip.Items.Add(toolStripMenuItem);
			_trayWidgetItems.Add(toolStripMenuItem);
		}
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		_trayExitItem = (ToolStripMenuItem)contextMenuStrip.Items.Add(AppText.Choose("종료", "Exit"), null, delegate
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)RequestExit);
		});
		_notifyIcon = new NotifyIcon
		{
			Icon = LoadTrayIcon(),
			Text = CreateTrayText(LauncherTitle),
			Visible = true,
			ContextMenuStrip = contextMenuStrip
		};
		_notifyIcon.MouseDown += delegate(object? _, System.Windows.Forms.MouseEventArgs args)
		{
			if (args.Button == MouseButtons.Right)
			{
				((DispatcherObject)this).Dispatcher.Invoke((Action)BeginTrayMenuInteraction);
			}
		};
		_notifyIcon.MouseUp += delegate(object? _, System.Windows.Forms.MouseEventArgs args)
		{
			if (args.Button == MouseButtons.Left)
			{
				((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
				{
					_lastTrayPointerUtc = DateTime.UtcNow;
					ShowFromTray();
				});
			}
		};
		_notifyIcon.MouseMove += delegate
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
			{
				ShowFromTrayHover();
			});
		};
		_notifyIcon.DoubleClick += delegate
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
			{
				ShowFromTray();
			});
		};
		UpdateTrayLabels();
		PromoteTrayIconAsync();
	}

	private async Task PromoteTrayIconAsync()
	{
		for (int attempt = 0; attempt < 6; attempt++)
		{
			await Task.Delay((attempt == 0) ? 850 : 1200);
			if (!TryPromoteTrayIconInSettings())
			{
				continue;
			}
			((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
			{
				if (_notifyIcon != null)
				{
					_notifyIcon.Visible = false;
					_notifyIcon.Visible = true;
				}
			});
			break;
		}
	}

	private bool TryPromoteTrayIconInSettings()
	{
		string processPath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(processPath))
		{
			return false;
		}
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Control Panel\\NotifyIconSettings", writable: true);
		if (registryKey == null)
		{
			return false;
		}
		bool result = false;
		string[] subKeyNames = registryKey.GetSubKeyNames();
		foreach (string name in subKeyNames)
		{
			using RegistryKey registryKey2 = registryKey.OpenSubKey(name, writable: true);
			if (registryKey2 != null)
			{
				string text = registryKey2.GetValue("ExecutablePath") as string;
				if (!string.IsNullOrWhiteSpace(text) && PathsEqual(text, processPath))
				{
					registryKey2.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
					result = true;
				}
			}
		}
		return result;
	}

	private void InstallStartupShortcut()
	{
		string processPath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
		{
			return;
		}
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
			if (!string.IsNullOrWhiteSpace(folderPath))
			{
				Directory.CreateDirectory(folderPath);
				string text = System.IO.Path.Combine(folderPath, LauncherStore.DefaultLauncherTitle + ".lnk");
				string text2 = ResolveShortcutTarget(text);
				if (!File.Exists(text) || string.IsNullOrWhiteSpace(text2) || !PathsEqual(text2, processPath))
				{
					CreateWindowsShortcut(text, processPath, System.IO.Path.GetDirectoryName(processPath), LauncherStore.DefaultLauncherTitle);
					_notifyIcon?.ShowBalloonTip(1600, LauncherStore.DefaultLauncherTitle, AppText.Choose("오른쪽 아래 시스템 아이콘에 등록되고 시작프로그램에 추가되었습니다.", "The launcher was added to the system tray and Windows startup."), ToolTipIcon.Info);
				}
			}
		}
		catch
		{
		}
	}

	private void BeginTrayMenuInteraction()
	{
		_isTrayMenuOpen = true;
		_hideAnimationGeneration++;
		_isHideAnimationRunning = false;
		_lastTrayPointerUtc = DateTime.UtcNow;
		_lastPointerSafeUtc = DateTime.UtcNow;
		_hoverHideTimer.Stop();
		if (base.IsVisible && _shell != null)
		{
			ClearShellAnimations();
			_shell.Opacity = 1.0;
			_shellScale.ScaleX = 1.0;
			_shellScale.ScaleY = 1.0;
			_shellTranslate.X = 0.0;
			_shellTranslate.Y = 0.0;
		}
	}

	private void CompleteQaShowAnimation()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("P_EDU_LAUNCHER_QA_SHOW"), "1", StringComparison.Ordinal) || _shell == null)
		{
			return;
		}
		ClearShellAnimations();
		_shell.Opacity = 1.0;
		_shellScale.ScaleX = 1.0;
		_shellScale.ScaleY = 1.0;
		_shellTranslate.X = 0.0;
		_shellTranslate.Y = 0.0;
		_hoverHideTimer.Stop();
		Activate();
	}

	private void EndTrayMenuInteraction()
	{
		_isTrayMenuOpen = false;
		_lastTrayPointerUtc = DateTime.UtcNow;
		_lastPointerSafeUtc = DateTime.UtcNow;
		if (!_isEditMode && !_isExitRequested)
		{
			_hoverHideTimer.Start();
		}
	}

	private void UpdateTrayLabels()
	{
		if (_notifyIcon != null)
		{
			_notifyIcon.Text = CreateTrayText(LauncherTitle);
		}
		if (_trayOpenItem != null)
		{
			_trayOpenItem.Text = AppText.Choose("런처 열기", "Open launcher");
		}
		if (_trayExitItem != null)
		{
			_trayExitItem.Text = AppText.Choose("종료", "Exit");
		}
		for (int i = 0; i < _trayWidgetItems.Count && i < WidgetTabs.Count; i++)
		{
			_trayWidgetItems[i].Text = AppText.Choose(WidgetTabs[i].Name + " 열기", "Open " + WidgetTabs[i].Name);
		}
	}

	private void HoverHideTimer_Tick(object? sender, EventArgs e)
	{
		if (_isEditMode || _isTrayMenuOpen || _isSettingsInteractionActive)
		{
			_lastPointerSafeUtc = DateTime.UtcNow;
		}
		else if (base.IsVisible && !_isExitRequested && !_isHideAnimationRunning)
		{
			if (IsPointerOverLauncher() || IsTrayPointerProtected())
			{
				_lastPointerSafeUtc = DateTime.UtcNow;
			}
			else if ((DateTime.UtcNow - _lastPointerSafeUtc).TotalMilliseconds >= 140.0)
			{
				BeginHideToTaskbarAnimation();
			}
		}
	}

	private bool IsPointerOverLauncher()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		if (handle == 0 || !GetWindowRect(handle, out var lpRect))
		{
			return base.IsMouseOver;
		}
		System.Drawing.Point position = System.Windows.Forms.Cursor.Position;
		if (position.X >= lpRect.Left - 4 && position.X <= lpRect.Right + 4 && position.Y >= lpRect.Top - 4)
		{
			return position.Y <= lpRect.Bottom + 4;
		}
		return false;
	}

	private bool IsTrayPointerProtected()
	{
		if (_isTrayMenuOpen)
		{
			return true;
		}
		if (IsPointerNearTrayIcon())
		{
			_lastTrayPointerUtc = DateTime.UtcNow;
			return true;
		}
		return (DateTime.UtcNow - _lastTrayPointerUtc).TotalMilliseconds < 220.0;
	}

	private static bool IsPointerNearTrayIcon()
	{
		System.Drawing.Point position = System.Windows.Forms.Cursor.Position;
		System.Drawing.Rectangle bounds = Screen.FromPoint(position).Bounds;
		System.Drawing.Rectangle workingArea = Screen.FromPoint(position).WorkingArea;
		int num = Math.Max(96, bounds.Bottom - workingArea.Bottom + 56);
		bool num2 = position.X >= bounds.Right - 560 && position.Y >= bounds.Bottom - num && position.Y <= bounds.Bottom + 16;
		bool result = position.X >= bounds.Right - 160 && position.X <= bounds.Right + 16 && position.Y >= bounds.Bottom - 560;
		if (!num2)
		{
			return result;
		}
		return true;
	}

	private void ShowFromTrayHover()
	{
		_lastTrayPointerUtc = DateTime.UtcNow;
		if (_isTrayMenuOpen)
		{
			_lastPointerSafeUtc = DateTime.UtcNow;
			return;
		}
		if (base.IsVisible && (DateTime.UtcNow - _lastTrayHoverShowUtc).TotalMilliseconds < 800.0)
		{
			_lastPointerSafeUtc = DateTime.UtcNow;
			return;
		}
		_lastTrayHoverShowUtc = DateTime.UtcNow;
		ShowFromTray();
	}

	private void ShowFromTray(int widgetIndex = -1)
	{
		_hideAnimationGeneration++;
		_isHideAnimationRunning = false;
		_lastPointerSafeUtc = DateTime.UtcNow;
		if (widgetIndex >= 0)
		{
			SelectWidget(widgetIndex);
		}
		bool num = !base.IsVisible || base.WindowState == WindowState.Minimized || _shell == null || _shell.Opacity < 0.95;
		if (!base.IsVisible)
		{
			Show();
		}
		if (base.WindowState == WindowState.Minimized)
		{
			base.WindowState = WindowState.Normal;
		}
		PositionNearTaskbar();
		Activate();
		base.Topmost = true;
		if (!_isEditMode)
		{
			base.Topmost = false;
		}
		if (num)
		{
			BeginBubbleShowAnimation(fromTray: true);
		}
		if (_isEditMode)
		{
			_hoverHideTimer.Stop();
		}
		else
		{
			_hoverHideTimer.Start();
		}
	}

	private void PositionNearTaskbar()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Left = Math.Max(workArea.Left + 18.0, workArea.Right - 720.0 - 6.0);
		base.Top = Math.Max(workArea.Top + 18.0, workArea.Bottom - 520.0 - 2.0);
	}

	private void BeginBubbleShowAnimation(bool fromTray = false)
	{
		ClearShellAnimations();
		_shell.Opacity = 0.0;
		_shellScale.ScaleX = 0.86;
		_shellScale.ScaleY = 0.86;
		_shellTranslate.X = 0.0;
		_shellTranslate.Y = 42.0;
		_shell.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(0.0, 1.0, 210, new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		}));
		BackEase easingFunction = new BackEase
		{
			Amplitude = 0.32,
			EasingMode = EasingMode.EaseOut
		};
		_shellScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(0.86, 1.0, 500, easingFunction));
		_shellScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(0.86, 1.0, 500, easingFunction));
		_shellTranslate.BeginAnimation(TranslateTransform.YProperty, CreateAnimation(42.0, 0.0, 500, easingFunction));
		BeginTrayConnectorAnimation(fromTray);
		SpawnBubbles(hide: false);
	}

	private void BeginTrayConnectorAnimation(bool fromTray)
	{
		ClearTrayConnectorAnimations();
		if (_trayConnector != null)
		{
			if (!fromTray)
			{
				_trayConnector.Opacity = 1.0;
				_trayConnectorScale.ScaleX = 1.0;
				_trayConnectorScale.ScaleY = 1.0;
				_trayConnectorTranslate.X = 0.0;
				_trayConnectorTranslate.Y = 0.0;
				return;
			}
			_trayConnector.Opacity = 0.0;
			_trayConnectorScale.ScaleX = 0.12;
			_trayConnectorScale.ScaleY = 0.12;
			_trayConnectorTranslate.X = 126.0;
			_trayConnectorTranslate.Y = 150.0;
			BackEase easingFunction = new BackEase
			{
				Amplitude = 0.42,
				EasingMode = EasingMode.EaseOut
			};
			_trayConnector.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(0.0, 1.0, 220, new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}));
			_trayConnectorScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(0.12, 1.0, 620, easingFunction, 20));
			_trayConnectorScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(0.12, 1.0, 620, easingFunction, 20));
			_trayConnectorTranslate.BeginAnimation(TranslateTransform.XProperty, CreateAnimation(126.0, 0.0, 620, easingFunction, 20));
			_trayConnectorTranslate.BeginAnimation(TranslateTransform.YProperty, CreateAnimation(150.0, 0.0, 620, easingFunction, 20));
		}
	}

	private async void BeginHideToTaskbarAnimation()
	{
		if (!_isSettingsInteractionActive && !_isHideAnimationRunning && base.IsVisible)
		{
			int animationGeneration = ++_hideAnimationGeneration;
			_isHideAnimationRunning = true;
			ClearShellAnimations();
			QuinticEase easingFunction = new QuinticEase
			{
				EasingMode = EasingMode.EaseIn
			};
			_shell.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(1.0, 0.0, 130, easingFunction));
			_shellScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(1.0, 0.18, 130, easingFunction));
			_shellScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(1.0, 0.18, 130, easingFunction));
			_shellTranslate.BeginAnimation(TranslateTransform.XProperty, CreateAnimation(0.0, 110.0, 130, easingFunction));
			_shellTranslate.BeginAnimation(TranslateTransform.YProperty, CreateAnimation(0.0, 170.0, 130, easingFunction));
			BeginTrayConnectorHideAnimation(easingFunction);
			SpawnBubbles(hide: true);
			await Task.Delay(150);
			if (animationGeneration == _hideAnimationGeneration && !_isExitRequested)
			{
				Hide();
				ClearShellAnimations();
				_shell.Opacity = 0.0;
				_shellScale.ScaleX = 0.86;
				_shellScale.ScaleY = 0.86;
				_shellTranslate.X = 0.0;
				_shellTranslate.Y = 42.0;
				ResetTrayConnector();
				_bubbleCanvas.Children.Clear();
				_isHideAnimationRunning = false;
			}
		}
	}

	private void BeginTrayConnectorHideAnimation(IEasingFunction easingFunction)
	{
		if (_trayConnector != null)
		{
			_trayConnector.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(_trayConnector.Opacity, 0.0, 90, easingFunction));
			_trayConnectorScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(_trayConnectorScale.ScaleX, 0.18, 130, easingFunction));
			_trayConnectorScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(_trayConnectorScale.ScaleY, 0.18, 130, easingFunction));
			_trayConnectorTranslate.BeginAnimation(TranslateTransform.XProperty, CreateAnimation(_trayConnectorTranslate.X, 132.0, 130, easingFunction));
			_trayConnectorTranslate.BeginAnimation(TranslateTransform.YProperty, CreateAnimation(_trayConnectorTranslate.Y, 250.0, 130, easingFunction));
		}
	}

	private void ClearShellAnimations()
	{
		_shell.BeginAnimation(UIElement.OpacityProperty, null);
		_shellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		_shellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		_shellTranslate.BeginAnimation(TranslateTransform.XProperty, null);
		_shellTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		ClearTrayConnectorAnimations();
	}

	private void ClearTrayConnectorAnimations()
	{
		if (_trayConnector != null)
		{
			_trayConnector.BeginAnimation(UIElement.OpacityProperty, null);
			_trayConnectorScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			_trayConnectorScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			_trayConnectorTranslate.BeginAnimation(TranslateTransform.XProperty, null);
			_trayConnectorTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		}
	}

	private void ResetTrayConnector()
	{
		if (_trayConnector != null)
		{
			ClearTrayConnectorAnimations();
			_trayConnector.Opacity = 0.0;
			_trayConnectorScale.ScaleX = 0.12;
			_trayConnectorScale.ScaleY = 0.12;
			_trayConnectorTranslate.X = 126.0;
			_trayConnectorTranslate.Y = 150.0;
		}
	}

	private void SpawnBubbles(bool hide)
	{
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		int animationGeneration = ++_bubbleAnimationGeneration;
		_bubbleCanvas.Children.Clear();
		double num = ((_bubbleCanvas.ActualWidth > 0.0) ? _bubbleCanvas.ActualWidth : 696.0);
		double num2 = ((_bubbleCanvas.ActualHeight > 0.0) ? _bubbleCanvas.ActualHeight : 496.0);
		int longestAnimation = 0;
		System.Windows.Media.Color[] array = new System.Windows.Media.Color[4]
		{
			System.Windows.Media.Color.FromRgb(224, 242, 254),
			System.Windows.Media.Color.FromRgb(96, 165, 250),
			System.Windows.Media.Color.FromRgb(124, 58, 237),
			System.Windows.Media.Color.FromRgb(192, 132, 252)
		};
		int num3 = (hide ? 24 : 18);
		for (int i = 0; i < num3; i++)
		{
			double num4 = 8.0 + _random.NextDouble() * 20.0;
			System.Windows.Media.Color color = array[_random.Next(array.Length)];
			ScaleTransform scaleTransform = new ScaleTransform(hide ? 1.0 : 0.25, hide ? 1.0 : 0.25);
			Ellipse ellipse = new Ellipse
			{
				Width = num4,
				Height = num4,
				Fill = new SolidColorBrush(color)
				{
					Opacity = (hide ? 0.28 : 0.2)
				},
				Stroke = new SolidColorBrush(Colors.White)
				{
					Opacity = 0.46
				},
				StrokeThickness = 1.15,
				Opacity = (hide ? 0.78 : 0.0),
				RenderTransform = scaleTransform,
				RenderTransformOrigin = new Point(0.5, 0.5)
			};
			double num5 = 45.0 + _random.NextDouble() * Math.Max(80.0, num - 90.0);
			double num6 = (hide ? (70.0 + _random.NextDouble() * Math.Max(100.0, num2 - 150.0)) : (num2 - 48.0 - _random.NextDouble() * 78.0));
			double to = (hide ? (num - 56.0 + _random.NextDouble() * 30.0) : num5);
			double to2 = (hide ? (num2 + 26.0 + _random.NextDouble() * 18.0) : (num6 - (105.0 + _random.NextDouble() * 210.0)));
			int num7 = _random.Next(0, hide ? 110 : 150);
			int num8 = (hide ? (360 + _random.Next(0, 210)) : (560 + _random.Next(0, 370)));
			longestAnimation = Math.Max(longestAnimation, num7 + num8);
			Canvas.SetLeft(ellipse, num5);
			Canvas.SetTop(ellipse, num6);
			_bubbleCanvas.Children.Add(ellipse);
			ellipse.BeginAnimation(Canvas.LeftProperty, CreateAnimation(num5, to, num8, new CubicEase
			{
				EasingMode = ((!hide) ? EasingMode.EaseOut : EasingMode.EaseIn)
			}, num7));
			ellipse.BeginAnimation(Canvas.TopProperty, CreateAnimation(num6, to2, num8, new CubicEase
			{
				EasingMode = ((!hide) ? EasingMode.EaseOut : EasingMode.EaseIn)
			}, num7));
			ellipse.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(hide ? 0.78 : 0.72, 0.0, num8, new QuadraticEase
			{
				EasingMode = EasingMode.EaseIn
			}, num7));
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(hide ? 1.0 : 0.25, hide ? 0.08 : 1.25, num8, new QuadraticEase
			{
				EasingMode = ((!hide) ? EasingMode.EaseOut : EasingMode.EaseIn)
			}, num7));
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(hide ? 1.0 : 0.25, hide ? 0.08 : 1.25, num8, new QuadraticEase
			{
				EasingMode = ((!hide) ? EasingMode.EaseOut : EasingMode.EaseIn)
			}, num7));
		}
		((DispatcherObject)this).Dispatcher.InvokeAsync<Task>((Func<Task>)async delegate
		{
			await Task.Delay(longestAnimation + 100);
			if (animationGeneration == _bubbleAnimationGeneration)
			{
				_bubbleCanvas.Children.Clear();
			}
		});
	}

	private static DoubleAnimation CreateAnimation(double from, double to, int milliseconds, IEasingFunction? easingFunction, int beginTimeMilliseconds = 0)
	{
		return new DoubleAnimation
		{
			From = from,
			To = to,
			Duration = TimeSpan.FromMilliseconds(milliseconds, 0L),
			BeginTime = TimeSpan.FromMilliseconds(beginTimeMilliseconds, 0L),
			EasingFunction = easingFunction,
			FillBehavior = FillBehavior.HoldEnd
		};
	}

	private void TryDragWindow(MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Left || IsWindowDragBlocked(e.OriginalSource as DependencyObject))
		{
			return;
		}
		try
		{
			DragMove();
			e.Handled = true;
		}
		catch (InvalidOperationException)
		{
		}
	}

	private static bool IsWindowDragBlocked(DependencyObject? source)
	{
		if (FindAncestor<System.Windows.Controls.Button>(source) == null && FindAncestor<System.Windows.Controls.TextBox>(source) == null && FindAncestor<ContextMenu>(source) == null)
		{
			return FindAncestor<MenuItem>(source) != null;
		}
		return true;
	}

	private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
	{
		while (start != null)
		{
			T val = (T)(object)((start is T) ? start : null);
			if (val != null)
			{
				return val;
			}
			start = VisualTreeHelper.GetParent(start);
		}
		return default(T);
	}

	private void RequestExit()
	{
		_isExitRequested = true;
		Close();
	}

	private void Window_StateChanged(object? sender, EventArgs e)
	{
		if (base.WindowState == WindowState.Minimized)
		{
			_wasMinimized = true;
		}
		else if (_wasMinimized && base.WindowState == WindowState.Normal)
		{
			_wasMinimized = false;
			BeginBubbleShowAnimation();
		}
	}

	private void Window_Closing(object? sender, CancelEventArgs e)
	{
		PersistLauncherState();
		if (!_isExitRequested)
		{
			e.Cancel = true;
			BeginHideToTaskbarAnimation();
		}
		else
		{
			_notifyIcon?.Dispose();
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		PersistLauncherState();
		_notifyIcon?.Dispose();
		base.OnClosed(e);
	}

	private static DrawingIcon LoadTrayIcon()
	{
		string processPath = Environment.ProcessPath;
		if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
		{
			DrawingIcon icon = DrawingIcon.ExtractAssociatedIcon(processPath);
			if (icon != null)
			{
				return icon;
			}
		}
		return DrawingSystemIcons.Application;
	}

	private static string CreateTrayText(string text)
	{
		string text2 = LauncherStore.NormalizeLauncherTitle(text);
		if (text2.Length > 63)
		{
			return text2.Substring(0, 63);
		}
		return text2;
	}

	private static string? ResolveShortcutTarget(string shortcutPath)
	{
		if (!File.Exists(shortcutPath) || !string.Equals(System.IO.Path.GetExtension(shortcutPath), ".lnk", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		object obj = null;
		object obj2 = null;
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			if (typeFromProgID == null)
			{
				return null;
			}
			obj = Activator.CreateInstance(typeFromProgID);
			if (obj == null)
			{
				return null;
			}
			obj2 = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, obj, new object[1] { shortcutPath });
			return obj2?.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, obj2, null) as string;
		}
		catch
		{
			return null;
		}
		finally
		{
			ReleaseComObject(obj2);
			ReleaseComObject(obj);
		}
	}

	private static string? ResolveShortcutArguments(string shortcutPath)
	{
		if (!File.Exists(shortcutPath) || !string.Equals(System.IO.Path.GetExtension(shortcutPath), ".lnk", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		object obj = null;
		object obj2 = null;
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			if (typeFromProgID == null)
			{
				return null;
			}
			obj = Activator.CreateInstance(typeFromProgID);
			if (obj == null)
			{
				return null;
			}
			obj2 = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, obj, new object[1] { shortcutPath });
			return obj2?.GetType().InvokeMember("Arguments", BindingFlags.GetProperty, null, obj2, null) as string;
		}
		catch
		{
			return null;
		}
		finally
		{
			ReleaseComObject(obj2);
			ReleaseComObject(obj);
		}
	}

	private static void CreateWindowsShortcut(string shortcutPath, string targetPath, string? workingDirectory, string description, string? arguments = null, string? iconLocation = null)
	{
		object obj = null;
		object obj2 = null;
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			if (typeFromProgID == null)
			{
				return;
			}
			obj = Activator.CreateInstance(typeFromProgID);
			if (obj == null)
			{
				return;
			}
			obj2 = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, obj, new object[1] { shortcutPath });
			if (obj2 != null)
			{
				Type type = obj2.GetType();
				type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, obj2, new object[1] { targetPath });
				if (!string.IsNullOrWhiteSpace(workingDirectory))
				{
					type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, obj2, new object[1] { workingDirectory });
				}
				if (!string.IsNullOrWhiteSpace(arguments))
				{
					type.InvokeMember("Arguments", BindingFlags.SetProperty, null, obj2, new object[1] { arguments });
				}
				type.InvokeMember("Description", BindingFlags.SetProperty, null, obj2, new object[1] { description });
				type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, obj2, new object[1] { string.IsNullOrWhiteSpace(iconLocation) ? (targetPath + ",0") : iconLocation });
				type.InvokeMember("Save", BindingFlags.InvokeMethod, null, obj2, null);
			}
		}
		finally
		{
			ReleaseComObject(obj2);
			ReleaseComObject(obj);
		}
	}

	private static void ReleaseComObject(object? instance)
	{
		if (instance != null && Marshal.IsComObject(instance))
		{
			Marshal.FinalReleaseComObject(instance);
		}
	}

	[DllImport("shell32.dll")]
	private static extern nint ILCombine(nint pidl1, nint pidl2);

	[DllImport("shell32.dll")]
	private static extern void ILFree(nint pidl);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHGetNameFromIDList(nint pidl, uint sigdnName, out nint ppszName);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern nint SHGetFileInfo(nint pszPath, uint dwFileAttributes, ref ShellFileInfo psfi, uint cbFileInfo, uint uFlags);

	[DllImport("ole32.dll")]
	private static extern void CoTaskMemFree(nint pv);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyIcon(nint hIcon);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern int GetWindowTextLength(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out NativeRect lpRect);

	private static System.Windows.Shapes.Rectangle CreateRibbon(double angle, double height, Thickness margin, double opacity)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		return new System.Windows.Shapes.Rectangle
		{
			Height = height,
			Margin = margin,
			VerticalAlignment = VerticalAlignment.Top,
			Fill = CreateRibbonBrush(),
			Opacity = opacity,
			RenderTransformOrigin = new Point(0.5, 0.5),
			RenderTransform = new RotateTransform(angle)
		};
	}

	private static System.Windows.Controls.Button CreateChromeButton(string text, double width, double height)
	{
		return new System.Windows.Controls.Button
		{
			Width = width,
			Height = height,
			Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
			Content = text,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(36, 44, 52, 72)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(102, 124, 142, 168)),
			BorderThickness = new Thickness(1.0),
			Cursor = System.Windows.Input.Cursors.Hand,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = ((text.Length > 2) ? 13 : 15),
			FontWeight = ((text.Length > 2) ? FontWeights.SemiBold : FontWeights.Normal)
		};
	}

	private static System.Windows.Controls.Button CreateHeaderActionButton(string text, double width, double height)
	{
		return new System.Windows.Controls.Button
		{
			Width = width,
			Height = height,
			Content = text,
			Padding = new Thickness(6.0, 0.0, 6.0, 0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 37, 99, 235), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 14, 165, 233), 0.58),
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 45, 212, 191), 1.0)
				}
			},
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(215, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			Template = CreateRoundedButtonTemplate(),
			Cursor = System.Windows.Input.Cursors.Hand,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = (width <= RenameButtonWidth ? 10.5 : 14.0),
			FontWeight = FontWeights.Bold,
			Effect = new DropShadowEffect
			{
				BlurRadius = 14.0,
				ShadowDepth = 0.0,
				Opacity = 0.62,
				Color = System.Windows.Media.Color.FromRgb(56, 189, 248)
			}
		};
	}

	private static System.Windows.Controls.ControlTemplate CreateRoundedButtonTemplate()
	{
		FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10.0));
		border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
		border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
		presenter.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		presenter.SetValue(System.Windows.Controls.ContentPresenter.RecognizesAccessKeyProperty, true);
		presenter.SetBinding(System.Windows.Controls.ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
		border.AppendChild(presenter);
		return new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button))
		{
			VisualTree = border
		};
	}

	private static System.Windows.Controls.ControlTemplate CreateContentOnlyButtonTemplate()
	{
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
		presenter.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		presenter.SetBinding(System.Windows.Controls.ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
		return new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button))
		{
			VisualTree = presenter
		};
	}

	private System.Windows.Controls.Button CreateSettingsButton()
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = CreateSettingsGearContent(),
			Width = SettingsButtonWidth,
			Height = SettingsButtonHeight,
			Margin = SettingsButtonMargin,
			Padding = new Thickness(0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Template = CreateContentOnlyButtonTemplate(),
			Cursor = System.Windows.Input.Cursors.Hand,
			Effect = new DropShadowEffect
			{
				BlurRadius = 20.0,
				ShadowDepth = 0.0,
				Opacity = 0.36,
				Color = System.Windows.Media.Color.FromRgb(112, 52, 159)
			}
		};
		System.Windows.Automation.AutomationProperties.SetName(button, AppText.Choose("설정", "Settings"));
		System.Windows.Automation.AutomationProperties.SetAutomationId(button, "settings-button");
		button.Click += delegate
		{
			ShowSettingsDialog();
		};
		return button;
	}

	private static UIElement CreateSettingsGearContent()
	{
		Grid gear = new Grid
		{
			Width = SettingsButtonWidth,
			Height = SettingsButtonHeight,
			ClipToBounds = false
		};
		Border badge = new Border
		{
			Width = 58.0,
			Height = 46.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CornerRadius = new CornerRadius(18.0),
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(246, 39, 47, 66), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(244, 54, 65, 83), 0.48),
					new GradientStop(System.Windows.Media.Color.FromArgb(238, 112, 52, 159), 1.0)
				}
			},
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 153, 164, 255)),
			BorderThickness = new Thickness(1.1),
			Effect = new DropShadowEffect
			{
				BlurRadius = 14.0,
				ShadowDepth = 0.0,
				Opacity = 0.34,
				Color = System.Windows.Media.Color.FromRgb(54, 51, 99)
			}
		};
		Border label = new Border
		{
			Width = 48.0,
			Height = 18.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CornerRadius = new CornerRadius(9.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(224, 26, 30, 47)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(96, 223, 234, 255)),
			BorderThickness = new Thickness(0.75),
			Child = new TextBlock
			{
				Text = AppText.Choose("설정", "Settings"),
				Foreground = System.Windows.Media.Brushes.White,
				FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
				FontSize = AppText.IsEnglish ? 10.0 : 11.0,
				FontWeight = FontWeights.Black,
				TextAlignment = TextAlignment.Center,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		gear.Children.Add(badge);
		gear.Children.Add(label);
		return gear;
	}

	private void ShowSettingsDialog()
	{
		BeginSettingsInteraction();
		ShowAppWindowForSettingsOperation();
		Window dialog = new Window
		{
			Owner = this,
			Title = AppText.Choose("설정", "Settings"),
			Width = SettingsDialogWidth,
			Height = SettingsDialogHeight,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			ResizeMode = ResizeMode.NoResize,
			WindowStyle = WindowStyle.None,
			AllowsTransparency = true,
			Background = System.Windows.Media.Brushes.Transparent,
			ShowInTaskbar = false
		};
		TextBlock statusText = new TextBlock
		{
			Text = string.Empty,
			Visibility = Visibility.Collapsed,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 224, 242, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 11.0,
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		StackPanel actions = new StackPanel
		{
			Margin = new Thickness(22.0, 12.0, 22.0, 0.0)
		};
		TextBlock languageLabel = new TextBlock
		{
			Text = AppText.Choose("언어", "Language"),
			Margin = new Thickness(2.0, 0.0, 0.0, 7.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(224, 191, 219, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 12.0,
			FontWeight = FontWeights.Bold
		};
		System.Windows.Controls.Primitives.UniformGrid languageRow = new System.Windows.Controls.Primitives.UniformGrid
		{
			Columns = 2,
			Rows = 1
		};
		System.Windows.Controls.Button koreanButton = CreateLanguageChoiceButton("한글", UiLanguage.Korean);
		koreanButton.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
		koreanButton.Click += delegate
		{
			if (ChangeLanguage(UiLanguage.Korean))
			{
				dialog.Close();
				base.Dispatcher.BeginInvoke(new Action(ShowSettingsDialog), System.Windows.Threading.DispatcherPriority.Background);
			}
		};
		System.Windows.Controls.Button englishButton = CreateLanguageChoiceButton("English", UiLanguage.English);
		englishButton.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
		englishButton.Click += delegate
		{
			if (ChangeLanguage(UiLanguage.English))
			{
				dialog.Close();
				base.Dispatcher.BeginInvoke(new Action(ShowSettingsDialog), System.Windows.Threading.DispatcherPriority.Background);
			}
		};
		languageRow.Children.Add(koreanButton);
		languageRow.Children.Add(englishButton);
		actions.Children.Add(languageLabel);
		actions.Children.Add(languageRow);
		System.Windows.Controls.Button saveButton = CreateSettingsDialogActionButton(AppText.Choose("현재 상태 저장", "Save Current Layout"));
		saveButton.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		saveButton.Click += delegate
		{
			string snapshotFile = ShowSettingsFileDialogWithAppVisible(dialog, ShowSaveSettingsFileDialog);
			if (snapshotFile == null)
			{
				statusText.Text = AppText.Choose("저장 취소", "Save canceled");
				statusText.Visibility = Visibility.Visible;
				RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
				return;
			}
			try
			{
				string savedFile = SaveCurrentSettingsSnapshot(snapshotFile);
				statusText.Text = AppText.Choose("저장 완료: ", "Saved: ") + System.IO.Path.GetFileName(savedFile);
				statusText.Visibility = Visibility.Visible;
				CloseSettingsDialogAfterSuccessfulOperation(dialog);
			}
			catch (Exception)
			{
				statusText.Text = AppText.Choose("저장할 수 없습니다", "Could not save");
				statusText.Visibility = Visibility.Visible;
				RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
			}
		};
		System.Windows.Controls.Button loadButton = CreateSettingsDialogActionButton(AppText.Choose("저장된 설정 불러오기", "Load Saved Layout"));
		loadButton.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		loadButton.Click += delegate
		{
			string snapshotFile = ShowSettingsFileDialogWithAppVisible(dialog, ShowOpenSettingsFileDialog);
			if (snapshotFile == null)
			{
				statusText.Text = AppText.Choose("불러오기 취소", "Load canceled");
				statusText.Visibility = Visibility.Visible;
				RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
				return;
			}
			try
			{
				LoadSavedSettingsSnapshot(snapshotFile);
				statusText.Text = AppText.Choose("불러오기 완료: ", "Loaded: ") + System.IO.Path.GetFileName(snapshotFile);
				statusText.Visibility = Visibility.Visible;
				CloseSettingsDialogAfterSuccessfulOperation(dialog);
			}
			catch (Exception)
			{
				statusText.Text = AppText.Choose("불러올 수 없습니다", "Could not load");
				statusText.Visibility = Visibility.Visible;
				RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
			}
		};
		System.Windows.Controls.Button resetButton = CreateSettingsDialogActionButton(AppText.Choose("초기화", "Reset All"));
		resetButton.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		resetButton.Click += delegate
		{
			MessageBoxResult confirmation = System.Windows.MessageBox.Show(this, AppText.Choose("런처를 처음 상태로 초기화할까요?\n모든 슬롯과 제목, 위젯 이름이 기본값으로 되돌아갑니다.", "Reset the launcher to its initial state?\nAll slots, the title, and widget names will return to their defaults."), AppText.Choose("초기화", "Reset All"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (confirmation != MessageBoxResult.Yes)
			{
				statusText.Text = AppText.Choose("초기화 취소", "Reset canceled");
				statusText.Visibility = Visibility.Visible;
				RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
				return;
			}
			try
			{
				ResetLauncherStateToDefaults();
				statusText.Text = AppText.Choose("초기화 완료", "Reset complete");
				statusText.Visibility = Visibility.Visible;
				CloseSettingsDialogAfterSuccessfulOperation(dialog);
			}
			catch (Exception)
			{
				statusText.Text = AppText.Choose("초기화할 수 없습니다", "Could not reset");
				statusText.Visibility = Visibility.Visible;
				RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
			}
		};
		actions.Children.Add(saveButton);
		actions.Children.Add(loadButton);
		actions.Children.Add(resetButton);
		TextBlock widgetResetLabel = new TextBlock
		{
			Text = AppText.Choose("위젯별 초기화", "Reset Individual Widgets"),
			Margin = new Thickness(2.0, 14.0, 0.0, 8.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(224, 191, 219, 254)),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 12.0,
			FontWeight = FontWeights.Bold
		};
		actions.Children.Add(widgetResetLabel);
		System.Windows.Controls.Primitives.UniformGrid widgetResetRow = new System.Windows.Controls.Primitives.UniformGrid
		{
			Columns = 3,
			Rows = 1
		};
		for (int widgetIndex = 0; widgetIndex < WidgetTabs.Count; widgetIndex++)
		{
			int targetWidgetIndex = widgetIndex;
			System.Windows.Controls.Button widgetResetButton = CreateWidgetResetButton(AppText.DefaultWidgetName(widgetIndex));
			widgetResetButton.Margin = new Thickness(widgetIndex == 0 ? 0.0 : 4.0, 0.0, widgetIndex == WidgetTabs.Count - 1 ? 0.0 : 4.0, 0.0);
			widgetResetButton.Click += delegate
			{
				MessageBoxResult confirmation = System.Windows.MessageBox.Show(this, AppText.Choose($"위젯 {targetWidgetIndex + 1}을(를) 처음 상태로 초기화할까요?\n이 위젯의 모든 슬롯과 이름이 기본값으로 되돌아갑니다.", $"Reset Widget {targetWidgetIndex + 1} to its initial state?\nAll slots and the name in this widget will return to their defaults."), AppText.Choose($"위젯 {targetWidgetIndex + 1} 초기화", $"Reset Widget {targetWidgetIndex + 1}"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
				if (confirmation != MessageBoxResult.Yes)
				{
					statusText.Text = AppText.Choose($"위젯 {targetWidgetIndex + 1} 초기화 취소", $"Widget {targetWidgetIndex + 1} reset canceled");
					statusText.Visibility = Visibility.Visible;
					RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
					return;
				}
				try
				{
					ResetWidgetToDefaults(targetWidgetIndex);
					statusText.Text = AppText.Choose($"위젯 {targetWidgetIndex + 1} 초기화 완료", $"Widget {targetWidgetIndex + 1} reset complete");
					statusText.Visibility = Visibility.Visible;
				}
				catch (Exception)
				{
					statusText.Text = AppText.Choose($"위젯 {targetWidgetIndex + 1} 초기화할 수 없습니다", $"Could not reset Widget {targetWidgetIndex + 1}");
					statusText.Visibility = Visibility.Visible;
					RestoreSettingsDialogAfterCanceledOrFailedOperation(dialog);
				}
			};
			widgetResetRow.Children.Add(widgetResetButton);
		}
		actions.Children.Add(widgetResetRow);
		Border chrome = new Border
		{
			CornerRadius = new CornerRadius(18.0),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 186, 230, 253)),
			BorderThickness = new Thickness(1.2),
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(248, 8, 15, 31), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(246, 15, 23, 42), 0.48),
					new GradientStop(System.Windows.Media.Color.FromArgb(246, 6, 38, 55), 1.0)
				}
			},
			Effect = new DropShadowEffect
			{
				BlurRadius = 30.0,
				ShadowDepth = 0.0,
				Opacity = 0.72,
				Color = System.Windows.Media.Color.FromRgb(8, 13, 31)
			},
			Child = new Grid
			{
				Children =
				{
					new StackPanel
					{
						Margin = new Thickness(18.0, 16.0, 18.0, 16.0),
						Children =
						{
							new TextBlock
							{
								Text = AppText.Choose("설정", "Settings"),
								Foreground = System.Windows.Media.Brushes.White,
								FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
								FontSize = 20.0,
								FontWeight = FontWeights.Black,
								TextAlignment = TextAlignment.Center
							},
							actions,
							statusText
						}
					},
					CreateSettingsDialogCloseButton(dialog)
				}
			}
		};
		dialog.Content = chrome;
		dialog.Closed += delegate
		{
			EndSettingsInteraction();
		};
		dialog.KeyDown += delegate(object _, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				dialog.Close();
			}
		};
		dialog.ShowDialog();
	}

	private void BeginSettingsInteraction()
	{
		_isSettingsInteractionActive = true;
		_lastPointerSafeUtc = DateTime.UtcNow;
	}

	private void EndSettingsInteraction()
	{
		_isSettingsInteractionActive = false;
		_lastPointerSafeUtc = DateTime.UtcNow;
		if (!_isEditMode && !_isExitRequested)
		{
			_hoverHideTimer.Start();
		}
	}

	private static System.Windows.Controls.Button CreateLanguageChoiceButton(string text, UiLanguage language)
	{
		bool selected = AppText.CurrentLanguage == language;
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = text,
			Height = 40.0,
			Padding = new Thickness(12.0, 0.0, 12.0, 0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = selected
				? new LinearGradientBrush
				{
					StartPoint = new Point(0.0, 0.0),
					EndPoint = new Point(1.0, 1.0),
					GradientStops =
					{
						new GradientStop(System.Windows.Media.Color.FromArgb(246, 37, 99, 235), 0.0),
						new GradientStop(System.Windows.Media.Color.FromArgb(246, 6, 182, 212), 0.56),
						new GradientStop(System.Windows.Media.Color.FromArgb(246, 45, 212, 191), 1.0)
					}
				}
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(96, 30, 41, 59)),
			BorderBrush = selected
				? new SolidColorBrush(System.Windows.Media.Color.FromArgb(245, 224, 242, 254))
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(132, 148, 163, 184)),
			BorderThickness = new Thickness(selected ? 1.5 : 1.0),
			Template = CreateRoundedButtonTemplate(),
			Cursor = System.Windows.Input.Cursors.Hand,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 13.0,
			FontWeight = FontWeights.Bold,
			Effect = selected
				? new DropShadowEffect
				{
					BlurRadius = 16.0,
					ShadowDepth = 0.0,
					Opacity = 0.58,
					Color = System.Windows.Media.Color.FromRgb(56, 189, 248)
				}
				: null
		};
		System.Windows.Automation.AutomationProperties.SetName(button, text);
		System.Windows.Automation.AutomationProperties.SetAutomationId(button, language == UiLanguage.Korean ? "language-ko" : "language-en");
		return button;
	}

	private static System.Windows.Controls.Button CreateSettingsDialogActionButton(string text)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = text,
			Height = 48.0,
			Padding = new Thickness(16.0, 0.0, 16.0, 0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 37, 99, 235), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 14, 165, 233), 0.55),
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 45, 212, 191), 1.0)
				}
			},
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			Cursor = System.Windows.Input.Cursors.Hand,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 14.0,
			FontWeight = FontWeights.Bold,
			Effect = new DropShadowEffect
			{
				BlurRadius = 16.0,
				ShadowDepth = 0.0,
				Opacity = 0.44,
				Color = System.Windows.Media.Color.FromRgb(14, 165, 233)
			}
		};
		System.Windows.Automation.AutomationProperties.SetName(button, text);
		return button;
	}

	private static System.Windows.Controls.Button CreateWidgetResetButton(string text)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = text,
			Height = 42.0,
			Padding = new Thickness(2.0, 0.0, 2.0, 0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops =
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 124, 58, 237), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 99, 102, 241), 0.55),
					new GradientStop(System.Windows.Media.Color.FromArgb(236, 56, 189, 248), 1.0)
				}
			},
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			Cursor = System.Windows.Input.Cursors.Hand,
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 13.0,
			FontWeight = FontWeights.Bold,
			Effect = new DropShadowEffect
			{
				BlurRadius = 12.0,
				ShadowDepth = 0.0,
				Opacity = 0.42,
				Color = System.Windows.Media.Color.FromRgb(124, 58, 237)
			}
		};
		System.Windows.Automation.AutomationProperties.SetName(button, text);
		return button;
	}

	private static System.Windows.Controls.Button CreateSettingsDialogCloseButton(Window dialog)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = "✕",
			Width = 30.0,
			Height = 30.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 8.0, 8.0, 0.0),
			Padding = new Thickness(0.0),
			Foreground = System.Windows.Media.Brushes.White,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 148, 163, 184)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 186, 230, 253)),
			BorderThickness = new Thickness(1.0),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			FontSize = 13.0,
			FontWeight = FontWeights.Bold,
			Cursor = System.Windows.Input.Cursors.Hand,
			ToolTip = AppText.Choose("닫기", "Close")
		};
		System.Windows.Automation.AutomationProperties.SetName(button, AppText.Choose("닫기", "Close"));
		System.Windows.Automation.AutomationProperties.SetAutomationId(button, "settings-close");
		button.Click += delegate
		{
			dialog.Close();
		};
		return button;
	}

	private string ShowSettingsFileDialogWithAppVisible(Window settingsDialog, Func<string> showFileDialog)
	{
		ShowAppWindowForSettingsOperation();
		if (settingsDialog.IsLoaded)
		{
			settingsDialog.Activate();
		}
		return showFileDialog();
	}

	private void ShowAppWindowForSettingsOperation()
	{
		Show();
		if (WindowState == WindowState.Minimized)
		{
			WindowState = WindowState.Normal;
		}
		Activate();
	}

	private static void RestoreSettingsDialogAfterCanceledOrFailedOperation(Window settingsDialog)
	{
		// The settings dialog is never hidden during a file operation, so it only needs to regain
		// focus after the file dialog closes. Never call Show() here: if the dialog has already been
		// closed, Show() throws InvalidOperationException.
		if (settingsDialog != null && settingsDialog.IsVisible)
		{
			settingsDialog.Activate();
		}
	}

	private static void CloseSettingsDialogAfterSuccessfulOperation(Window dialog)
	{
		if (dialog.IsLoaded)
		{
			dialog.Close();
		}
	}

	private string ShowSaveSettingsFileDialog()
	{
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = AppText.Choose("현재 상태 저장", "Save Current Layout"),
			InitialDirectory = LauncherStore.GetSavedSettingsDirectory(GetAppTopLevelDirectory()),
			FileName = "launcher-items.json",
			DefaultExt = ".json",
			Filter = AppText.Choose("JSON 설정 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "JSON layout files (*.json)|*.json|All files (*.*)|*.*"),
			AddExtension = true,
			OverwritePrompt = true
		};
		return saveFileDialog.ShowDialog(this) == true ? saveFileDialog.FileName : null;
	}

	private string ShowOpenSettingsFileDialog()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = AppText.Choose("저장된 설정 불러오기", "Load Saved Layout"),
			InitialDirectory = LauncherStore.GetSavedSettingsDirectory(GetAppTopLevelDirectory()),
			FileName = "launcher-items.json",
			DefaultExt = ".json",
			Filter = AppText.Choose("JSON 설정 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "JSON layout files (*.json)|*.json|All files (*.*)|*.*"),
			CheckFileExists = true,
			Multiselect = false
		};
		return openFileDialog.ShowDialog(this) == true ? openFileDialog.FileName : null;
	}

	private static System.Windows.Controls.Button CreateEditModeButton()
	{
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		System.Windows.Controls.Button button = CreateHeaderActionButton(AppText.Choose("앱 수정", "Edit Apps"), HeaderActionButtonWidth, HeaderActionButtonHeight);
		button.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		button.FontSize = 14.0;
		button.FontWeight = FontWeights.Bold;
		button.Foreground = System.Windows.Media.Brushes.White;
		button.Background = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops = 
			{
				new GradientStop(System.Windows.Media.Color.FromRgb(37, 99, 235), 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(6, 182, 212), 0.48),
				new GradientStop(System.Windows.Media.Color.FromRgb(124, 58, 237), 1.0)
			}
		};
		button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		button.BorderThickness = new Thickness(1.4);
		button.Effect = new DropShadowEffect
		{
			BlurRadius = 18.0,
			ShadowDepth = 0.0,
			Opacity = 0.82,
			Color = System.Windows.Media.Color.FromRgb(96, 165, 250)
		};
		return button;
	}

	private static LinearGradientBrush CreateShellBrush()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops = 
			{
				new GradientStop(System.Windows.Media.Color.FromArgb(250, 8, 10, 16), 0.0),
				new GradientStop(System.Windows.Media.Color.FromArgb(248, 17, 23, 38), 0.34),
				new GradientStop(System.Windows.Media.Color.FromArgb(244, 20, 18, 32), 0.68),
				new GradientStop(System.Windows.Media.Color.FromArgb(250, 5, 7, 12), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateRibbonBrush()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 0.0),
			GradientStops = 
			{
				new GradientStop(System.Windows.Media.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0),
				new GradientStop(System.Windows.Media.Color.FromArgb(90, 56, 189, 248), 0.2),
				new GradientStop(System.Windows.Media.Color.FromArgb(106, 99, 102, 241), 0.48),
				new GradientStop(System.Windows.Media.Color.FromArgb(104, 139, 92, 246), 0.76),
				new GradientStop(System.Windows.Media.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateTitleBrush()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 0.0),
			GradientStops = 
			{
				new GradientStop(Colors.White, 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(224, 242, 254), 0.25),
				new GradientStop(System.Windows.Media.Color.FromRgb(196, 181, 253), 0.58),
				new GradientStop(System.Windows.Media.Color.FromRgb(147, 197, 253), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateTabBrush(bool selected)
	{
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		if (!selected)
		{
			return new LinearGradientBrush
			{
				StartPoint = new Point(0.0, 0.0),
				EndPoint = new Point(1.0, 1.0),
				GradientStops = 
				{
					new GradientStop(System.Windows.Media.Color.FromArgb(241, 26, 34, 51), 0.0),
					new GradientStop(System.Windows.Media.Color.FromArgb(233, 26, 27, 46), 0.48),
					new GradientStop(System.Windows.Media.Color.FromArgb(231, 36, 26, 64), 1.0)
				}
			};
		}
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops = 
			{
				new GradientStop(System.Windows.Media.Color.FromRgb(30, 58, 138), 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(67, 56, 202), 0.5),
				new GradientStop(System.Windows.Media.Color.FromRgb(124, 58, 237), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateSlotBrush()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops = 
			{
				new GradientStop(System.Windows.Media.Color.FromArgb(58, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0),
				new GradientStop(System.Windows.Media.Color.FromArgb(36, 30, 41, 59), 0.48),
				new GradientStop(System.Windows.Media.Color.FromArgb(36, 42, 43, 74), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateContextMenuBrush()
	{
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops =
			{
				new GradientStop(System.Windows.Media.Color.FromRgb(15, 23, 42), 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(22, 78, 99), 0.36),
				new GradientStop(System.Windows.Media.Color.FromRgb(20, 83, 45), 0.72),
				new GradientStop(System.Windows.Media.Color.FromRgb(120, 53, 15), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateContextDeleteBrush()
	{
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops =
			{
				new GradientStop(System.Windows.Media.Color.FromRgb(244, 63, 94), 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(249, 115, 22), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateGroupSlotBrush()
	{
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops =
			{
				new GradientStop(System.Windows.Media.Color.FromArgb(128, 20, 184, 166), 0.0),
				new GradientStop(System.Windows.Media.Color.FromArgb(92, 34, 197, 94), 0.36),
				new GradientStop(System.Windows.Media.Color.FromArgb(104, 251, 191, 36), 0.7),
				new GradientStop(System.Windows.Media.Color.FromArgb(88, 244, 63, 94), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateGroupSlotBorderBrush()
	{
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops =
			{
				new GradientStop(System.Windows.Media.Color.FromRgb(236, 253, 245), 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(45, 212, 191), 0.28),
				new GradientStop(System.Windows.Media.Color.FromRgb(250, 204, 21), 0.62),
				new GradientStop(System.Windows.Media.Color.FromRgb(251, 113, 133), 1.0)
			}
		};
	}

	private static LinearGradientBrush CreateGroupBadgeBrush()
	{
		return new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(1.0, 1.0),
			GradientStops =
			{
				new GradientStop(System.Windows.Media.Color.FromRgb(14, 165, 233), 0.0),
				new GradientStop(System.Windows.Media.Color.FromRgb(34, 197, 94), 0.45),
				new GradientStop(System.Windows.Media.Color.FromRgb(249, 115, 22), 1.0)
			}
		};
	}

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.15.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			base.Title = LauncherStore.DefaultLauncherTitle;
			base.Width = 720.0;
			base.Height = 520.0;
			base.MinWidth = 720.0;
			base.MinHeight = 520.0;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.CanMinimize;
			base.AllowsTransparency = true;
			base.Background = System.Windows.Media.Brushes.Transparent;
			base.ShowInTaskbar = true;
			RootScene = new Grid
			{
				ClipToBounds = false
			};
			base.Content = RootScene;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.15.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			RootScene = (Grid)target;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
