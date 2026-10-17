# PewTube v0.2.0

A lightweight, open-source-oriented Windows desktop media downloader written in C# (.NET 10, WinForms), using external **yt-dlp** and **FFmpeg** executables.

## Build and run

1. Open `PewTube.sln` in Visual Studio 2026 with the .NET 10 SDK and Windows Desktop workload.
2. For development: press F5. Standard Debug builds generate a `.dll` and `.runtimeconfig.json` next to the `.exe`. **This is expected.**
3. To ship **one** main application EXE, run `Publish-Windows.bat`, or run:

   `dotnet publish PewTube/PewTube.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o Publish/PewTube`

4. Put `yt-dlp.exe` and `ffmpeg.exe` in a folder named `Tools` **beside the published** `PewTube.exe`:

   ```text
   Publish/PewTube/
     PewTube.exe
     Tools/
       yt-dlp.exe
       ffmpeg.exe
   ```

5. Downloads: https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe and https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip (extract `bin/ffmpeg.exe`).

The self-contained, single-file publish embeds the app's .NET dependencies and runtime config, so the target Windows machine does **not** need a separate .NET installation. **This does not embed yt-dlp or FFmpeg**, which remain easy to update in `Tools`. Single-file binaries may be larger, and some native components may be extracted to a temporary directory at runtime.

## Options

- **TYPE:** Video with Audio / Audio Only
- **FORMAT:** Video = Auto, MP4, MKV, WebM. Audio = MP3, WAV, AIFF, M4A.
- **QUALITY:** Video = best or low/medium/high profiles capped at 240p–4320p. Audio = bitrates for MP3/M4A, sample rate and PCM bit depth for WAV/AIFF.
- UI options cascade: TYPE -> FORMAT -> QUALITY.

**Important quality caveats:** Audio extraction can involve **re-encoding**. Converting lossy source audio to 24-bit/48 kHz PCM does not recover missing detail or increase its native fidelity. MP3/M4A bitrate choices control target encoding, not the original stream's true quality. Video height options are limits, not a guarantee that the site offers a particular size. MP4/WebM remuxing may fail if the source's video/audio codecs are incompatible with the chosen container; MKV or Auto often works for mixed codecs. Not all websites support all options.

## Code map

- `Program.cs` — Windows Forms application startup.
- `MainForm.cs` — UI appearance, cascading dropdowns, validation, and progress/status handling.
- `DownloadCatalog.cs` (`DownloadOptions.cs`) — available types, formats and quality presets.
- `Downloader.cs` — process execution, metadata JSON parsing, and yt-dlp/FFmpeg arguments.
- `Publish-Windows.bat` — release publish for a single main executable.

To add a new quality option, update `DownloadCatalog` first, then map it in `Downloader`. Commands are passed using `ProcessStartInfo.ArgumentList` (no shell interpolation). All video URLs should be obtained and used lawfully, with attention to copyright and platform terms. DRM-protected and unsupported streams are not handled.

This source was prepared without a Windows/.NET 10 build environment, so it has not been compiled or runtime-tested here.
