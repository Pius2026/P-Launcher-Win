using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PiusLauncher;

public sealed class LauncherItem : INotifyPropertyChanged
{
	public const int MaxTargetsPerSlot = 10;

	private readonly List<LauncherTarget> _targets = new List<LauncherTarget>();

	private string? _targetPath;

	private string _displayName;

	private LauncherItemKind _kind;

	private string? _slotName;

	public int WidgetIndex { get; }

	public int SlotIndex { get; }

	public string SlotLabel => $"{SlotIndex + 1:00}";

	public string? SlotName
	{
		get
		{
			return _slotName;
		}
		private set
		{
			SetField(ref _slotName, value, "SlotName");
		}
	}

	public string? TargetPath
	{
		get
		{
			return _targetPath;
		}
		private set
		{
			SetField(ref _targetPath, value, "TargetPath");
		}
	}

	public IReadOnlyList<LauncherTarget> Targets => _targets;

	public int TargetCount => _targets.Count;

	public bool IsGroup => TargetCount > 1;

	public string DisplayName
	{
		get
		{
			if (!HasTarget)
			{
				return SlotName ?? AppText.DefaultSlotName(SlotIndex);
			}
			if (!string.IsNullOrWhiteSpace(SlotName))
			{
				return SlotName;
			}
			return IsGroup ? $"{_displayName} +{TargetCount - 1}" : _displayName;
		}
		private set
		{
			SetField(ref _displayName, value, "DisplayName");
		}
	}

	public LauncherItemKind Kind
	{
		get
		{
			return _kind;
		}
		private set
		{
			SetField(ref _kind, value, "Kind");
		}
	}

	public bool HasTarget => !string.IsNullOrWhiteSpace(TargetPath);

	public string Subtitle
	{
		get
		{
			if (!HasTarget)
			{
				return AppText.Choose("+ 눌러 추가", "+ Click to add");
			}
			return IsGroup ? AppText.Choose($"{TargetCount}개 일괄 실행", $"Run {TargetCount} items") : GetKindDisplayName(Kind);
		}
	}

