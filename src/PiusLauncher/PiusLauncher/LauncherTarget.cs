namespace PiusLauncher;

public sealed class LauncherTarget
{
	public string TargetPath { get; }

	public string DisplayName { get; }

	public LauncherItemKind Kind { get; }

	public LauncherTarget(string targetPath, string displayName, LauncherItemKind kind)
	{
		TargetPath = targetPath;
		DisplayName = displayName;
		Kind = kind;
	}
}
