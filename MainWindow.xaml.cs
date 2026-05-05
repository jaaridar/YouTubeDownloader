using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YouTubeDownloader;

public partial class MainWindow : Window
{
    private readonly string _downloadFolder;
    private Process? _currentProcess;
    private bool _isDownloading;
    private CancellationTokenSource? _fetchCts;
    private string? _currentThumbnailUrl;

    public MainWindow()
    {
        InitializeComponent();

        _downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
        Directory.CreateDirectory(_downloadFolder);

        // Set end time defaults
        EndH.Text = "0";
        EndM.Text = "1";
        EndS.Text = "0";
    }

    // ── Window Controls ────────────────────────

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, MouseButtonEventArgs e)
    {
        _currentProcess?.Kill(entireProcessTree: true);
        Close();
    }

    // ── URL Input ──────────────────────────────

    private void Url_GotFocus(object sender, RoutedEventArgs e)
    {
        if (UrlTextBox.Text == "Paste YouTube URL here...")
        {
            UrlTextBox.Text = "";
            UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0xe0, 0xe0, 0xe0));
        }
    }

    private void Url_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
        {
            UrlTextBox.Text = "Paste YouTube URL here...";
            UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0x9e, 0x9e, 0x9e));
        }
    }

    private void Url_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!_isDownloading) BeginDownload();
        }
    }

    private async void Url_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounced auto-fetch: 1.2s after user stops typing
        _fetchCts?.Cancel();
        _fetchCts = new CancellationTokenSource();
        var token = _fetchCts.Token;

        string url = UrlTextBox.Text.Trim();
        if (url.Length < 15 || url == "Paste YouTube URL here...")
        {
            PreviewCard.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            await Task.Delay(1200, token);
            if (!token.IsCancellationRequested)
                await FetchVideoInfo(url);
        }
        catch (TaskCanceledException) { }
    }

    // ── Format Change ──────────────────────────

    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Show/hide compatibility bar based on format
        var tag = GetSelectedTag(FormatCombo);
        CompatBar.Visibility = tag == "video+audio" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Section Toggle ─────────────────────────

    private void Section_Changed(object sender, RoutedEventArgs e)
    {
        TimeSection.Visibility = SectionToggle.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── Number-only Input ──────────────────────

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");
    }

    // ── Open Folder ────────────────────────────

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _downloadFolder,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show($"Downloads saved to:\n{_downloadFolder}", "Downloads Folder");
        }
    }

    // ── Fetch Video Info (Auto-Preview) ────────

    private async Task FetchVideoInfo(string url)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            TitleLabel.Text = "⏳  Fetching video info...";
            MetaLabel.Text = "";
            SizeLabel.Text = "";
        });

        try
        {
            var psi = new ProcessStartInfo("yt-dlp",
                $"--dump-json --no-warnings \"{url}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            string json = "";
            if (proc != null)
            {
                json = await proc.StandardOutput.ReadToEndAsync();
                proc.WaitForExit(15000);
            }

            if (string.IsNullOrEmpty(json))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TitleLabel.Text = "Ready 🚀";
                    MetaLabel.Text = "Could not fetch video info — check URL";
                    PreviewCard.Visibility = Visibility.Collapsed;
                });
                return;
            }

            string title = ExtractJsonString(json, "title") ?? "Unknown";
            string uploader = ExtractJsonString(json, "uploader") ?? ExtractJsonString(json, "channel") ?? "Unknown";
            string? durStr = ExtractJsonString(json, "duration");
            string duration = "";
            int durationSec = 0;
            if (durStr != null && double.TryParse(durStr, out double ds))
            {
                durationSec = (int)ds;
                duration = FormatSeconds(durationSec);
            }
            string? sizeStr = ExtractJsonString(json, "filesize") ?? ExtractJsonString(json, "filesize_approx");
            long? totalBytes = null;
            if (sizeStr != null && long.TryParse(sizeStr, out long fs))
                totalBytes = fs;

            // Get thumbnail
            string? thumbUrl = ExtractJsonString(json, "thumbnail");

            // Update UI
            await Dispatcher.InvokeAsync(() =>
            {
                VideoTitleLabel.Text = title;
                VideoMetaLabel.Text = $"📺 {uploader}  ·  ⏱️ {duration}";
                VideoSizeLabel.Text = totalBytes.HasValue ? $"💾 {FormatBytes(totalBytes.Value)}" : "";
                PreviewCard.Visibility = Visibility.Visible;
                TitleLabel.Text = $"🎬  {title}";
                MetaLabel.Text = uploader != null ? $"📺 {uploader}  ·  ⏱️ {duration}" : "";
                if (totalBytes.HasValue)
                    SizeLabel.Text = $"💾 Estimated: {FormatBytes(totalBytes.Value)}";

                // Set end time to video duration
                EndH.Text = (durationSec / 3600).ToString();
                EndM.Text = ((durationSec % 3600) / 60).ToString();
                EndS.Text = (durationSec % 60).ToString();
            });

            // Download thumbnail in background
            if (!string.IsNullOrEmpty(thumbUrl))
            {
                _currentThumbnailUrl = thumbUrl;
                await DownloadThumbnail(thumbUrl);
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TitleLabel.Text = "Ready 🚀";
                MetaLabel.Text = $"Error: {ex.Message}";
                PreviewCard.Visibility = Visibility.Collapsed;
            });
        }
    }

    private async Task DownloadThumbnail(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var bytes = await client.GetByteArrayAsync(url);

            await Dispatcher.InvokeAsync(() =>
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                ThumbnailImage.Source = bitmap;
            });
        }
        catch { /* thumbnail is optional */ }
    }

    // ── Download ───────────────────────────────

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        BeginDownload();
    }

    private async void BeginDownload()
    {
        if (_isDownloading) return;

        string url = UrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url) || url == "Paste YouTube URL here...")
        {
            MessageBox.Show("Please enter a YouTube URL!", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _isDownloading = true;
        DownloadBtn.IsEnabled = false;
        DownloadBtn.Content = "⏳  Downloading...";
        TitleLabel.Text = "🚀  Starting download...";
        MetaLabel.Text = "";
        SizeLabel.Text = "";
        ResetProgress();

        try
        {
            string quality = GetQualityFormat();
            string format = GetSelectedTag(FormatCombo);
            bool compatMode = CompatMode.IsChecked == true;
            bool section = SectionToggle.IsChecked == true;

            await Task.Run(() => RunDownload(url, quality, format, compatMode, section));
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TitleLabel.Text = "❌  Download failed";
                MetaLabel.Text = ex.Message.Length > 80
                    ? ex.Message[..80] + "..."
                    : ex.Message;
            });
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                _isDownloading = false;
                DownloadBtn.IsEnabled = true;
                DownloadBtn.Content = "⬇  DOWNLOAD";
            });
        }
    }

    private string GetSelectedTag(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Tag?.ToString() ?? "";
        return "";
    }

    private string GetQualityFormat()
    {
        // Map combo index to yt-dlp format string
        return QualityCombo.SelectedIndex switch
        {
            0 => "bestvideo[height<=4320]+bestaudio",
            1 => "bestvideo[height<=2160]+bestaudio",
            2 => "bestvideo[height<=1440]+bestaudio",
            3 => "bestvideo[height<=1080]+bestaudio",
            4 => "bestvideo[height<=720]+bestaudio",
            5 => "bestvideo[height<=480]+bestaudio",
            6 => "bestvideo[height<=360]+bestaudio",
            7 => "bestvideo+bestaudio",
            _ => "bestvideo+bestaudio"
        };
    }

    private void RunDownload(string url, string quality, string format, bool compatMode, bool section)
    {
        // Build yt-dlp arguments
        string formatArg;

        if (format == "audio")
        {
            // Audio only: best audio, extract to MP3
            formatArg = $"--format bestaudio --extract-audio --audio-format mp3 --audio-quality 0 --postprocessor-args \"-id3v2_version 3\"";
        }
        else if (format == "video")
        {
            // Video only (no audio)
            formatArg = $"--format \"bestvideo\"";
        }
        else
        {
            // Video + Audio
            if (compatMode)
            {
                // H.264 + AAC = After Effects compatible
                // Use best video with H.264 codec + best audio with AAC
                formatArg = $"--format \"bestvideo[vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[vcodec^=h264]+bestaudio[acodec^=aac]/best[protocol^=https][vcodec^=avc1]\" "
                          + $"--merge-output-format mp4 --recode-video mp4 ";
            }
            else
            {
                // Standard: use quality selector
                string fmtStr;
                if (string.IsNullOrEmpty(quality) || quality == "bestvideo+bestaudio")
                    fmtStr = "bestvideo+bestaudio/best";
                else
                    fmtStr = quality;
                formatArg = $"--format \"{fmtStr}\" ";

                formatArg += $"--merge-output-format mp4 ";
            }
        }

        var args = $"{formatArg}--progress --newline --no-warnings "
                 + $"--output \"{_downloadFolder}/%(title)s.%(ext)s\" ";

        if (section)
        {
            string startTime = $"{Pad(StartH.Text)}:{Pad(StartM.Text)}:{Pad(StartS.Text)}";
            string endTime = $"{Pad(EndH.Text)}:{Pad(EndM.Text)}:{Pad(EndS.Text)}";

            args += $"--download-sections \"*{startTime}-{endTime}\" "
                  + $"--force-keyframes-at-cuts ";
        }

        args += $"\"{url}\"";

        // Start yt-dlp process
        var psi = new ProcessStartInfo("yt-dlp", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        _currentProcess = process;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) ParseProgressLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) ParseProgressLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        _currentProcess = null;

        if (process.ExitCode == 0)
        {
            Dispatcher.Invoke(() =>
            {
                TitleLabel.Text = "✅  Download completed successfully!";
                MetaLabel.Text = $"Saved to: {_downloadFolder}";
                ProgressFill.Width = ProgressFrame.ActualWidth;
                ProgressText.Text = "100%";
                SpeedLabel.Text = "⚡ Done";
                EtaLabel.Text = "";
                SizeProgressLabel.Text = "";
            });
        }
        else
        {
            Dispatcher.Invoke(() =>
            {
                TitleLabel.Text = "❌  Download failed";
                MetaLabel.Text = "Check the URL and try again. If using After Effects mode, try standard mode.";
            });
        }
    }

    // ── Progress Parsing ───────────────────────

    private void ParseProgressLine(string line)
    {
        var match = Regex.Match(line,
            @"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+)([KMG]iB)\s+at\s+([\d.]+)([KMG]iB)/s\s+ETA\s+(\S+)");

        if (!match.Success)
        {
            var doneMatch = Regex.Match(line, @"\[download\]\s+100%");
            if (doneMatch.Success)
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressFill.Width = ProgressFrame.ActualWidth;
                    ProgressText.Text = "100%";
                });
            }
            return;
        }

        double percent = double.Parse(match.Groups[1].Value);
        double sizeVal = double.Parse(match.Groups[2].Value);
        string sizeUnit = match.Groups[3].Value;
        double speedVal = double.Parse(match.Groups[4].Value);
        string speedUnit = match.Groups[5].Value;
        string eta = match.Groups[6].Value;

        double totalMb = sizeUnit switch
        {
            "KiB" => sizeVal / 1024.0,
            "MiB" => sizeVal,
            "GiB" => sizeVal * 1024.0,
            _ => sizeVal
        };

        double downloadedMb = totalMb * percent / 100.0;

        double speedMbps = speedUnit switch
        {
            "KiB" => speedVal / 1024.0,
            "MiB" => speedVal,
            "GiB" => speedVal * 1024.0,
            _ => speedVal
        };

        Dispatcher.Invoke(() =>
        {
            double pw = Math.Max(0, ProgressFrame.ActualWidth * percent / 100.0);
            ProgressFill.Width = pw;
            ProgressText.Text = $"{percent:F0}%";
            SpeedLabel.Text = $"⚡ {speedMbps:F2} MB/s";
            EtaLabel.Text = $"⏳ {eta}";
            SizeProgressLabel.Text = $"📦 {downloadedMb:F1} / {totalMb:F1} MB";
        });
    }

    // ── Helpers ────────────────────────────────

    private void ResetProgress()
    {
        ProgressFill.Width = 0;
        ProgressText.Text = "0%";
        SpeedLabel.Text = "⚡ ---";
        EtaLabel.Text = "⏳ ---";
        SizeProgressLabel.Text = "📦 ---";
    }

    private static string? ExtractJsonString(string json, string key)
    {
        var match = Regex.Match(json, $@"""{key}"":\s*""((?:[^""\\]|\\.)*)""");
        if (match.Success) return match.Groups[1].Value;
        match = Regex.Match(json, $@"""{key}"":\s*([\d.]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string Pad(string val)
    {
        if (int.TryParse(val, out int n))
            return n.ToString("D2");
        return "00";
    }

    private static string FormatSeconds(int totalSeconds)
    {
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return $"{h}:{m:D2}:{s:D2}";
    }

    private static string FormatBytes(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        if (mb >= 1024) return $"{mb / 1024.0:F2} GB";
        return $"{mb:F2} MB";
    }
}