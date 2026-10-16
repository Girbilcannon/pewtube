using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PewTube;

internal sealed record MediaInfo(string Title, string? Uploader, string? Duration, string? Resolution);

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
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    public async Task<MediaInfo> AnalyzeAsync(string url, CancellationToken token)
    {
        if (!HasYtDlp) throw new FileNotFoundException("Missing Tools\\yt-dlp.exe");
        using var process = new Process { StartInfo = Create(YtDlp, new[] { "--no-playlist", "--dump-single-json", "--skip-download", url }) };
        process.Start();
        using var reg = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var outputTask = process.StandardOutput.ReadToEndAsync(token);
        var errorTask = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        string output = await outputTask;
        string error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(LastError(error));
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        string? Get(string key) => root.TryGetProperty(key, out var e) && e.ValueKind is not JsonValueKind.Null ? e.ToString() : null;
        var duration = Get("duration");
        string? prettyDuration = double.TryParse(duration, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? TimeSpan.FromSeconds(d).ToString(d >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss") : null;
        return new MediaInfo(Get("title") ?? "Untitled video", Get("uploader"), prettyDuration, Get("resolution"));
    }

    public async Task DownloadAsync(string url, string folder, string quality, string container, Action<string> log, Action<double?> progress, CancellationToken token)
    {
        if (!HasYtDlp || !HasFfmpeg) throw new FileNotFoundException("Both yt-dlp.exe and ffmpeg.exe are required in the Tools folder.");
        Directory.CreateDirectory(folder);
        string format = quality switch
        {
            "2160p" => "bv*[height<=2160]+ba/b[height<=2160]",
            "1440p" => "bv*[height<=1440]+ba/b[height<=1440]",
            "1080p" => "bv*[height<=1080]+ba/b[height<=1080]",
            "720p" => "bv*[height<=720]+ba/b[height<=720]",
            _ => "bv*+ba/b"
        };
        var args = new List<string> {
            "--no-playlist", "--newline", "--progress", "--no-overwrites",
            "--ffmpeg-location", _tools,
            "--format", format,
            "--paths", folder,
            "--output", "%(title).180B [%(id)s].%(ext)s"
        };
        if (container != "Auto") args.AddRange(new[] { "--merge-output-format", container.ToLowerInvariant() });
        args.Add(url);
        using var process = new Process { StartInfo = Create(YtDlp, args) };
        process.Start();
        using var reg = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var stderr = new StringBuilder();
        async Task Consume(StreamReader reader, bool error)
        {
            while (await reader.ReadLineAsync(token) is { } line)
            {
                if (error) stderr.AppendLine(line);
                log(line);
                if (line.StartsWith("[download]", StringComparison.Ordinal))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"\[download\]\s+(\d+(?:\.\d+)?)%");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out double pct)) progress(pct);
                }
            }
        }
        await Task.WhenAll(Consume(process.StandardOutput, false), Consume(process.StandardError, true), process.WaitForExitAsync(token));
        if (process.ExitCode != 0) throw new InvalidOperationException(LastError(stderr.ToString()));
        progress(100);
    }

    private static string LastError(string error) =>
        string.Join(Environment.NewLine, error.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(7));
}
