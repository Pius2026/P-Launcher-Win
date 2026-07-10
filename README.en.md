# P-Edu-Launcher

[한국어](README.md) | [English](README.en.md)

P-Edu-Launcher is a Windows desktop launcher for opening programs, files, folders, shortcuts, and websites from one place. It is built with .NET 9 WPF and provides three widgets with 30 slots in total.

## Features

- Three widgets with ten slots each
- Up to ten targets in one slot, launched together
- Programs, regular files, folders, Windows shortcuts, and website shortcuts
- Drag and drop, slot reordering, and per-target removal in edit mode
- Renameable launcher title, widgets, and slots
- Save and load the current layout as JSON
- Reset the entire launcher or an individual widget
- System-tray operation with auto-hide and quick reopening
- Instant `한글` / `English` UI switching that persists across restarts

User-defined titles, widget names, slot names, and file names are user data and are not translated. Only labels and messages supplied by the application follow the selected language.

## Important runtime behavior

When launched, the app automatically creates or updates `P-Edu-Launcher.lnk` in the current user's Windows Startup folder. It remains active in the notification area until the Exit command is used.

Application data is stored locally.

| Data | Default location |
| --- | --- |
| Current layout | `%APPDATA%\PiusLauncher\launcher-items.json` |
| Language preference | `%APPDATA%\PiusLauncher\ui-preferences.json` |
| Managed shortcuts | `%APPDATA%\PiusLauncher\SlotShortcuts` |

The launcher does not implement remote synchronization or usage telemetry. When it opens a registered website or program, that target's behavior and privacy policy apply.

## Usage notes

- A saved layout JSON contains target paths, not the target files. Slots will fail on another PC or Windows account when equivalent paths do not exist.
- When a `.lnk`, `.url`, or `.appref-ms` shortcut is added, the app may copy it to the managed-shortcut folder and delete the original shortcut file. Use a copy first if the original shortcut must be retained.
- The app currently has no setting that disables automatic startup. Remove `P-Edu-Launcher.lnk` from the Windows Startup folder manually to disable it.
- Windows-owned file-dialog labels follow the Windows display language. Some internal folder and managed-file names remain in Korean for stored-data compatibility.

## Requirements

- Windows
- Building from source: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Running a framework-dependent publish: .NET 9 Desktop Runtime

The project targets `net9.0-windows`.

## Build from source

> Note: running the app with `dotnet run` also creates the Startup shortcut described above.

```powershell
dotnet restore P-Edu-Launcher.sln
dotnet build P-Edu-Launcher.sln -c Release --no-restore
dotnet test P-Edu-Launcher.sln -c Release --no-build
dotnet run --project src/PiusLauncher/PiusLauncher.csproj -c Release
```

## Publish for Windows x64

```powershell
dotnet publish src/PiusLauncher/PiusLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/publish/P-Edu-Launcher
```

`artifacts/` and the executable bundle at the repository root are ignored by Git. Keep compiled files out of source commits and attach release builds separately through GitHub Releases.

## Repository layout

```text
src/PiusLauncher/                 WPF app project, icons, and embedded resources
tests/PiusLauncher.Tests/         xUnit test project
.github/workflows/ci.yml          Windows build and test workflow
README.md / README.en.md          Korean and English documentation
LICENSE                           MIT license
```

`src/PiusLauncher/mainwindow.baml` is an embedded WPF resource required by the build and must not be removed.

## Files excluded from the public repository

The supplied `.gitignore` prevents accidental publication of local-only material, including:

- User layouts and local paths under `저장된 설정/`
- `backup/`, `artifacts/`, `bin/`, `obj/`, and `TestResults/`
- Root executables, DLLs, PDBs, shortcuts, and release archives
- `.omo/`, `.claude/`, `.codegraph/`, work plans, and QA logs

Before committing new files, inspect `git status` for personal data, absolute local paths, tokens, and generated binaries.

## Contributing and security

- Contribution guide: [CONTRIBUTING.md](CONTRIBUTING.md)
- Security reports: [SECURITY.md](SECURITY.md)
- Test-only third-party packages: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
- Pre-publication audit: [PUBLISHING_CHECKLIST.md](PUBLISHING_CHECKLIST.md)

## License

This project's source code is released under the [MIT License](LICENSE). NuGet packages and other third-party components remain subject to their respective licenses.

Before the first public release, confirm that you may distribute the source, embedded BAML, and icons under MIT. If `PiusLauncher` is not the correct legal copyright holder, update the name in `LICENSE` before publishing.
