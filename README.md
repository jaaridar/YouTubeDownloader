# YouTube Downloader Pro 🎬

A modern, feature-rich Windows desktop application for downloading YouTube videos and audio. Built with **WPF (.NET 10)** and powered by **yt-dlp** + **ffmpeg**.

![App Screenshot](screenshot.png)

---

## 📋 Table of Contents

- [System Requirements](#-system-requirements)
- [Installation](#-installation)
- [First-Time Setup](#-first-time-setup)
- [How to Use](#-how-to-use)
  - [Download a Video](#1-download-a-video-step-by-step)
  - [Download Audio Only (MP3)](#2-download-audio-only-mp3)
  - [Download a Specific Section (Ultra Quality Dual Sliders)](#3-download-a-specific-section-ultra-quality-dual-sliders)
  - [After Effects Compatibility Mode](#4-after-effects-compatibility-mode)
  - [Download Video Without Audio](#5-download-video-without-audio)
- [Quality Options Explained](#-quality-options-explained)
- [Keyboard Shortcuts](#-keyboard-shortcuts)
- [Troubleshooting](#-troubleshooting)
- [Folder Structure](#-folder-structure)
- [Updating](#-updating)
- [Technical Details](#-technical-details)

---

## 💻 System Requirements

| Requirement | Details |
|-------------|---------|
| **OS** | Windows 10 (64-bit) or Windows 11 |
| **Architecture** | x64 (64-bit) |
| **RAM** | Minimum 512 MB (2 GB+ recommended for 4K/8K) |
| **Disk Space** | ~140 MB for the app + space for downloads |
| **Internet** | Required for downloading videos |
| **Dependencies** | **yt-dlp** and **ffmpeg** (bundled separately — see below) |

---

## 📦 Installation

### Step 1: Download the Application

1. Download **`YouTubeDownloader_Pro.exe`** from the release.
2. Place it in a folder of your choice (e.g., `C:\YouTube Downloader`).
3. Double-click to run — no installation required!

### Step 2: Download Required Tools (yt-dlp + ffmpeg)

The application **requires** two external tools to function:

#### Option A: Automatic Setup (Recommended)
The application looks for `yt-dlp.exe` and `ffmpeg.exe` in the same folder as the app OR in a subfolder named `ffmpeg/`. Simply download them and place them accordingly.

#### Option B: Manual Download

**Download yt-dlp.exe:**
1. Go to: [https://github.com/yt-dlp/yt-dlp/releases](https://github.com/yt-dlp/yt-dlp/releases)
2. Download **`yt-dlp.exe`** (look for the latest release, scroll to Assets).
3. Place it in the **same folder** as `YouTubeDownloader_Pro.exe`.

**Download ffmpeg.exe:**
1. Go to: [https://www.gyan.dev/ffmpeg/builds/](https://www.gyan.dev/ffmpeg/builds/)
2. Under "Release Builds", download **`ffmpeg-release-essentials.zip`**
3. Extract the zip, go into the `bin` folder, and copy **`ffmpeg.exe`**
4. Place `ffmpeg.exe` in the **same folder** as `YouTubeDownloader_Pro.exe`

### Final Folder Layout

Your application folder should look like this:

```
📁 Your Download Folder/
├── YouTubeDownloader_Pro.exe    ← The app
├── yt-dlp.exe                   ← Download engine (MUST have)
├── ffmpeg.exe                   ← Media processor (MUST have)
└── downloads/                   ← Created automatically (your videos go here)
```

---

## 🚀 First-Time Setup

1. **Run the app** — double-click `YouTubeDownloader_Pro.exe`
2. **Allow network access** — Windows Firewall may ask for permission; click **"Allow access"**
3. The app opens with a dark modern interface. You'll see a status message: **"Ready 🚀"**

> ✅ **No configuration files, no registry edits, no admin rights needed!**

---

## 🎯 How to Use

### 1. Download a Video (Step by Step)

1. **Copy a YouTube video URL** from your browser (e.g., `https://youtube.com/watch?v=...` or `https://youtu.be/...`)
2. **Paste it** into the URL text box in the app (Ctrl+V or right-click → Paste)
3. **Wait ~1.2 seconds** — the app auto-fetches the video info and shows:
   - Video thumbnail preview
   - Video title
   - Uploader/channel name
   - Duration
   - Estimated file size
4. **Select Quality** (e.g., "1080p (Full HD)" or "Best Available")
5. **Select Format** → keep it on **"🎬 Video + Audio (MP4)"**
6. **Click the green ⬇ DOWNLOAD button**
7. Watch the **live progress bar**, download speed, and ETA
8. When complete, you'll see **"✅ Download completed successfully!"**
9. **Click "📁 Open Downloads Folder"** to access your file

### 2. Download Audio Only (MP3)

1. Paste a YouTube URL (or search for a song)
2. Under **Format**, select **"🎵 Audio Only (MP3)"**
3. Select your preferred **Quality** (e.g., "Best Available" for highest quality audio)
4. Click **⬇ DOWNLOAD**
5. The app automatically extracts the audio to **high-quality MP3 (320kbps VBR)** with proper ID3 tags

### 3. Download a Specific Section (Ultra Quality Dual Sliders)

This lets you download only a portion of a video — perfect for clips, highlights, or music snippets.

1. **Paste a YouTube URL**.
2. Toggle **"✂️ DOWNLOAD SPECIFIC SECTION"** switch to **ON**.
3. Use the **Dual Range Sliders** to visually select your segment:
   - Drag the **Left Slider** to set the Start Time.
   - Drag the **Right Slider** to set the End Time.
   - A **Duration Bubble** will appear in the middle showing exactly how much you've selected.
4. **Smart Hit-Testing**: Even if the sliders overlap, the app intelligently brings the closer one to the front so you can always adjust your range.
5. Click **⬇ DOWNLOAD**.
6. The app uses **keyframe-accurate cutting** for seamless results.

> ⚠️ **Note:** Section download works best with **Video + Audio** format.

### 4. After Effects Compatibility Mode

This mode forces **H.264 video + AAC audio** codecs, which is required for importing into **Adobe After Effects** and other professional video editors. YouTube Shorts videos especially may have compatibility issues without this mode.

1. Select **"🎬 Video + Audio (MP4)"** format
2. A **compatibility bar** appears below the format selector
3. Toggle **"🔄 After Effects Compatibility"** switch to **ON**
4. Select your quality
5. Click **⬇ DOWNLOAD**
6. The output will be a standard H.264 MP4 file that imports perfectly into:
   - Adobe After Effects
   - Adobe Premiere Pro
   - DaVinci Resolve
   - Final Cut Pro
   - Any video editor

> ⚠️ **Note:** Compatibility mode may result in slightly larger files and may not find matches for all videos. If download fails, try turning this mode off.

### 5. Download Video Without Audio

1. Select **"🎬 Video Only (No Audio)"** format
2. Choose quality
3. Click **⬇ DOWNLOAD**
4. You get a video file without any audio track

---

## ⚙️ Quality Options Explained

| Option | Resolution | Use Case |
|--------|-----------|----------|
| **4320p (8K)** | 7680×4320 | Ultra-high resolution content (rare) |
| **2160p (4K)** | 3840×2160 | 4K monitors, high-quality archives |
| **1440p (2K)** | 2560×1440 | Good quality, smaller than 4K |
| **1080p (Full HD)** | 1920×1080 | ⭐ **Best balance of quality & size** |
| **720p (HD)** | 1280×720 | Standard HD, smaller files |
| **480p (SD)** | 854×480 | Smaller downloads, older content |
| **360p** | 640×360 | Minimal quality, smallest files |
| **Best Available** | Auto | Picks the highest resolution YouTube offers |

> 💡 **Tip:** 1080p is the sweet spot for most users. 4K downloads can be 1-5 GB per video.

---

## ⌨️ Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Enter** (in URL box) | Start download immediately |
| **Ctrl+V** | Paste URL |
| **Mouse drag** (on title bar) | Move the window |
| **─** button | Minimize window |
| **✕** button | Close window (also cancels active download) |

---

## 🔧 Troubleshooting

### "yt-dlp is not recognized..."
**Solution:** Make sure `yt-dlp.exe` is in the same folder as the app. Download it from [yt-dlp releases](https://github.com/yt-dlp/yt-dlp/releases).

### "Could not fetch video info"
**Solution:**
- Check your internet connection
- Make sure the YouTube URL is valid (not a playlist or Shorts URL — if using Shorts, copy the actual video URL)
- The video may be private, age-restricted, or geo-blocked

### Download fails / stuck
**Solution:**
- Check that `ffmpeg.exe` is present in the app folder
- Try **After Effects Compatibility mode** for problematic videos
- Try a lower quality setting
- The video might be a **livestream** or **premiere** (not yet available)

### "Download failed" after trying
**Solution:**
- If you're using **After Effects Compatibility mode**, try turning it off
- If you're downloading a **specific section**, try without section mode first
- Check if there's enough disk space in the `downloads/` folder
- Restart the application

### The app doesn't open
**Solution:**
- Your system may be blocking the app. Right-click `YouTubeDownloader_Pro.exe` → **Properties** → check "Unblock" if present → Apply
- Run as Administrator if needed
- Make sure you're on Windows 10 or later

### Progress bar shows but never completes
**Solution:**
- The video file might be very large (4K/8K). Be patient.
- Check the `downloads/` folder to see if a partial file exists
- If the file name ends with `.part`, the download was interrupted. Delete it and try again.

---

## 📁 Folder Structure

```
📁 App Folder/
├── YouTubeDownloader_Pro.exe        ← The main application
├── yt-dlp.exe                       ← YouTube download engine (required)
├── ffmpeg.exe                       ← Audio/video processing (required)
├── downloads/                       ← All downloaded files go here
│   ├── My Video Title.mp4           ← Example video download
│   ├── My Song Title.mp3            ← Example audio download
│   └── ...                          ← More downloads
└── README.md                        ← This file
```

The `downloads/` folder is created **automatically** when you first run the app.

---

## 🔄 Updating

### Update the App:
1. Download the latest `YouTubeDownloader_Pro.exe` from releases
2. Replace the old `.exe` with the new one
3. Your settings and `downloads/` folder are unaffected

### Update yt-dlp:
1. Download the latest `yt-dlp.exe` from [GitHub releases](https://github.com/yt-dlp/yt-dlp/releases)
2. Replace the old one in your app folder
3. Or run: `yt-dlp -U` from command line in the app folder

> 💡 **yt-dlp updates frequently** — YouTube changes often break older versions. Keep yt-dlp up to date!

---

## 🛠 Technical Details

| Aspect | Details |
|--------|---------|
| **Framework** | .NET 10 WPF |
| **Language** | C# 13 |
| **Download Engine** | yt-dlp |
| **Media Processing** | ffmpeg |
| **Output Format (Video)** | MP4 (H.264/AAC or original) |
| **Output Format (Audio)** | MP3 (320kbps) |
| **Single-file EXE** | Yes — self-contained, no runtime required |
| **Architecture** | x64 only |
| **Compression** | Compressed with native binary extraction |

### How It Works

1. You enter a YouTube URL
2. The app calls `yt-dlp --dump-json` to fetch video metadata (title, duration, thumbnail, formats)
3. The metadata populates the preview card in real-time
4. When you click download, the app builds a yt-dlp command with your selected parameters
5. yt-dlp downloads the video (and ffmpeg handles merging/encoding if needed)
6. Progress is parsed from yt-dlp's output and displayed live
7. The finished file is saved to the `downloads/` folder

---

## 📝 License

This project is for **personal and educational use only**. Respect YouTube's Terms of Service and copyright laws. Only download content you have the rights to.

- [yt-dlp License](https://github.com/yt-dlp/yt-dlp/blob/master/LICENSE)
- [ffmpeg License](https://ffmpeg.org/legal.html)

---

*Made with ❤️ using WPF, yt-dlp, and ffmpeg*