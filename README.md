# ORACLE 2.0

ORACLE is a local-first Windows desktop utility for understanding running processes, system information, and selected files. This upgrade keeps the existing C# / WPF architecture and its system-inspection services.

## Requirements

- Windows 10 or Windows 11, x64
- For the framework-dependent developer build: .NET 10 SDK to build and matching .NET Desktop Runtime to run
- The published `win-x64` release includes the .NET 10 and Windows Desktop runtime files and does not require a separately installed .NET runtime

No third-party NuGet packages or external services are used by the application.

## Run and build from source

From the directory containing this README:

```powershell
dotnet run --project .\ORACLE.csproj
dotnet build .\ORACLE.csproj -c Debug
dotnet build .\ORACLE.csproj -c Release
dotnet run --project .\ORACLE.csproj -c Debug --no-build -- --self-test
```

The self-check covers byte formatting, UTF-8 detection, keyword findings, limitation disclosure, and the 2 MB file-inspection bound.

## Release package

The self-contained x64 download included in this repository is `website\deploy\downloads\ORACLE-2.0.0-win-x64-build4.zip`. Extract the ZIP, then run `ORACLE.exe`. No installer is included. The app is unsigned; Windows SmartScreen may show a warning. No Microsoft signature or security certification is claimed. Generated `bin`, `obj`, and expanded `releases` output directories are excluded from Git; create a new output directory when publishing from source.

To reproduce the x64 publish into a new empty release directory:

```powershell
dotnet publish .\ORACLE.csproj -c Release -r win-x64 --self-contained true -p:PublishDir="<absolute-new-output-directory>\"
```

## What ORACLE does

- **Dashboard:** Windows version, device name, uptime, CPU sampled from Windows system time, physical memory, and refresh time.
- **Processes:** Refreshable, searchable, sortable process list with process IDs, names, CPU estimate, memory, and available status.
- **Process details:** Observable details for a selected process. Executable path can be unavailable, and publisher/signature verification is not implemented. A process name alone does not establish intent.
- **File inspection:** Bounded (2 MB) text/source preview and simple keyword matches; basic version metadata for executables. Selected files are never executed. This is not a full malware scanner.
- **System information:** OS, processor model where available, logical processor count, architecture, physical memory, and storage volumes. GPU information is not collected.
- **Snippet editor:** Open and save text snippets up to 1 MB. Code execution and sandboxing are unavailable.
- **Activity:** Timestamped ORACLE messages for the current session, not Windows event logs.
- **Settings:** A validated, persisted dashboard refresh interval stored in `%LOCALAPPDATA%\ORACLE\settings.json`.

App data inspection and settings are local. The app contains no account flow, telemetry integration, or remote analysis service. Access to some process metadata can be restricted by Windows. Process CPU is an estimate between samples; the first sample may be unavailable.

## Website

The static site lives in `website\`. See [website/README.md](website/README.md) for local preview and deployment instructions. Its package manifest is generated from the real ZIP, and the download is enabled only if the package can be reached.
