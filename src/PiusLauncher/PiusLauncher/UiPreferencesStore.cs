using System;
using System.IO;
using System.Text.Json;

namespace PiusLauncher;

public sealed class UiPreferencesStore
{
	private sealed class UiPreferences
	{
		public string Language { get; set; } = nameof(UiLanguage.Korean);
	}

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly string _storageFile;

	public UiPreferencesStore()
		: this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiusLauncher", "ui-preferences.json"))
	{
	}

	public UiPreferencesStore(string storageFile)
	{
		if (string.IsNullOrWhiteSpace(storageFile))
		{
			throw new ArgumentException("Preferences file path is required.", nameof(storageFile));
		}
		_storageFile = storageFile;
	}

	public UiLanguage LoadLanguage()
	{
		if (!File.Exists(_storageFile))
		{
			return UiLanguage.Korean;
		}
		try
		{
			UiPreferences preferences = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(_storageFile), JsonOptions);
			if (preferences != null
				&& Enum.TryParse(preferences.Language, ignoreCase: true, out UiLanguage language)
				&& Enum.IsDefined(language))
			{
				return language;
			}
		}
		catch (JsonException)
		{
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
		return UiLanguage.Korean;
	}

	public void SaveLanguage(UiLanguage language)
	{
		string directory = Path.GetDirectoryName(_storageFile);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}
		string contents = JsonSerializer.Serialize(new UiPreferences
		{
			Language = AppText.Normalize(language).ToString()
		}, JsonOptions);
		File.WriteAllText(_storageFile, contents);
	}
}