	public string ToolTip
	{
		get
		{
			if (!HasTarget)
			{
				return AppText.Choose("프로그램, 파일, 바로가기, 폴더 추가", "Add a program, file, shortcut, or folder");
			}
			if (!IsGroup)
			{
				return TargetPath ?? string.Empty;
			}
			return string.Join(Environment.NewLine, _targets.Select((target, index) => $"{index + 1}. {target.DisplayName} - {target.TargetPath}"));
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public LauncherItem(int widgetIndex, int slotIndex)
	{
		WidgetIndex = widgetIndex;
		SlotIndex = slotIndex;
		_displayName = AppText.DefaultSlotName(slotIndex);
		_kind = LauncherItemKind.Empty;
	}

	public void SetTarget(string targetPath, string? displayName = null)
	{
		_targets.Clear();
		AddTarget(targetPath, displayName);
	}

	public bool AddTarget(string targetPath, string? displayName = null)
	{
		if (string.IsNullOrWhiteSpace(targetPath) || _targets.Count >= MaxTargetsPerSlot || _targets.Any(target => string.Equals(target.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)))
		{
			return false;
		}
		LauncherItemKind kind = DetectKind(targetPath);
		string cleanDisplayName = CleanDisplayName(string.IsNullOrWhiteSpace(displayName) ? CreateDisplayName(targetPath) : displayName.Trim());
		_targets.Add(new LauncherTarget(targetPath, cleanDisplayName, kind));
		SyncPrimaryTarget();
		NotifyVisualStateChanged();
		return true;
	}

	public void SetSlotName(string? slotName)
	{
		SlotName = string.IsNullOrWhiteSpace(slotName) ? null : CleanDisplayName(slotName);
		NotifyVisualStateChanged();
	}

	public bool RemoveTargetAt(int index)
	{
		if (index < 0 || index >= _targets.Count)
		{
			return false;
		}
		_targets.RemoveAt(index);
		SyncPrimaryTarget();
		NotifyVisualStateChanged();
		return true;
	}

	public void Clear()
	{
		_targets.Clear();
		SyncPrimaryTarget();
		NotifyVisualStateChanged();
	}

	public static bool MoveContentsWithinWidget(IList<LauncherItem> widgetItems, int sourceSlotIndex, int targetSlotIndex)
	{
		if (sourceSlotIndex == targetSlotIndex)
		{
			return false;
		}
		int sourcePosition = IndexOfSlot(widgetItems, sourceSlotIndex);
		int targetPosition = IndexOfSlot(widgetItems, targetSlotIndex);
		if (sourcePosition < 0 || targetPosition < 0)
		{
			return false;
		}
		LauncherItem source = widgetItems[sourcePosition];
		LauncherItem target = widgetItems[targetPosition];
		if (source.WidgetIndex != target.WidgetIndex)
		{
			return false;
		}
		SlotContentSnapshot movedContent = SlotContentSnapshot.From(source);
		if (sourcePosition < targetPosition)
		{
			for (int index = sourcePosition; index < targetPosition; index++)
			{
				widgetItems[index].ApplyContentSnapshot(SlotContentSnapshot.From(widgetItems[index + 1]));
			}
		}
		else
		{
			for (int index = sourcePosition; index > targetPosition; index--)
			{
				widgetItems[index].ApplyContentSnapshot(SlotContentSnapshot.From(widgetItems[index - 1]));
			}
		}
		target.ApplyContentSnapshot(movedContent);
		return true;
	}

	public static LauncherItemKind DetectKind(string targetPath)
	{
		if (Uri.TryCreate(targetPath, UriKind.Absolute, out Uri? uri) && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
		{
			return LauncherItemKind.Site;
		}
		if (Directory.Exists(targetPath))
		{
			return LauncherItemKind.Folder;
		}
		string text = Path.GetExtension(targetPath).ToLowerInvariant();
		if (text == null)
		{
			goto IL_00b4;
		}
		if (string.Equals(text, ".url", StringComparison.OrdinalIgnoreCase))
		{
			return LauncherItemKind.Site;
		}
		int length = text.Length;
		if (length != 4)
		{
			if (length != 10 || !(text == ".appref-ms"))
			{
				goto IL_00b4;
			}
		}
		else
		{
			switch (text[2])
			{
			case 'n':
				if (text == ".lnk")
				{
					goto IL_0096;
				}
				goto IL_00b4;
			case 'r':
				if (text == ".url")
				{
					goto IL_0096;
				}
				goto IL_00b4;
			case 'x':
				break;
			case 'o':
				goto IL_00a7;
			default:
				goto IL_00b4;
			case 's':
				goto IL_00b6;
			case 'a':
				goto IL_00c5;
			case 'm':
				goto IL_00d4;
				IL_0096:
				return LauncherItemKind.Shortcut;
			}
			if (!(text == ".exe"))
			{
				goto IL_00b4;
			}
		}
		goto IL_00e1;
		IL_00a7:
		if (!(text == ".com"))
		{
			goto IL_00b4;
		}
		goto IL_00e1;
		IL_00b4:
		return LauncherItemKind.File;
		IL_00b6:
		if (!(text == ".msi"))
		{
			goto IL_00b4;
		}
		goto IL_00e1;
		IL_00c5:
		if (!(text == ".bat"))
		{
			goto IL_00b4;
		}
		goto IL_00e1;
		IL_00d4:
		if (!(text == ".cmd"))
		{
			goto IL_00b4;
		}
		goto IL_00e1;
		IL_00e1:
		return LauncherItemKind.Program;
	}

	private static string GetKindDisplayName(LauncherItemKind kind)
	{
		return kind switch
		{
			LauncherItemKind.Program => AppText.Choose("프로그램", "Program"),
			LauncherItemKind.Shortcut => AppText.Choose("바로가기", "Shortcut"),
			LauncherItemKind.File => AppText.Choose("파일", "File"),
			LauncherItemKind.Folder => AppText.Choose("폴더", "Folder"),
			LauncherItemKind.Site => AppText.Choose("사이트", "Website"),
			_ => AppText.Choose("비어 있음", "Empty"),
		};
	}

	private static string CreateDisplayName(string targetPath)
	{
		string path = targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string text = Path.GetFileNameWithoutExtension(path);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Path.GetFileName(path);
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return CleanDisplayName(text);
		}
		return targetPath;
	}

	private static string CleanDisplayName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return name;
		}
		string text = name.Trim();
		if (text.StartsWith("위젯", StringComparison.OrdinalIgnoreCase))
		{
			int num = text.IndexOf('-');
			int num2 = ((num >= 0) ? text.IndexOf('-', num + 1) : (-1));
			if (num2 >= 0 && num2 + 1 < text.Length)
			{
				string text2 = text.Substring(num2 + 1).Trim();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					return text2;
				}
			}
		}
		return text;
	}

	private void NotifyVisualStateChanged()
	{
		OnPropertyChanged("DisplayName");
		OnPropertyChanged("HasTarget");
		OnPropertyChanged("Subtitle");
		OnPropertyChanged("ToolTip");
		OnPropertyChanged("Targets");
		OnPropertyChanged("TargetCount");
		OnPropertyChanged("IsGroup");
		OnPropertyChanged("SlotName");
		OnPropertyChanged("TargetPath");
		OnPropertyChanged("Kind");
	}

	private static int IndexOfSlot(IList<LauncherItem> widgetItems, int slotIndex)
	{
		for (int index = 0; index < widgetItems.Count; index++)
		{
			if (widgetItems[index].SlotIndex == slotIndex)
			{
				return index;
			}
		}
		return -1;
	}

	private void ApplyContentSnapshot(SlotContentSnapshot snapshot)
	{
		_targets.Clear();
		foreach (LauncherTarget target in snapshot.Targets)
		{
			_targets.Add(new LauncherTarget(target.TargetPath, target.DisplayName, target.Kind));
		}
		SlotName = snapshot.SlotName;
		SyncPrimaryTarget();
		NotifyVisualStateChanged();
	}

	private void SyncPrimaryTarget()
	{
		LauncherTarget? firstTarget = _targets.FirstOrDefault();
		TargetPath = firstTarget?.TargetPath;
		Kind = firstTarget?.Kind ?? LauncherItemKind.Empty;
		DisplayName = firstTarget?.DisplayName ?? AppText.DefaultSlotName(SlotIndex);
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

	private sealed record SlotContentSnapshot(IReadOnlyList<LauncherTarget> Targets, string? SlotName)
	{
		public static SlotContentSnapshot From(LauncherItem item)
		{
			return new SlotContentSnapshot(
				item._targets.Select(target => new LauncherTarget(target.TargetPath, target.DisplayName, target.Kind)).ToArray(),
				item.SlotName);
		}
	}
}
