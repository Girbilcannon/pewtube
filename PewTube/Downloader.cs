using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PewTube;

internal sealed record MediaInfo(string Title, string? Uploader, string? Duration, string? Resolution);

/// <summary>
/// Thin, auditable wrapper around yt-dlp and FFmpeg. No shell, scripting, or web API:
/// process arguments are passed separately to prevent argument injection from URLs.
/// </summary>
internal sealed class Downloader
{
    private readonly string _tools = Path.Combine(AppContext.BaseDirectory, "Tools");
    private string YtDlp => Path.Combine(_tools, "yt-dlp.exe");
    private string Ffmpeg => Path.Combine(_tools, "ffmpeg.exe");

    public bool HasYtDlp => File.Exists(YtDlp);
    public bool HasFfmpeg => File.Exists(Ffmpeg);

    private static ProcessStartInfo Create(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    public async Task<MediaInfo> AnalyzeAsync(string url, CancellationToken token)
    {
        if (!HasYtDlp) throw new FileNotFoundException("Missing Tools\\yt-dlp.exe");
        using var process = new Process { StartInfo = Create(YtDlp,
            ["--no-playlist", "--dump-single-json", "--skip-download", "--no-warnings", "--", url]) };
        process.Start();
        using var registration = token.Register(() => TryKill(process));
        // Drain both streams concurrently to avoid a full stderr pipe blocking the process.
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(token);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        string output = await outputTask;
        string error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(LastError(error));
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        string? Get(string key) => root.TryGetProperty(key, out var e) && e.ValueKind != JsonValueKind.Null ? e.ToString() : null;
        string? duration = Get("duration");
        string? prettyDuration = double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
            ? TimeSpan.FromSeconds(d).ToString(d >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss") : null;
        return new MediaInfo(Get("title") ?? "Untitled video", Get("uploader"), prettyDuration, Get("resolution"));
    }

    public async Task DownloadAsync(string url, string folder, DownloadOptions options,
        Action<string> log, Action<double?> progress, CancellationToken token)
    {
        if (!HasYtDlp || !HasFfmpeg)
            throw new FileNotFoundException("Both yt-dlp.exe and ffmpeg.exe are required in the Tools folder.");

        Directory.CreateDirectory(folder);
        var args = new List<string> {
            "--no-playlist", "--newline", "--progress", "--no-overwrites",
            "--ffmpeg-location", _tools, "--paths", folder,
            "--output", "%(title).180B [%(id)s].%(ext)s"
        };

        if (options.Type == MediaType.AudioOnly)
            AddAudioArguments(args, options);
        else
            AddVideoArguments(args, options);

        // '--' terminates options so an unusual URL can never become a CLI switch.
        args.Add("--");
        args.Add(url);

        using var process = new Process { StartInfo = Create(YtDlp, args) };
        process.Start();
        using var registration = token.Register(() => TryKill(process));
        var errors = new StringBuilder();

        async Task Consume(StreamReader reader, bool isError)
        {
            while (await reader.ReadLineAsync(token) is { } line)
            {
                if (isError) errors.AppendLine(line);
                log(line);
                var match = Regex.Match(line, @"\[download\]\s+(\d+(?:\.\d+)?)%");
                if (match.Success && double.TryParse(match.Groups[1].Value,
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    progress(value);
            }
        }

        await Task.WhenAll(Consume(process.StandardOutput, false),
            Consume(process.StandardError, true), process.WaitForExitAsync(token));
        if (process.ExitCode != 0) throw new InvalidOperationException(LastError(errors.ToString()));
        progress(100);
    }

    private static void AddVideoArguments(List<string> args, DownloadOptions options)
    {
        // The height is a CEILING. The source may not offer that exact resolution.
        // Low/Medium use yt-dlp's worst/best sort to select a representative stream.
        Match match = Regex.Match(options.Quality, @"(\d+)p\)");
        int height = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        string suffix = height > 0 ? $"[height<={height}]" : "";
        bool low = options.Quality.StartsWith("Low", StringComparison.Ordinal);
        bool medium = options.Quality.StartsWith("Medium", StringComparison.Ordinal);

        string format = low
            ? $"wv*{suffix}+wa/w{suffix}"
            : $"bv*{suffix}+ba/b{suffix}";
        args.AddRange(["--format", format]);
        if (medium) args.AddRange(["--format-sort", "+size,+br"]);
        // Force an output container, including for single-file sources where a merge
        // would not otherwise happen. Remuxing copies streams when compatible.
        if (options.Format != "Auto")
            args.AddRange(["--remux-video", options.Format.ToLowerInvariant()]);
    }

    private static void AddAudioArguments(List<string> args, DownloadOptions options)
    {
        args.AddRange(["--format", "bestaudio/best", "--extract-audio"]);
        string format = options.Format.ToLowerInvariant();
        args.AddRange(["--audio-format", format]);

        if (format is "mp3" or "m4a")
        {
            // FFmpeg encoding bitrates; converting cannot improve source fidelity.
            Match match = Regex.Match(options.Quality, @"^(\d+) kbps$");
            if (match.Success)
                args.AddRange(["--postprocessor-args", $"ExtractAudio:-b:a {match.Groups[1].Value}k"]);
            else
                args.AddRange(["--audio-quality", "0"]);
        }
        else if (format is "wav" or "aiff")
        {
            Match match = Regex.Match(options.Quality, @"^(\d+) Hz / (\d+)-bit$");
            if (match.Success)
            {
                string sampleRate = match.Groups[1].Value;
                string depth = match.Groups[2].Value;
                // WAV = little endian; AIFF = big endian. FFmpeg supports PCM 8/16/24.
                string codec = (format, depth) switch {
                    ("wav", "8") => "pcm_u8", ("aiff", "8") => "pcm_s8",
                    ("wav", "16") => "pcm_s16le", ("aiff", "16") => "pcm_s16be",
                    ("wav", "24") => "pcm_s24le", ("aiff", "24") => "pcm_s24be",
                    _ => throw new ArgumentOutOfRangeException(nameof(options))
                };
                args.AddRange(["--postprocessor-args", $"ExtractAudio:-ar {sampleRate} -c:a {codec}"]);
            }
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static string LastError(string error) =>
        string.Join(Environment.NewLine, error.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(8));
}
