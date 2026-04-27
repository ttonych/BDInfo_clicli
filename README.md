> **Disclaimer**
> This fork was assembled for personal use by **Codex (OpenAI)** — not by a professional programmer. It may contain mistakes or rough edges. **Use at your own risk.**

# BDInfo_clicli

BDInfo_clicli forks **https://github.com/UniqProject/BDInfo** (tag v0.7.6.2_1b https://github.com/UniqProject/BDInfo/releases/tag/v0.7.6.2_1b). UniqProject/BDInfo itself is not the original BDInfo—the original project is from CinemaSquid: http://www.cinemasquid.com/blu-ray/tools/bdinfo

- **Export & reload .bdinfo reports** (export via **CLI** and **GUI**; open/reload in **GUI**)
- **Report snapshots: legacy XML/JSON today, Snapshot v2 compressed JSON as the canonical direction** (see [`docs/REPORT_FORMATS.md`](docs/REPORT_FORMATS.md))
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
  -c, --charts[=FORMAT]      Save all charts as image (png by default; also jpg, bmp, gif, tiff).
  -r, --report               Choose report formats (txt, bdinfo, bdinfo-json, bdinfo-xml). Use commas for multiple.
                             bdinfo writes Snapshot v2 compressed JSON; bdinfo-json/xml are legacy exports.
  -z, --compress             Compress legacy bdinfo-json/bdinfo-xml reports using ZIP.
```

---

### 2) Export & reload reports (.bdinfo)
You can now **export a BDInfo report to a single `.bdinfo` file** and later **open it in the GUI** to browse playlists, streams, and **see charts as if you just scanned the disc**.
CLI `-r bdinfo` now writes Snapshot v2 compressed JSON. The legacy proof-of-concept **JSON** and **XML** snapshot formats remain available through explicit CLI format names and the current GUI export dialog. The GUI export dialog also supports a plain text report (`.txt`).

- **Export locations**: available in **GUI** (button **“Export Report…”**) and in the **CLI** (use `-r` / `--report`).
- **GUI export defaults**: the suggested `.bdinfo` filename is based on the disc volume label. Empty or fully unsafe labels fall back to `BDINFO.bdinfo`.
- **CLI export formats**: `txt`, `bdinfo` (Snapshot v2 compressed JSON), `bdinfo-json` (legacy JSON), and `bdinfo-xml` (legacy XML).
- **GUI export formats today**: **XML `.bdinfo`**, **JSON `.bdinfo`**, **compressed XML `.bdinfo`**, **compressed JSON `.bdinfo`**, and **text `.txt`** are available from the export dialog.
- **Open/reload**: supported **only in the GUI**. Click **Load Report...** and select a previously saved `.bdinfo` file. BDInfo detects Snapshot v2 compressed JSON, legacy JSON/XML, and ZIP-compressed legacy `.bdinfo` reports.
- **Use case**: scan once on a server/headless box, then send the `.bdinfo` to someone who can open it in the GUI and review details/charts without access to the disc.

Format direction:
- Text `.txt` is the legacy human-readable report and should stay close to original BDInfo output.
- `.bdinfo` is an application snapshot format, not a public stable API.
- Snapshot v2 is versioned compressed JSON with a `snapshot.json` entry and explicit `format`, `schemaVersion`, and `payloadKind` markers.
- Legacy XML/JSON `.bdinfo` export remains available during the transition, but XML is deprecated and should not be extended without a specific compatibility need.
- Charts are derived visual artifacts, not the source of truth for reports.

See [`docs/REPORT_FORMATS.md`](docs/REPORT_FORMATS.md) for the full policy.

---

### 3) HDR10+ carryover bug fix
Fixed an issue where loading an HDR10+ title first could cause a subsequent non‑HDR10+ title to still show **HDR10+** unless the app was restarted. The flag is now reset and kept in sync with the scanner when creating new streams, preventing the label from “leaking” between different streams.

**Scope:** This change addresses **only** this upstream issue: [UniqProject/BDInfo #50 — "Issue with HDR10+ implementation"](https://github.com/UniqProject/BDInfo/issues/50).

---

## Requirements
- Windows 7 or newer
- Blu‑ray BD‑ROM drive (or a decrypted disc/folder already on disk)
- .NET Framework **4.8**

> **BDInfo will not work on copy‑protected discs.** Decrypt commercial Blu‑ray movies before scanning.

---

## Build from source
1. Open `BDInfo.sln` in Visual Studio, or build with Visual Studio MSBuild.
2. Target **.NET Framework 4.8**.
3. Build the **BDInfo** solution.

Authoritative local build commands:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' BDInfo.sln /t:Restore /p:Configuration=Release /p:Platform='Any CPU'
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' BDInfo.sln /p:Configuration=Release /p:Platform='Any CPU' /m
```

