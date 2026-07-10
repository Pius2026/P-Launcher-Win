using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PiusLauncher;

public sealed class LauncherStore
{
	private sealed class LauncherStoreSnapshot
	{
		public string LauncherTitle { get; set; } = DefaultLauncherTitle;

		public List<string> WidgetNames { get; set; } = new List<string>();

		public List<LauncherItemRecord> Items { get; set; } = new List<LauncherItemRecord>();

		public int SelectedWidgetIndex { get; set; }
	}

	private sealed class LauncherItemRecord
	{
		public int WidgetIndex { get; set; }

		public int SlotIndex { get; set; }

		public string TargetPath { get; set; } = string.Empty;

		public string? DisplayName { get; set; }

		public string? SlotName { get; set; }

		public LauncherItemKind Kind { get; set; }

		public List<LauncherTargetRecord> Entries { get; set; } = new List<LauncherTargetRecord>();
	}

	private sealed class LauncherTargetRecord
	{
		public string TargetPath { get; set; } = string.Empty;

		public string? DisplayName { get; set; }

		public LauncherItemKind Kind { get; set; }
	}

	private const int WidgetCount = 3;

	private const int SlotsPerWidget = 10;

	public const string DefaultLauncherTitle = "P-Edu-Launcher";

	public const string SavedSettingsFolderName = "저장된 설정";

	private const string SettingsFileName = "launcher-items.json";

	private const string LegacyDefaultLauncherTitle = "PIUS 런처";

	private const string LegacyEnglishDefaultLauncherTitle = "PIUS Launcher";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly string _storageFile;

