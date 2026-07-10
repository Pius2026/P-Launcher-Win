using System.Collections.Generic;

namespace PiusLauncher;

public sealed class LauncherDisplaySettings
{
	public string LauncherTitle { get; init; } = LauncherStore.DefaultLauncherTitle;

	public List<string> WidgetNames { get; init; } = new List<string>();

	public int SelectedWidgetIndex { get; init; }
}
