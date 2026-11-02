# Vulcans Replay Studio

A free, offline, native Windows instant-replay recorder. It keeps a rolling buffer of the screen
and saves the most recent moments on demand. Nothing is uploaded.

## Layout

- `src\VulcansReplayStudio2.sln`: the solution.
  - `VulcansReplayStudio2.App`: WPF application (`VulcansReplayStudio2.App.exe`).
  - `VulcansReplayStudio2.Core`: models and service contracts.
  - `VulcansReplayStudio2.Infrastructure`: settings, hotkeys and clip library services.
  - `VulcansReplayStudio2.MediaEngine`: capture, replay buffer and export.
  - `VulcansReplayStudio2.Core.Tests`: unit tests.
- `installer\VulcansReplayStudio2.iss`: Inno Setup script for the per-user installer.

## Requirements

- Windows 10/11 x64.
- .NET 9 SDK to build, .NET 9 Desktop Runtime (x64) to run.
- `ffmpeg.exe` and `ffprobe.exe` in `Tools\FFmpeg`. The binaries are not stored in git because
  they exceed GitHub's file size limit. They are included in the release packages.

## Build and test

```
dotnet build src\VulcansReplayStudio2.sln -c Release
dotnet test src\VulcansReplayStudio2.Core.Tests
```

## Release packages

The installer script expects a framework-dependent publish of the App in
`dist\VulcansReplayStudio2_<version>` (for example `dist\VulcansReplayStudio2_1.0.1`), with
`Tools\FFmpeg` alongside it. Compile it with Inno Setup 6:

```
dotnet publish src\VulcansReplayStudio2.App -c Release -r win-x64 --self-contained false -p:DebugType=none -o dist\VulcansReplayStudio2_1.0.1
iscc installer\VulcansReplayStudio2.iss
```

The version is set in `src\Directory.Build.props` and in `installer\VulcansReplayStudio2.iss`.

## License

MIT, see `LICENSE`. FFmpeg is distributed under its own license, see `licenses\FFmpeg-LICENSE.txt`.