	public LauncherStore()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiusLauncher");
		Directory.CreateDirectory(text);
		_storageFile = Path.Combine(text, SettingsFileName);
	}

	public LauncherStore(string storageFile)
	{
		if (string.IsNullOrWhiteSpace(storageFile))
		{
			throw new ArgumentException("Storage file path is required.", nameof(storageFile));
		}
		string? directoryName = Path.GetDirectoryName(storageFile);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		_storageFile = storageFile;
	}

	public LauncherDisplaySettings LoadInto(IReadOnlyList<IReadOnlyList<LauncherItem>> widgets)
	{
		if (!File.Exists(_storageFile))
		{
			return new LauncherDisplaySettings();
		}
		try
		{
			LauncherStoreSnapshot launcherStoreSnapshot = JsonSerializer.Deserialize<LauncherStoreSnapshot>(File.ReadAllText(_storageFile), JsonOptions);
			if (launcherStoreSnapshot == null)
			{
				return new LauncherDisplaySettings();
			}
			foreach (LauncherItemRecord item in launcherStoreSnapshot.Items ?? new List<LauncherItemRecord>())
			{
				int widgetIndex = item.WidgetIndex;
				bool flag = ((widgetIndex < 0 || widgetIndex >= 3) ? true : false);
				if (!flag)
				{
					int slotIndex = item.SlotIndex;
					flag = ((slotIndex < 0 || slotIndex >= 10) ? true : false);
				}
				if (!flag)
				{
					LauncherItem slot = widgets[item.WidgetIndex][item.SlotIndex];
					List<LauncherTargetRecord> entries = item.Entries ?? new List<LauncherTargetRecord>();
					if (entries.Count > 0)
					{
						foreach (LauncherTargetRecord entry in entries.Take(LauncherItem.MaxTargetsPerSlot))
						{
							if (!string.IsNullOrWhiteSpace(entry.TargetPath))
							{
								slot.AddTarget(entry.TargetPath, entry.DisplayName);
							}
						}
					}
					else if (!string.IsNullOrWhiteSpace(item.TargetPath))
					{
						slot.SetTarget(item.TargetPath, item.DisplayName);
					}
					slot.SetSlotName(item.SlotName);
				}
			}
			return new LauncherDisplaySettings
			{
				LauncherTitle = NormalizeLauncherTitle(launcherStoreSnapshot.LauncherTitle),
				WidgetNames = (launcherStoreSnapshot.WidgetNames ?? new List<string>()),
				SelectedWidgetIndex = Math.Clamp(launcherStoreSnapshot.SelectedWidgetIndex, 0, 2)
			};
		}
		catch
		{
			string destFileName = $"{_storageFile}.broken-{DateTime.Now:yyyyMMddHHmmss}";
			File.Copy(_storageFile, destFileName, overwrite: true);
			return new LauncherDisplaySettings();
		}
	}

	public void Save(IEnumerable<LauncherItem> items, string launcherTitle, IEnumerable<WidgetTab> widgetTabs, int selectedWidgetIndex)
	{
		WriteSnapshot(_storageFile, items, launcherTitle, widgetTabs, selectedWidgetIndex);
	}

	public string SaveCurrentSettingsSnapshot(string appTopLevelDirectory, IEnumerable<LauncherItem> items, string launcherTitle, IEnumerable<WidgetTab> widgetTabs, int selectedWidgetIndex)
	{
		string snapshotFile = GetSavedSettingsSnapshotFile(appTopLevelDirectory);
		return SaveCurrentSettingsSnapshotFile(snapshotFile, items, launcherTitle, widgetTabs, selectedWidgetIndex);
	}

	public string SaveCurrentSettingsSnapshotFile(string snapshotFile, IEnumerable<LauncherItem> items, string launcherTitle, IEnumerable<WidgetTab> widgetTabs, int selectedWidgetIndex)
	{
		if (string.IsNullOrWhiteSpace(snapshotFile))
		{
			throw new ArgumentException("Snapshot file path is required.", nameof(snapshotFile));
		}
		WriteSnapshot(snapshotFile, items, launcherTitle, widgetTabs, selectedWidgetIndex);
		return snapshotFile;
	}

	public LauncherDisplaySettings LoadSavedSettingsSnapshot(string appTopLevelDirectory, IReadOnlyList<IReadOnlyList<LauncherItem>> widgets)
	{
		string snapshotFile = GetSavedSettingsSnapshotFile(appTopLevelDirectory);
		return LoadSavedSettingsSnapshotFile(snapshotFile, widgets);
	}

	public LauncherDisplaySettings LoadSavedSettingsSnapshotFile(string snapshotFile, IReadOnlyList<IReadOnlyList<LauncherItem>> widgets)
	{
		if (string.IsNullOrWhiteSpace(snapshotFile))
		{
			throw new ArgumentException("Snapshot file path is required.", nameof(snapshotFile));
		}
		if (!File.Exists(snapshotFile))
		{
			throw new FileNotFoundException("Saved launcher settings were not found.", snapshotFile);
		}
		if (JsonSerializer.Deserialize<LauncherStoreSnapshot>(File.ReadAllText(snapshotFile), JsonOptions) == null)
		{
			throw new InvalidDataException("Saved launcher settings file is empty.");
		}
		foreach (IReadOnlyList<LauncherItem> widget in widgets)
		{
			foreach (LauncherItem item in widget)
			{
				item.Clear();
				item.SetSlotName(null);
			}
		}
		File.Copy(snapshotFile, _storageFile, overwrite: true);
		return LoadInto(widgets);
	}

	public static string GetSavedSettingsDirectory(string appTopLevelDirectory)
	{
		if (string.IsNullOrWhiteSpace(appTopLevelDirectory))
		{
			throw new ArgumentException("App top-level folder path is required.", nameof(appTopLevelDirectory));
		}
		string directory = Path.Combine(appTopLevelDirectory, SavedSettingsFolderName);
		Directory.CreateDirectory(directory);
		return directory;
	}

	private static string GetSavedSettingsSnapshotFile(string appTopLevelDirectory)
	{
		return Path.Combine(GetSavedSettingsDirectory(appTopLevelDirectory), SettingsFileName);
	}

	private static void WriteSnapshot(string storageFile, IEnumerable<LauncherItem> items, string launcherTitle, IEnumerable<WidgetTab> widgetTabs, int selectedWidgetIndex)
	{
		string? directoryName = Path.GetDirectoryName(storageFile);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string contents = JsonSerializer.Serialize(new LauncherStoreSnapshot
		{
			LauncherTitle = NormalizeLauncherTitle(launcherTitle),
			WidgetNames = (from tab in widgetTabs
				orderby tab.Index
				select tab.Name).ToList(),
			SelectedWidgetIndex = Math.Clamp(selectedWidgetIndex, 0, 2),
				Items = (from item in items
					where item.HasTarget && !string.IsNullOrWhiteSpace(item.TargetPath)
					select new LauncherItemRecord
					{
						WidgetIndex = item.WidgetIndex,
						SlotIndex = item.SlotIndex,
						TargetPath = item.TargetPath,
						DisplayName = item.Targets.FirstOrDefault()?.DisplayName ?? item.DisplayName,
						SlotName = item.SlotName,
						Kind = item.Kind,
						Entries = item.Targets.Select(target => new LauncherTargetRecord
						{
							TargetPath = target.TargetPath,
							DisplayName = target.DisplayName,
							Kind = target.Kind
						}).ToList()
				}).ToList()
		}, JsonOptions);
		File.WriteAllText(storageFile, contents);
	}

	public static string NormalizeLauncherTitle(string launcherTitle)
	{
		if (string.IsNullOrWhiteSpace(launcherTitle))
		{
			return DefaultLauncherTitle;
		}
		string title = launcherTitle.Trim();
		return string.Equals(title, LegacyDefaultLauncherTitle, StringComparison.Ordinal) || string.Equals(title, LegacyEnglishDefaultLauncherTitle, StringComparison.Ordinal) ? DefaultLauncherTitle : title;
	}
}
