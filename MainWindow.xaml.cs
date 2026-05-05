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
    private string? _currentVideoId;
    private string? _lastDownloadedFile;
    private int _videoDurationSec;
    private bool _isSyncing;

    public MainWindow()
    {
        InitializeComponent();

        _downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
        Directory.CreateDirectory(_downloadFolder);

        Loaded += MainWindow_Loaded;
    }


    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckDependencies();
    }

    private async Task CheckDependencies()
    {
        string binFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
        bool hasFfmpeg = File.Exists(Path.Combine(binFolder, "ffmpeg.exe"));
        bool hasYtdlp = File.Exists(Path.Combine(binFolder, "yt-dlp.exe"));

        if (!hasFfmpeg || !hasYtdlp)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TitleLabel.Text = "⚙️  Initializing...";
                MetaLabel.Text = "Extracting internal components for first-time setup...";
                DownloadBtn.IsEnabled = false;
            });
            await EnsureBundledTools();
        }

        hasFfmpeg = await CheckCommandExists("ffmpeg", "-version");
        hasYtdlp = await CheckCommandExists("yt-dlp", "--version");

        await Dispatcher.InvokeAsync(() =>
        {
            FfmpegWarning.Visibility = hasFfmpeg ? Visibility.Collapsed : Visibility.Visible;
            if (!hasYtdlp)
            {
                TitleLabel.Text = "❌  Engine missing!";
                MetaLabel.Text = "Critical tools could not be initialized.";
                DownloadBtn.IsEnabled = false;
            }
            else
            {
                TitleLabel.Text = "Ready to Download";
                MetaLabel.Text = "Enter a valid YouTube URL to start the process";
                DownloadBtn.IsEnabled = true;
            }
        });
    }

    private async Task EnsureBundledTools()
    {
        await ExtractResource("ffmpeg.exe");
        await ExtractResource("yt-dlp.exe");
    }

    private async Task ExtractResource(string fileName)
    {
        try
        {
            string binFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
            Directory.CreateDirectory(binFolder);
            
            string destPath = Path.Combine(binFolder, fileName);
            if (!File.Exists(destPath))
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = $"YouTubeDownloader.{fileName}";
                
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using FileStream fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream);
                }
            }
        }
        catch { /* ignored */ }
    }

    private async Task<bool> CheckCommandExists(string cmd, string args)
    {
        try
        {
            // Check if cmd exists in PATH or locally
            string fullCmd = GetCommandPath(cmd);
            var psi = new ProcessStartInfo(fullCmd, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private string GetCommandPath(string cmd)
    {
        string binFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
        string localPath = Path.Combine(binFolder, cmd + ".exe");
        
        if (File.Exists(localPath)) return localPath;
        
        // Fallback to legacy root check
        string rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cmd + ".exe");
        if (File.Exists(rootPath)) return rootPath;
        
        return cmd; // Fallback to PATH
    }

    private void FixFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("To fix this, please install FFmpeg or ensure 'ffmpeg.exe' is in the application folder.\n\nYou can download it from ffmpeg.org.", "Fix FFmpeg", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void UpdateEngine_Click(object sender, RoutedEventArgs e)
    {
        TitleLabel.Text = "🔄  Updating Engine...";
        MetaLabel.Text = "Please wait while yt-dlp updates itself...";
        try
        {
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo(GetCommandPath("yt-dlp"), "-U")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
            });
            TitleLabel.Text = "✅  Engine Updated";
            MetaLabel.Text = "yt-dlp is now at the latest version.";
        }
        catch (Exception ex)
        {
            TitleLabel.Text = "❌  Update Failed";
            MetaLabel.Text = ex.Message;
        }
    }

    // ── Window Controls ────────────────────────

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _currentProcess?.Kill(entireProcessTree: true);
        Close();
    }

    // ── URL Input ──────────────────────────────

    private void Url_GotFocus(object sender, RoutedEventArgs e)
    {
    }

    private void Url_LostFocus(object sender, RoutedEventArgs e)
    {
    }

    private void Url_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!_isDownloading) BeginDownload();
        }
    }

    private void ClearUrl_Click(object sender, RoutedEventArgs e)
    {
        UrlTextBox.Text = "";
        ResetUI();
    }

    private void ResetUI()
    {
        PreviewCard.Visibility = Visibility.Collapsed;
        DownloadActions.Visibility = Visibility.Collapsed;
        _lastDownloadedFile = null;
        _currentVideoId = null;
        _currentThumbnailUrl = null;
        _videoDurationSec = 0;
        ResetProgress();
        TitleLabel.Text = "Ready to Download";
        MetaLabel.Text = "Enter a valid YouTube URL to start the process";
    }

    private async void Url_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PreviewCard == null) return;
        
        ClearUrlBtn.Visibility = string.IsNullOrEmpty(UrlTextBox.Text) ? Visibility.Collapsed : Visibility.Visible;

        _fetchCts?.Cancel();
        _fetchCts = new CancellationTokenSource();
        var token = _fetchCts.Token;

        string url = UrlTextBox.Text.Trim();
        if (url.Length < 10)
        {
            PreviewCard.Visibility = Visibility.Collapsed;
            TitleLabel.Text = "Ready to Download";
            MetaLabel.Text = "Enter a valid YouTube URL to start the process";
            return;
        }

        try
        {
            await Task.Delay(1000, token);
            if (!token.IsCancellationRequested)
                await FetchVideoInfo(url);
        }
        catch (TaskCanceledException) { }
    }

    // ── Section Toggle ─────────────────────────

    private void Section_Changed(object sender, RoutedEventArgs e)
    {
        if (TimeSection == null) return;
        TimeSection.Visibility = SectionToggle.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── Number-only Input ──────────────────────

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");
    }


    // ── Fetch Video Info (Auto-Preview) ────────

    private async Task FetchVideoInfo(string url)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            ResetUI();
            TitleLabel.Text = "🔍  Analyzing video...";
            MetaLabel.Text = "Fetching metadata from YouTube servers...";
        });

        try
        {
            var psi = new ProcessStartInfo(GetCommandPath("yt-dlp"),
                $"--dump-json --no-warnings --no-playlist \"{url}\"")
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
                await proc.WaitForExitAsync();
            }

            if (string.IsNullOrEmpty(json))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TitleLabel.Text = "Ready to Download";
                    MetaLabel.Text = "Invalid URL or connection issue";
                    PreviewCard.Visibility = Visibility.Collapsed;
                });
                return;
            }

            string title = ExtractJsonString(json, "title") ?? "Unknown";
            string uploader = ExtractJsonString(json, "uploader") ?? ExtractJsonString(json, "channel") ?? "Unknown";
            string? durStr = ExtractJsonString(json, "duration");
            int durationSec = 0;
            if (durStr != null && double.TryParse(durStr, out double ds)) durationSec = (int)ds;
            
            string? sizeStr = ExtractJsonString(json, "filesize") ?? ExtractJsonString(json, "filesize_approx");
            long? totalBytes = null;
            if (sizeStr != null && long.TryParse(sizeStr, out long fs)) totalBytes = fs;

            string? thumbUrl = ExtractJsonString(json, "thumbnail");

            await Dispatcher.InvokeAsync(() =>
            {
                VideoTitleLabel.Text = title;
                VideoMetaLabel.Text = $"{uploader}  •  {FormatSeconds(durationSec)}";
                VideoSizeLabel.Text = totalBytes.HasValue ? FormatBytes(totalBytes.Value) : "Unknown Size";
                PreviewCard.Visibility = Visibility.Visible;
                TitleLabel.Text = "✨  Video Found";
                MetaLabel.Text = title;

                _currentVideoId = ExtractVideoId(url);
                ThumbnailImage.Visibility = Visibility.Visible;
                DownloadActions.Visibility = Visibility.Collapsed;

                if (durationSec > 0)
                {
                    _videoDurationSec = durationSec;
                    _isSyncing = true;
                    StartSlider.Maximum = durationSec;
                    StartSlider.Value = 0;
                    EndSlider.Maximum = durationSec;
                    EndSlider.Value = durationSec;
                    UpdateTimeInputsFromSliders();
                    _isSyncing = false;
                    UpdateSliderRangeFill();
                }
            });

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
                TitleLabel.Text = "Ready to Download";
                MetaLabel.Text = $"Error: {ex.Message}";
                PreviewCard.Visibility = Visibility.Collapsed;
            });
        }
    }

    private void PlayVideo_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastDownloadedFile)) return;
        
        string cleanPath = _lastDownloadedFile.Trim('"');
        if (File.Exists(cleanPath))
        {
            Process.Start(new ProcessStartInfo(cleanPath) { UseShellExecute = true });
        }
        else
        {
            // Try to find it in the downloads folder if the path is relative
            string fallbackPath = Path.Combine(_downloadFolder, Path.GetFileName(cleanPath));
            if (File.Exists(fallbackPath))
            {
                Process.Start(new ProcessStartInfo(fallbackPath) { UseShellExecute = true });
            }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_downloadFolder))
        {
            Process.Start("explorer.exe", _downloadFolder);
        }
    }

    private string? ExtractVideoId(string url)
    {
        // Support for watch, shorts, live, embed, and shortened youtu.be links
        var match = Regex.Match(url, @"(?:v=|\/v\/|embed\/|youtu\.be\/|\/shorts\/|\/live\/)([^?&""/]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private async void DownloadThumb_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentThumbnailUrl)) return;
        try
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(_currentThumbnailUrl);
            string safeTitle = Regex.Replace(VideoTitleLabel.Text, @"[<>:""/\\|?*]", "_");
            string path = Path.Combine(_downloadFolder, safeTitle + "_thumb.jpg");
            await File.WriteAllBytesAsync(path, bytes);
            TitleLabel.Text = "✅  Thumbnail Saved";
            MetaLabel.Text = $"Saved to library: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            TitleLabel.Text = "❌  Save Failed";
            MetaLabel.Text = ex.Message;
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
        catch { }
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
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("Please enter a valid YouTube URL!", "Missing URL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isDownloading = true;
        DownloadBtn.IsEnabled = false;
        DownloadBtnText.Text = "PREPARING...";
        TitleLabel.Text = "🚀  Starting download...";
        MetaLabel.Text = "Requesting streams from server...";
        ResetProgress();

        try
        {
            int qualityIndex = 0;
            string format = "";
            bool section = false;
            string startTime = "";
            string endTime = "";

            Dispatcher.Invoke(() =>
            {
                qualityIndex = QualityCombo.SelectedIndex;
                format = GetSelectedTag(FormatCombo);
                section = SectionToggle.IsChecked == true;
                startTime = $"{StartH.Text}:{StartM.Text}:{StartS.Text}";
                endTime = $"{EndH.Text}:{EndM.Text}:{EndS.Text}";
            });

            await Task.Run(() => RunDownload(url, qualityIndex, format, section, startTime, endTime));
        }
            catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TitleLabel.Text = "❌  Download failed";
                MetaLabel.Text = ex.Message;
            });
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                _isDownloading = false;
                DownloadBtn.IsEnabled = true;
                DownloadBtnText.Text = "INITIALIZE DOWNLOAD";
            });
        }
    }

    private string GetSelectedTag(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Tag?.ToString() ?? "";
        return "";
    }

    private void RunDownload(string url, int qualityIndex, string format, bool section, string startTimeStr, string endTimeStr)
    {
        string formatArg;
        if (format == "audio")
        {
            formatArg = "--format \"bestaudio/best\" --extract-audio --audio-format mp3 --audio-quality 0 ";
        }
        else if (format == "video")
        {
            string res = qualityIndex switch {
                0 => "4320", 1 => "2160", 2 => "1440", 3 => "1080", 4 => "720", 5 => "480", _ => ""
            };
            formatArg = string.IsNullOrEmpty(res) ? "--format \"bestvideo\" " : $"--format \"bestvideo[height<={res}]\" ";
        }
        else
        {
            string res = qualityIndex switch {
                0 => "4320", 1 => "2160", 2 => "1440", 3 => "1080", 4 => "720", 5 => "480", _ => ""
            };
            string filter = string.IsNullOrEmpty(res) ? "bestvideo+bestaudio/best" : $"bestvideo[height<={res}]+bestaudio/best";
            formatArg = $"--format \"{filter}\" --merge-output-format mp4 ";
        }

        string sectionArg = "";
        if (section)
        {
            sectionArg = $"--download-sections \"*{startTimeStr}-{endTimeStr}\" --force-keyframes-at-cuts ";
        }

        // Get actual filename first
        string outTemplate = Path.Combine(_downloadFolder, "%(title)s.%(ext)s");
        _lastDownloadedFile = "";
        
        var getFilePsi = new ProcessStartInfo(GetCommandPath("yt-dlp"), $"--get-filename -o \"{outTemplate}\" \"{url}\"") {
            RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8
        };
        using (var p = Process.Start(getFilePsi)) {
            _lastDownloadedFile = p?.StandardOutput.ReadLine()?.Trim();
        }

        var args = $"{formatArg}{sectionArg}--progress --newline --no-warnings --no-playlist "
                 + $"--output \"{outTemplate}\" \"{url}\"";

        var psi = new ProcessStartInfo(GetCommandPath("yt-dlp"), args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        _currentProcess = process;

        process.OutputDataReceived += (_, e) => 
        { 
            if (e.Data != null) 
            {
                ParseProgressLine(e.Data); 
                // Capture destination file
                if (e.Data.Contains("Destination:"))
                {
                    var path = e.Data.Substring(e.Data.IndexOf("Destination:") + 12).Trim();
                    if (!path.EndsWith(".part")) _lastDownloadedFile = path;
                }
                else if (e.Data.Contains("Merging formats into"))
                {
                    var match = Regex.Match(e.Data, "\"([^\"]+)\"");
                    if (match.Success) _lastDownloadedFile = match.Groups[1].Value.Trim('"');
                }
                else if (e.Data.Contains("[download] ") && e.Data.EndsWith(".mp4"))
                {
                    // Fallback for single format downloads
                    var pathIdx = e.Data.IndexOf("Destination: ");
                    if (pathIdx >= 0) _lastDownloadedFile = e.Data.Substring(pathIdx + 13).Trim('"');
                }
            }
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) ParseProgressLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        _currentProcess = null;

        Dispatcher.Invoke(() =>
        {
            if (process.ExitCode == 0)
            {
                TitleLabel.Text = "✅  Download Successful!";
                MetaLabel.Text = "The file has been saved to your library.";
                ProgressFill.Width = 570; 
                ProgressText.Text = "100%";
                SpeedLabel.Text = "Done";
                EtaLabel.Text = "00:00";
                DownloadActions.Visibility = Visibility.Visible;
            }
            else
            {
                TitleLabel.Text = "❌  Process Error";
                MetaLabel.Text = "Something went wrong. Check your connection or FFmpeg.";
            }
        });
    }

    private void ParseProgressLine(string line)
    {
        var match = Regex.Match(line, @"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+)([KMG]iB)\s+at\s+([\d.]+)([KMG]iB)/s\s+ETA\s+(\S+)");
        if (!match.Success) return;

        double percent = double.Parse(match.Groups[1].Value);
        double sizeVal = double.Parse(match.Groups[2].Value);
        string sizeUnit = match.Groups[3].Value;
        double speedVal = double.Parse(match.Groups[4].Value);
        string speedUnit = match.Groups[5].Value;
        string eta = match.Groups[6].Value;

        double totalMb = sizeUnit switch { "KiB" => sizeVal / 1024.0, "MiB" => sizeVal, "GiB" => sizeVal * 1024.0, _ => sizeVal };
        double speedMb = speedUnit switch { "KiB" => speedVal / 1024.0, "MiB" => speedVal, "GiB" => speedVal * 1024.0, _ => speedVal };

        Dispatcher.Invoke(() =>
        {
            ProgressFill.Width = Math.Max(0, 570 * percent / 100.0);
            ProgressText.Text = $"{percent:F0}%";
            SpeedLabel.Text = $"{speedMb:F2} MB/s";
            EtaLabel.Text = eta;
            SizeProgressLabel.Text = $"{(totalMb * percent / 100.0):F1} / {totalMb:F1} MB";
        });
    }

    private void ResetProgress()
    {
        ProgressFill.Width = 0;
        ProgressText.Text = "0%";
        SpeedLabel.Text = "0.00 MB/s";
        EtaLabel.Text = "--:--";
        SizeProgressLabel.Text = "0 / 0 MB";
    }

    private static string? ExtractJsonString(string json, string key)
    {
        var match = Regex.Match(json, $@"""{key}"":\s*""((?:[^""\\]|\\.)*)""");
        if (match.Success) return match.Groups[1].Value;
        match = Regex.Match(json, $@"""{key}"":\s*([\d.]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    // ── Timeline Slider Sync ───────────────────

    private void TimePart_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing || _videoDurationSec <= 0) return;
        _isSyncing = true;
        try
        {
            int start = ParseTime($"{StartH.Text}:{StartM.Text}:{StartS.Text}");
            int end = ParseTime($"{EndH.Text}:{EndM.Text}:{EndS.Text}");
            
            StartSlider.Value = Math.Clamp(start, 0, _videoDurationSec);
            EndSlider.Value = Math.Clamp(end, 0, _videoDurationSec);
            UpdateSliderRangeFill();
        }
        finally { _isSyncing = false; }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncing || _videoDurationSec <= 0) return;
        _isSyncing = true;
        try
        {
            UpdateTimeInputsFromSliders();
            UpdateSliderRangeFill();
        }
        finally { _isSyncing = false; }
    }

    private void UpdateTimeInputsFromSliders()
    {
        int start = (int)StartSlider.Value;
        int end = (int)EndSlider.Value;

        StartH.Text = (start / 3600).ToString("D2");
        StartM.Text = ((start % 3600) / 60).ToString("D2");
        StartS.Text = (start % 60).ToString("D2");

        EndH.Text = (end / 3600).ToString("D2");
        EndM.Text = ((end % 3600) / 60).ToString("D2");
        EndS.Text = (end % 60).ToString("D2");
    }

    private void SliderGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_videoDurationSec <= 0) return;
        
        var point = e.GetPosition(StartSlider);
        double ratio = point.X / StartSlider.ActualWidth;
        double value = ratio * StartSlider.Maximum;

        double distStart = Math.Abs(value - StartSlider.Value);
        double distEnd = Math.Abs(value - EndSlider.Value);

        if (distStart < distEnd)
        {
            Panel.SetZIndex(StartSlider, 10);
            Panel.SetZIndex(EndSlider, 0);
        }
        else
        {
            Panel.SetZIndex(StartSlider, 0);
            Panel.SetZIndex(EndSlider, 10);
        }
    }

    private void SliderGrid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        // No specific reset needed for ZIndex usually, but can be added if desired
    }

    private void UpdateSliderRangeFill()
    {
        if (_videoDurationSec <= 0 || SliderRangeFill == null || StartSlider.ActualWidth <= 0) return;
        
        double startRatio = StartSlider.Value / _videoDurationSec;
        double endRatio = EndSlider.Value / _videoDurationSec;

        double left = Math.Min(startRatio, endRatio) * StartSlider.ActualWidth;
        double right = Math.Max(startRatio, endRatio) * StartSlider.ActualWidth;

        SliderRangeFill.Margin = new Thickness(left + 10, 0, 0, 0);
        SliderRangeFill.Width = Math.Max(0, right - left);

        UpdateRangeLabel(left, right);
    }

    private void UpdateRangeLabel(double left, double right)
    {
        if (DurationBubble == null) return;
        
        int diff = Math.Abs((int)EndSlider.Value - (int)StartSlider.Value);
        DurationBubbleText.Text = FormatSeconds(diff);
        
        double center = left + (right - left) / 2;
        Canvas.SetLeft(DurationBubble, center - (DurationBubble.ActualWidth / 2) + 10);
    }

    private int ParseTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        try
        {
            var parts = text.Split(':');
            int h = 0, m = 0, s = 0;
            if (parts.Length == 3) { h = int.Parse(parts[0]); m = int.Parse(parts[1]); s = int.Parse(parts[2]); }
            else if (parts.Length == 2) { m = int.Parse(parts[0]); s = int.Parse(parts[1]); }
            else if (parts.Length == 1) { s = int.Parse(parts[0]); }
            return h * 3600 + m * 60 + s;
        }
        catch { return 0; }
    }

    private static string Pad(string val) => int.TryParse(val, out int n) ? n.ToString("D2") : "00";

    private static string FormatSecondsFull(int totalSeconds)
    {
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    private static string FormatSeconds(int totalSeconds)
    {
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m}:{s:D2}";
    }

    private static string FormatBytes(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        return mb >= 1024 ? $"{mb / 1024.0:F2} GB" : $"{mb:F2} MB";
    }
}
