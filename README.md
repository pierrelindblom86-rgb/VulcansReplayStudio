<div align="center">

<img src="docs/banner.png" alt="Vulcans Replay Studio promotional banner (illustrated graphic, not an app screenshot)" width="100%">

# Vulcans Replay Studio

### Never lose your best moments.

Vulcans Replay Studio records your screen quietly in the background. When something great happens, press **F9** and the last moments are saved as a clip you can play, trim and keep.

**[⬇ Download the installer](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/download/v1.1.0/VulcansReplayStudio-Setup-1.1.0.exe)** &nbsp;·&nbsp; **[Portable ZIP](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/download/v1.1.0/VulcansReplayStudio2_1.1.0_Portable_win-x64.zip)** &nbsp;·&nbsp; **[Latest release](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/latest)**

Free · Windows 64-bit · Works offline · Version 1.1.0

</div>

---

## Why Replay Studio

Good moments rarely wait for you to hit record. Replay Studio keeps a rolling replay running in the background, so the last part of a moment is always there when you need it. Save it with one key, then find it, play it and trim it in your own local library.

## Features

| | |
|---|---|
| 🎬 **Background replay recording** | Keeps a rolling replay of your screen running in the background. The replay length defaults to 60 seconds and is set in Settings. Capture defaults to 1920×1080. |
| ⌨️ **Save with F9** | Press **F9** to save the current replay. The hotkey can be changed in Settings. |
| 📚 **Local clip library** | Saved clips sit in a folder on your PC and are browsable from **Replay Library**. You can rename clips, delete them (to the Recycle Bin for clips on fixed drives) and clear the Recent Clips list. |
| 📥 **Import existing videos** | Add video files you already have to your library. Removing an imported clip takes it out of the library only; the file itself stays where it is. |
| ▶️ **Built-in player** | Watch clips inside the app with the Clip Player. |
| ✂️ **Non-destructive clip editor** | Set In and Out points and reset the trim at any time. Your original clip is not overwritten. |
| 🪶 **Single Windows app** | A standard Windows installer, or a portable ZIP that needs no installation. |

## How it works

1. **Start capture.** Install Replay Studio, open it, and start recording from the **Capture** page. It keeps the rolling replay running in the background.
2. **Press F9 when it counts.** Save the last replay with one key press.
3. **Watch, trim and keep.** Open **Replay Library** to play a clip, or open the **Clip Editor** to set In and Out points.

## Download

| File | What it is | SHA-256 |
|---|---|---|
| [`VulcansReplayStudio-Setup-1.1.0.exe`](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/download/v1.1.0/VulcansReplayStudio-Setup-1.1.0.exe) | Windows installer (recommended) | `ff862097900bc9e8c4bb6f059b36d347f73d2749268eeac8b19a741431ce1c8e` |
| [`VulcansReplayStudio2_1.1.0_Portable_win-x64.zip`](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/download/v1.1.0/VulcansReplayStudio2_1.1.0_Portable_win-x64.zip) | Portable ZIP, no installation | `72c01c48b9f21ec1bbe7bdd9b74e910a74521d4e0583b457b8aaf7408f69dfe0` |
| [`SHA256SUMS.txt`](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/download/v1.1.0/SHA256SUMS.txt) | Checksums for both files | — |

Browse every version on the **[release page](https://github.com/pierrelindblom86-rgb/VulcansReplayStudio/releases/latest)**.

## Installation

1. Download `VulcansReplayStudio-Setup-1.1.0.exe` from the table above.
2. Run the installer. It installs for the current user and does not need administrator rights.
3. If the installer reports that the .NET 9 Desktop Runtime (x64) is missing, install it from Microsoft, then run the installer again.
4. Start **Vulcans Replay Studio** from the Start menu.

## Portable version

1. Download `VulcansReplayStudio2_1.1.0_Portable_win-x64.zip` and extract it to a folder you control, for example `C:\Apps\VulcansReplayStudio`.
2. Keep the extracted files together. The FFmpeg tools are in the `Tools\FFmpeg` folder and must stay there.
3. Run `VulcansReplayStudio2.App.exe`.
4. The portable build needs the .NET 9 Desktop Runtime (x64) installed on the PC, the same as the installer.

## Verify your download (SHA-256)

Compare the file's hash with the table above before you run it.

In PowerShell:

```
certutil -hashfile "VulcansReplayStudio-Setup-1.1.0.exe" SHA256
```

Or check every file at once against `SHA256SUMS.txt` from the release page. The output hash must match exactly.

## System requirements

- **Windows** 10 or 11, 64-bit (x64).
- **.NET 9 Desktop Runtime (x64).** The installer checks for it and links to the download if it is missing. The portable ZIP needs it too.
- Free disk space for your saved clips. Clips are stored in the Storage Path you choose in Settings.

## Known limitations

- **Windows x64 only.**
- **CPU video encoding.** This release encodes video with the CPU (x264 through FFmpeg). GPU hardware encoding is not included.
- **Recycle Bin on fixed drives only.** Deleted clips go to the Recycle Bin when they are on a fixed drive.
- **Desktop capture.** Replay Studio records the desktop; it does not capture a single application window.

## License and credits

- Vulcans Replay Studio is released under the **MIT License** (see [`LICENSE`](LICENSE)).
- Video capture and encoding use **FFmpeg** and **ffprobe**, which are distributed under their own license. See [`licenses/FFmpeg-LICENSE.txt`](licenses/FFmpeg-LICENSE.txt).

## More free tools from Vulcans Software Solutions

- **[Vulcans Living Wallpaper](https://github.com/pierrelindblom86-rgb/VulcansLivingWP)**: play video and animated wallpapers on Windows, per monitor or spanned across displays.

Each app works on its own.

---

<details>
<summary><b>Developer documentation</b> (building from source, project layout, licenses)</summary>

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
`dist\VulcansReplayStudio2_<version>` (for example `dist\VulcansReplayStudio2_1.1.0`), with
`Tools\FFmpeg` alongside it. Compile it with Inno Setup 6:

```
dotnet publish src\VulcansReplayStudio2.App -c Release -r win-x64 --self-contained false -p:DebugType=none -o dist\VulcansReplayStudio2_1.1.0
iscc installer\VulcansReplayStudio2.iss
```

The version is set in `src\Directory.Build.props` and in `installer\VulcansReplayStudio2.iss`.

## License

MIT, see `LICENSE`. FFmpeg is distributed under its own license, see `licenses\FFmpeg-LICENSE.txt`.

</details>
