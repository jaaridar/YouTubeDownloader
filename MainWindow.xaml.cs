using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YouTubeDownloader;

public partial class MainWindow : Window
{
    private readonly string _downloadFolder;
    private Process? _currentProcess;
    private bool _isDownloading;

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
            BeginDownload();
        }
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
            await Task.Run(() => RunDownload(url));
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
                DownloadBtn.Content = "⬇  DOWNLOAD HIGH QUALITY";
            });
        }
    }

    private void RunDownload(string url)
    {
        // First: get video info using yt-dlp --dump-json
        string? videoTitle = null;
        string? uploader = null;
        string? duration = null;
        long? totalBytes = null;

        try
        {
            var infoArgs = $"--dump-json --no-warnings --format \"bestvideo+bestaudio/best\" \"{url}\"";
            var infoPsi = new ProcessStartInfo("yt-dlp", infoArgs)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var infoProc = Process.Start(infoPsi);
            string json = infoProc?.StandardOutput.ReadToEnd() ?? "";
            infoProc?.WaitForExit(15000);

            // Parse JSON fields manually (avoids needing Newtonsoft.Json)
            videoTitle = ExtractJsonString(json, "title");
            uploader = ExtractJsonString(json, "uploader") ?? ExtractJsonString(json, "channel");
            string? durStr = ExtractJsonString(json, "duration");
            if (durStr != null && double.TryParse(durStr, out double durSec))
                duration = FormatSeconds((int)durSec);

            string? sizeStr = ExtractJsonString(json, "filesize");
            if (sizeStr == null) sizeStr = ExtractJsonString(json, "filesize_approx");
            if (sizeStr != null && long.TryParse(sizeStr, out long fs))
                totalBytes = fs;
        }
        catch { /* info is optional */ }

        // Build yt-dlp arguments
        var args = $"--format \"bestvideo+bestaudio/best\" "
                 + $"--merge-output-format mp4 "
                 + $"--progress --newline --no-warnings "
                 + $"--output \"{_downloadFolder}/%(title)s.%(ext)s\" ";

        if (SectionToggle.IsChecked == true)
        {
            string startTime = $"{Pad(StartH.Text)}:{Pad(StartM.Text)}:{Pad(StartS.Text)}";
            string endTime = $"{Pad(EndH.Text)}:{Pad(EndM.Text)}:{Pad(EndS.Text)}";

            args += $"--download-sections \"*{startTime}-{endTime}\" "
                  + $"--force-keyframes-at-cuts "
                  + $"--output \"{_downloadFolder}/%(title)s_section.%(ext)s\" ";
        }

        args += $"\"{url}\"";

        // Update UI with video info
        Dispatcher.Invoke(() =>
        {
            TitleLabel.Text = videoTitle != null
                ? $"🎬  {videoTitle}"
                : "⏳  Downloading...";
            MetaLabel.Text = uploader != null
                ? $"📺  {uploader}  ·  ⏱️  {duration ?? "?"}"
                : "";
            if (totalBytes.HasValue)
                SizeLabel.Text = $"💾  Estimated size: {FormatBytes(totalBytes.Value)}";
        });

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
            if (e.Data != null)
                ParseProgressLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                ParseProgressLine(e.Data);
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
                MetaLabel.Text = "Check the URL and try again.";
            });
        }
    }

    private void ParseProgressLine(string line)
    {
        // yt-dlp progress line format:
        // [download]  45.2% of ~10.50MiB at  5.23MiB/s ETA 00:01
        var match = Regex.Match(line,
            @"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+)([KMG]iB)\s+at\s+([\d.]+)([KMG]iB)/s\s+ETA\s+(\S+)");

        if (!match.Success)
        {
            // [download] 100%  (finished)
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

        // Convert to MB for display
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
            double progressWidth = ProgressFrame.ActualWidth * percent / 100.0;
            ProgressFill.Width = progressWidth;
            ProgressText.Text = $"{percent:F1}%";
            SpeedLabel.Text = $"⚡ {speedMbps:F2} MB/s";
            EtaLabel.Text = $"⏳ {eta}";
            SizeProgressLabel.Text = $"📦 {downloadedMb:F1} MB / {totalMb:F1} MB";
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
        // Simple JSON field extraction without external dependencies
        var match = Regex.Match(json, $@"""{key}"":\s*""((?:[^""\\]|\\.)*)""");
        if (match.Success)
            return match.Groups[1].Value;

        // Try number value
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
        if (mb >= 1024)
            return $"{mb / 1024.0:F2} GB";
        return $"{mb:F2} MB";
    }
}