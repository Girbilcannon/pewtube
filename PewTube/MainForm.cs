using System.Drawing;

namespace PewTube;

internal sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(24, 26, 31);
    private static readonly Color PanelBg = Color.FromArgb(32, 35, 42);
    private static readonly Color Field = Color.FromArgb(17, 19, 24);
    private static readonly Color TextColor = Color.FromArgb(231, 235, 242);
    private static readonly Color Muted = Color.FromArgb(161, 171, 185);
    private static readonly Color Accent = Color.FromArgb(67, 145, 235);

    private readonly Downloader _downloader = new();
    private readonly TextBox _url = new();
    private readonly TextBox _folder = new();
    private readonly ComboBox _quality = new(), _container = new();
    private readonly Button _analyze = new(), _download = new(), _cancel = new(), _browse = new();
    private readonly Label _media = new(), _status = new();
    private readonly TextBox _log = new();
    private readonly ProgressBar _progress = new();
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = "PewTube";
        ClientSize = new Size(800, 625);
        MinimumSize = new Size(690, 560);
        BackColor = Bg;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label { Text = "PewTube", Font = new Font("Segoe UI Semibold", 23, FontStyle.Bold), ForeColor = TextColor, AutoSize = true, Location = new Point(26, 18) };
        var subtitle = new Label { Text = "Simple, high-quality video downloads", ForeColor = Muted, AutoSize = true, Location = new Point(29, 64) };
        Controls.Add(title); Controls.Add(subtitle);

        var card = new Panel { BackColor = PanelBg, Location = new Point(24, 103), Size = new Size(752, 315), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        Controls.Add(card);
        AddLabel(card, "VIDEO URL", 17, 14);
        StyleBox(_url, 18, 44, 598, "Paste a video URL...");
        _url.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(_url);
        StyleButton(_analyze, "Analyze", 626, 43, 107, true);
        _analyze.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        card.Controls.Add(_analyze);
        _media.Text = "Paste a URL and click Analyze to inspect the video.";
        _media.ForeColor = Muted;
        _media.AutoEllipsis = true;
        _media.Location = new Point(19, 88);
        _media.Size = new Size(713, 40);
        _media.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(_media);
        AddLabel(card, "QUALITY", 18, 135);
        AddLabel(card, "CONTAINER", 386, 135);
        StyleCombo(_quality, new[] { "Best available", "2160p", "1440p", "1080p", "720p" }, 18, 160, 341);
        StyleCombo(_container, new[] { "Auto", "MKV", "MP4", "WebM" }, 386, 160, 346);
        card.Controls.Add(_quality); card.Controls.Add(_container);
        AddLabel(card, "SAVE TO", 18, 205);
        StyleBox(_folder, 18, 232, 596, null);
        _folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        _folder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(_folder);
        StyleButton(_browse, "Browse", 625, 231, 107, false);
        _browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        card.Controls.Add(_browse);
        StyleButton(_download, "Download Video", 18, 270, 175, true);
        card.Controls.Add(_download);
        StyleButton(_cancel, "Cancel", 201, 270, 92, false);
        _cancel.Enabled = false;
        card.Controls.Add(_cancel);

        _status.Text = ToolStatus();
        _status.ForeColor = Muted;
        _status.Location = new Point(28, 432);
        _status.Size = new Size(740, 23);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_status);
        _progress.Location = new Point(27, 462);
        _progress.Size = new Size(746, 14);
        _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_progress);
        _log.Location = new Point(27, 490);
        _log.Size = new Size(746, 112);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BackColor = Field;
        _log.ForeColor = Muted;
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = new Font("Consolas", 9);
        Controls.Add(_log);

        _browse.Click += (_, _) => Browse();
        _analyze.Click += async (_, _) => await Analyze();
        _download.Click += async (_, _) => await Download();
        _cancel.Click += (_, _) => _cts?.Cancel();
    }

    private string ToolStatus() => !_downloader.HasYtDlp ? "Missing Tools\\yt-dlp.exe — see README" : !_downloader.HasFfmpeg ? "Missing Tools\\ffmpeg.exe — see README" : "Ready";
    private static void AddLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label { Text = text, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 9), AutoSize = true, Location = new Point(x, y) });
    }
    private static void StyleBox(TextBox box, int x, int y, int width, string? placeholder)
    {
        box.Location = new Point(x, y); box.Size = new Size(width, 29);
        box.BackColor = Field; box.ForeColor = TextColor; box.BorderStyle = BorderStyle.FixedSingle;
        if (placeholder != null) box.PlaceholderText = placeholder;
    }
    private static void StyleButton(Button button, string label, int x, int y, int width, bool primary)
    {
        button.Text = label; button.Location = new Point(x, y); button.Size = new Size(width, 30);
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? Accent : Field;
        button.ForeColor = TextColor; button.Cursor = Cursors.Hand;
    }
    private static void StyleCombo(ComboBox box, string[] options, int x, int y, int width)
    {
        box.DropDownStyle = ComboBoxStyle.DropDownList; box.Location = new Point(x, y);
        box.Width = width; box.BackColor = Field; box.ForeColor = TextColor;
        box.Items.AddRange(options); box.SelectedIndex = 0;
    }
    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _folder.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) _folder.Text = dialog.SelectedPath;
    }
    private void Log(string line)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => { if (!IsDisposed) { _log.AppendText(line + Environment.NewLine); } });
    }
    private void SetProgress(double? pct)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => { if (!IsDisposed && pct is { } p) _progress.Value = Math.Clamp((int)p, 0, 100); });
    }
    private static bool ValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    private void Busy(bool state)
    {
        _analyze.Enabled = !state; _download.Enabled = !state; _cancel.Enabled = state;
    }
    private async Task Analyze()
    {
        var url = _url.Text.Trim();
        if (!ValidUrl(url)) { MessageBox.Show(this, "Enter a valid http(s) video URL."); return; }
        using var cts = new CancellationTokenSource();
        _cts = cts; Busy(true); _status.Text = "Analyzing...";
        try
        {
            var info = await _downloader.AnalyzeAsync(url, cts.Token);
            _media.Text = $"{info.Title}   •   {info.Uploader ?? "Unknown"}   •   {info.Duration ?? "Unknown duration"}";
            _status.Text = "Video analyzed successfully.";
        }
        catch (OperationCanceledException) { _status.Text = "Canceled."; }
        catch (Exception ex) { _status.Text = "Analysis failed."; Log(ex.Message); MessageBox.Show(this, ex.Message, "PewTube", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _cts = null; Busy(false); }
    }
    private async Task Download()
    {
        var url = _url.Text.Trim();
        if (!ValidUrl(url)) { MessageBox.Show(this, "Enter a valid http(s) video URL."); return; }
        if (string.IsNullOrWhiteSpace(_folder.Text)) { MessageBox.Show(this, "Select a download folder."); return; }
        using var cts = new CancellationTokenSource();
        _cts = cts; Busy(true); _progress.Value = 0; _status.Text = "Downloading...";
        try
        {
            await _downloader.DownloadAsync(url, _folder.Text, _quality.Text, _container.Text, Log, SetProgress, cts.Token);
            _status.Text = "Download complete.";
            Log("Download complete.");
        }
        catch (OperationCanceledException) { _status.Text = "Canceled."; Log("Download canceled. Partial files may remain."); }
        catch (Exception ex) { _status.Text = "Download failed."; Log(ex.Message); MessageBox.Show(this, ex.Message, "PewTube", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _cts = null; Busy(false); }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
