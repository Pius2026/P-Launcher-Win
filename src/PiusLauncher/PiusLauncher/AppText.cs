using System;

namespace PiusLauncher;

public enum UiLanguage
{
	Korean,
	English
}

public static class AppText
{
	private static UiLanguage _currentLanguage = UiLanguage.Korean;

	public static UiLanguage CurrentLanguage
	{
		get => _currentLanguage;
		set => _currentLanguage = Normalize(value);
	}

	public static bool IsEnglish => CurrentLanguage == UiLanguage.English;

	public static string Choose(string korean, string english)
	{
		return IsEnglish ? english : korean;
	}

	public static string DefaultWidgetName(int index)
	{
		return IsEnglish ? $"Widget {index + 1}" : $"위젯 {index + 1}";
	}

	public static string DefaultSlotName(int index)
	{
		return IsEnglish ? $"Slot {index + 1:00}" : $"슬롯 {index + 1:00}";
	}

	public static bool IsDefaultWidgetName(string name, int index)
	{
		return string.Equals(name, $"위젯 {index + 1}", StringComparison.Ordinal)
			|| string.Equals(name, $"Widget {index + 1}", StringComparison.Ordinal);
	}

	public static UiLanguage Normalize(UiLanguage language)
	{
		return Enum.IsDefined(language) ? language : UiLanguage.Korean;
	}
}
