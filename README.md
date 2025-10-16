> **Disclaimer**
> This fork was assembled for personal use by **Codex (OpenAI)** — not by a professional programmer. It may contain mistakes or rough edges. **Use at your own risk.**

# BDInfo_clicli

BDInfo_clicli forks **https://github.com/UniqProject/BDInfo** (tag v0.7.6.2_1b https://github.com/UniqProject/BDInfo/releases/tag/v0.7.6.2_1b). UniqProject/BDInfo itself is not the original BDInfo—the original project is from CinemaSquid: http://www.cinemasquid.com/blu-ray/tools/bdinfo

- **Export & reload .bdinfo reports** (export via **CLI** and **GUI**; open/reload in **GUI**)
- **Report formats: JSON and XML** (the `.bdinfo` file can be either; format is auto‑detected)
- **HDR10+ carryover bug fix** in video stream metadata
- A simple **command‑line (CLI) mode** for headless use

> **What BDInfo does:**
The BDInfo tool was designed to collect video and audio technical specifications from Blu-ray movie discs, including:
- Disc Size
- Playlist File Contents
- Stream Codec and Bitrate Details

---

## Highlights of this fork

### 1) CLI mode for BDInfo
Run BDInfo from the command line to scan discs/folders and export reports/charts without opening the GUI.

**CLI help**
```
Usage: BDInfo.exe <BD_PATH> [REPORT_DEST]
BD_PATH may be a directory containing a BDMV folder or a BluRay ISO file.
REPORT_DEST is the folder the BDInfo report is to be written to. If not given, the report will be written next to BDInfo.exe.

Options:
  -?, --help, -h             Print out the options.
  -l, --list                 Print the list of playlists.
  -m, --mpls=VALUE           Comma separated list of playlists to scan.
  -w, --whole                Scan whole disc - every playlist.
  -v, --version              Print the version.
  -c, --charts               Save all charts as image (select image format, default png)
  -r, --report               Choose report formats (txt, bdinfo, bdinfo-json). Use commas for multiple.
```

---

### 2) Export & reload reports (.bdinfo)
You can now **export a BDInfo report to a single `.bdinfo` file** and later **open it in the GUI** to browse playlists, streams, and **see charts as if you just scanned the disc**.
You can choose the **report format** when exporting: **JSON** or **XML**. The exported file uses the `.bdinfo` extension in both cases.

- **Export locations**: available in **GUI** (button **“Export Report…”**) and in the **CLI** (use `-r` / `--report`).
- **Open/reload**: supported **only in the GUI**. Click **Report…** and select a previously saved `.bdinfo` file. BDInfo **automatically detects** whether it is JSON or XML.
- **Use case**: scan once on a server/headless box, then send the `.bdinfo` to someone who can open it in the GUI and review details/charts without access to the disc.

---

### 3) HDR10+ carryover bug fix
Fixed an issue where loading an HDR10+ title first could cause a subsequent non‑HDR10+ title to still show **HDR10+** unless the app was restarted. The flag is now reset and kept in sync with the scanner when creating new streams, preventing the label from “leaking” between different streams.

**Scope:** This change addresses **only** this upstream issue: [UniqProject/BDInfo #50 — "Issue with HDR10+ implementation"](https://github.com/UniqProject/BDInfo/issues/50).

---

## Requirements
- Windows 7 or newer
- Blu‑ray BD‑ROM drive (or a decrypted disc/folder already on disk)
- .NET Framework **4.7.2** or later

> **BDInfo will not work on copy‑protected discs.** Decrypt commercial Blu‑ray movies before scanning.

---

## Build from source
1. Open `BDInfo.sln` in Visual Studio.
2. Target **.NET Framework 4.7.2+**.
3. Build the **BDInfo** solution.

---

## Usage overview

### GUI
1. Launch **BDInfo.exe**.
2. Select a disc or BDMV folder to scan.
3. Inspect playlists, streams, and bitrates.
4. Click **Export Report…** in the report viewer to save as **JSON** or **XML**.
5. Later, click **Report…** to load a saved `.bdinfo` and review charts without rescanning.

### CLI (headless)
Common options:
- `-l, --list` – list playlists in `<BD_PATH>`
- `-m, --mpls=VALUE` – comma‑separated playlist IDs to scan (e.g., `-m 800,801`)
- `-w, --whole` – scan **all** playlists
- `-r, --report` – choose report formats: `txt`, `bdinfo`, `bdinfo-json` (comma‑separate for multiple)
- `-c, --charts` – save bitrate charts as images (default **png**)

---

## Limitations
- Copy‑protected discs are **not supported**; decrypt first.
- Bit‑depth on some Dolby TrueHD / DTS‑HD MA streams may be occasionally inaccurate (historical BDInfo issue).

---

## License
LGPL‑2.1 (same as the original BDInfo).

---

## Credits
- Original **BDInfo** by Cinema Squid.
- Upstream fork: **UniqProject/BDInfo** (`v0.7.6.2_1b`).
- This fork: CLI mode, GUI export/reload, JSON/XML report support, HDR10+ bug fix.


======
Original README.md UniqProject/BDInfo

BDInfo
======

(source origin: http://www.cinemasquid.com/blu-ray/tools/bdinfo)
(I am not the original Author of this tool)

The BDInfo tool was designed to collect video and audio technical specifications from Blu-ray movie discs, including:

<ul>
<li>Disc Size</li>
<li>Playlist File Contents</li>
<li>Stream Codec and Bitrate Details</li>
</ul>

Requirements
======
<ul>
<li>Windows 7 or higher Operating System</li>
<li>Blu-ray BD-ROM Drive</li>
<li>.NET Framework 4.7.2 or Higher</li>
<li>Source Code</li>
</ul>

The BDInfo source code is licensed under the LGPL 2.1.<br>
The free tool <a href="http://www.microsoft.com/visualstudio/en-us/products/2010-editions/visual-csharp-express">Microsoft Visual C# 2010 Express</a> can be used to build the source code.


Known Issues
======

Occasionally inaccurate bit-depth measurement on Dolby TrueHD and DTS-HD Master audio streams.<br>
BDInfo will *NOT* function correctly with copy-protected discs. You will have to decrypt commercial Blu-ray movie discs before you will be able to gather any info.
