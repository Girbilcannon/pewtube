namespace PewTube;

/// <summary>The selected media type determines which formats and qualities the UI offers.</summary>
internal enum MediaType { VideoWithAudio, AudioOnly }

/// <summary>Immutable snapshot of the user's download preferences.</summary>
internal sealed record DownloadOptions(MediaType Type, string Format, string Quality);

/// <summary>
/// Central catalog: adding a format or quality requires changing this file, rather than
/// duplicating option strings in the form and the downloader.
/// </summary>
internal static class DownloadCatalog
{
    public static readonly string[] VideoFormats = ["Auto", "MP4", "MKV", "WebM"];
    public static readonly string[] AudioFormats = ["MP3", "WAV", "AIFF", "M4A"];
    public static readonly string[] VideoQualities = [
        "Best Available", "High (best up to 4320p)", "High (best up to 2160p)",
        "High (best up to 1440p)", "High (best up to 1080p)", "High (best up to 720p)",
        "Medium (best up to 1080p)", "Medium (best up to 720p)", "Medium (best up to 480p)",
        "Low (best up to 480p)", "Low (best up to 360p)", "Low (best up to 240p)"
    ];
    public static string[] FormatsFor(MediaType type) =>
        type == MediaType.AudioOnly ? AudioFormats : VideoFormats;

    public static string[] QualitiesFor(MediaType type, string format) => type switch
    {
        MediaType.VideoWithAudio => VideoQualities,
        _ when format is "WAV" or "AIFF" => PcmQualities,
        _ when format == "MP3" => Mp3Qualities,
        _ when format == "M4A" => M4aQualities,
        _ => ["Best Available"]
    };

    public static readonly string[] Mp3Qualities = [
        "Best Available", "320 kbps", "256 kbps", "224 kbps", "192 kbps", "160 kbps",
        "128 kbps", "96 kbps", "64 kbps"
    ];
    public static readonly string[] M4aQualities = [
        "Best Available", "256 kbps", "192 kbps", "160 kbps", "128 kbps", "96 kbps", "64 kbps"
    ];
    public static readonly string[] PcmQualities = [
        "Best Available", "48000 Hz / 8-bit", "48000 Hz / 16-bit", "48000 Hz / 24-bit",
        "44100 Hz / 8-bit", "44100 Hz / 16-bit", "44100 Hz / 24-bit"
    ];
}