`dotnet build` is not the supported build path for this classic .NET Framework WinForms project.

---

## Development workflow
Normal changes should be made on a short-lived branch and reviewed through a pull request into `UHD_Support`.

Required checks before publishing a branch:
- `git diff --check`
- Visual Studio MSBuild Release build
- `BDInfo.exe --help`
- `.bdinfo` round-trip smoke: `.\scripts\smoke-report-roundtrip.ps1 -BuildOutputPath .\BDInfo\bin\Release`
- HDR10+ carryover smoke: `.\scripts\smoke-hdr10plus-carryover.ps1 -BuildOutputPath .\BDInfo\bin\Release`
- Real-disc CLI smoke for CLI/report/chart changes: `.\scripts\smoke-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00107`
- Real-disc HDR10+ smoke when sample media is available: `.\scripts\smoke-hdr10plus-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00800`
- `.bdinfo` round-trip check for report serialization changes

Manual HDR10+ carryover verification requires one process: scan an HDR10+ title, swap to a known non-HDR10+ disc without closing the process, scan the non-HDR10+ playlist, then export the second report and confirm it does not contain `HDR10+`. The same-process CLI path can be checked with `.\scripts\smoke-hdr10plus-swap-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -FirstSourcePath V:\ -FirstPlaylist 00800 -SecondSourcePath V:\ -WaitForDiscSwap`; when only a non-HDR10+ disc is available, use `-SyntheticFirst -SecondSourcePath V:\ -SecondPlaylist 00001`. For noninteractive coordination, pass `-SwapReadySignalPath` and `-DiscReadySignalPath`.

Codex/agent operating rules live in [`AGENTS.md`](AGENTS.md). Pull requests should use the repository PR template.

---

## Usage overview

### GUI
1. Launch **BDInfo.exe**.
2. Select a disc or BDMV folder to scan.
3. Inspect playlists, streams, and bitrates.
4. Click **Export Report…** in the report viewer to save the current legacy **XML `.bdinfo`**, **JSON `.bdinfo`**, compressed `.bdinfo`, or text `.txt`.
5. Later, click **Load Report...** to load a saved Snapshot v2, legacy XML/JSON, or compressed `.bdinfo` and review charts without rescanning.

### CLI (headless)
Common options:
- `-l, --list` – list playlists in `<BD_PATH>`
- `-m, --mpls=VALUE` – comma‑separated playlist IDs to scan (e.g., `-m 800,801`)
- `-w, --whole` – scan **all** playlists
- `-r, --report` – choose report formats: `txt`, `bdinfo`, `bdinfo-json`, `bdinfo-xml` (comma‑separate for multiple)
- `-c, --charts[=FORMAT]` – save bitrate charts as images (default **png**; also `jpg`, `bmp`, `gif`, `tiff`)
- `-z, --compress` – compress legacy `bdinfo-json` / `bdinfo-xml` reports using ZIP; `bdinfo` is always compressed Snapshot v2

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



Original README.md UniqProject/BDInfo
======

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
<li>.NET Framework 4.8</li>
<li>Source Code</li>
</ul>

The BDInfo source code is licensed under the LGPL 2.1.<br>
The free tool <a href="http://www.microsoft.com/visualstudio/en-us/products/2010-editions/visual-csharp-express">Microsoft Visual C# 2010 Express</a> can be used to build the source code.


Known Issues
======

Occasionally inaccurate bit-depth measurement on Dolby TrueHD and DTS-HD Master audio streams.<br>
BDInfo will *NOT* function correctly with copy-protected discs. You will have to decrypt commercial Blu-ray movie discs before you will be able to gather any info.
