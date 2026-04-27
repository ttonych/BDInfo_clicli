# BDROM Parser TODO Audit

This audit covers TODOs and nearby scanner/parser risks in `BDInfo/BDROM` as of the post-release maintenance pass after `v0.7.6.2-clicli.2`.

The goal is to avoid opportunistic scanner edits without sample media. Parser changes should stay small, be backed by a fixture or a real-disc note, and avoid changing report output silently.

## Priority Order

### Done: PMT Descriptor Parsing And Parser Error Handling

Files:
- `BDInfo/BDROM/TSStreamFile.cs`

Completed in [#54](https://github.com/ttonych/BDInfo_clicli/pull/54):
- PMT stream descriptors are parsed through bounded `ES_info_length` handling.
- Malformed PMT stream-info lengths stop that PMT payload parse conservatively.
- Parser internals no longer write PMT exceptions directly to stdout.

Remaining risk:
- Descriptor data is still internal and not surfaced in reports.
- Any behavior change based on descriptor contents should be sample-driven.

### Partially Done: Playlist Stream And Chapter Completeness

Files:
- `BDInfo/BDROM/TSPlaylistFile.cs`
- `BDInfo/BDROM/TSStreamClipFile.cs`

Completed in [#53](https://github.com/ttonych/BDInfo_clicli/pull/53):
- Added defensive bounds checks around playlist chapter clip indexes.
- Added conservative bounds checks around playlist stream-entry header and payload lengths.

Risk:
- MVC stream entries are explicitly TODO in both playlist and clip metadata parsing.
- Secondary/PiP stream handling is skipped.
- Only chapter type `1` is handled; other chapter types are ignored.
- Subtitle stream coding type is read but not interpreted.
- Short trailing chapters are filtered by a simple `> 1.0` second rule.

Recommended next PRs:
- Treat MVC/PiP parsing as sample-driven work; do not infer behavior without 3D/PiP sample discs.
- Document any unsupported chapter type encountered during a real-disc smoke before changing report behavior.

### P2: Video Timing Accuracy For B-Pyramid Streams

Files:
- `BDInfo/BDROM/TSStreamFile.cs`

Risk:
- Several TODOs note missing frame reorder support for streams encoded with `b-pyramid > 0`.
- This can affect PTS/DTS deltas, bitrate windows, stream length, and chart accuracy.

Recommended next PR:
- Do not refactor timing code without representative AVC/HEVC samples.
- Add a diagnostic note or focused test harness only after a sample demonstrates the current inaccuracy.

### P2: HEVC HDR And Mastering Metadata Accuracy

Files:
- `BDInfo/BDROM/TSCodecHEVC.cs`

Risk:
- Profile string handling is incomplete.
- Mastering display color parsing is marked as sometimes off.
- HDR10+ carryover itself is now covered by automated and local smoke scripts, but broader HEVC metadata parsing remains sample-sensitive.

Recommended next PR:
- Keep HDR10+ carryover tests separate from broader HEVC metadata work.
- For mastering display metadata, compare against a trusted external parser before changing scaling or field order.

### P3: Audio Metadata Completeness

Files:
- `BDInfo/BDROM/TSCodecAC3.cs`
- `BDInfo/BDROM/TSCodecDTSHD.cs`
- `BDInfo/BDROM/TSCodecTrueHD.cs`

Risk:
- AC3 headphone mode is recognized but not used.
- DTS-HD asset parsing has incomplete branches for multiple assets and speaker activity details.
- TrueHD dialnorm is not pulled from TrueHD metadata directly.

Recommended next PR:
- Treat this as metadata-quality work unless a crash or clear wrong label is reported.
- Prefer one codec per PR with before/after report snippets from a known disc.

### P3: Interleaved File Size And SSIF Reporting

Files:
- `BDInfo/BDROM/BDROM.cs`
- `BDInfo/BDROM/TSPlaylistFile.cs`
- `BDInfo/BDROM/TSInterleavedFile.cs`

Risk:
- Stream file comparison does not use interleaved file sizes.
- SSIF/MVC video stream additions are handled by local special cases.
- This can affect sorting, estimated sizes, and 3D disc reporting.

Recommended next PR:
- Keep size/sorting changes separate from parser changes.
- Verify with a 3D/SSIF sample disc before changing the interleaved file path.

## Current Recommendation

Do not start with broad parser rewrites. The remaining scanner/parser items are sample-sensitive and should wait for a real disc, a saved report mismatch, or a crash that makes the next behavior change concrete.

The safest next maintenance work is outside parser semantics:
- improve release and verification automation,
- keep tracking docs current,
- add sample inventory notes when a relevant disc is available,
- create focused issues only when the next action is clear.

## Verification Baseline

For parser/scanner changes, run:

```powershell
git diff --check
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' BDInfo.sln /p:Configuration=Release /p:Platform='Any CPU' /m
& .\BDInfo\bin\Release\BDInfo.exe --help
& .\scripts\smoke-report-roundtrip.ps1 -BuildOutputPath .\BDInfo\bin\Release
& .\scripts\smoke-hdr10plus-carryover.ps1 -BuildOutputPath .\BDInfo\bin\Release
```

When suitable media is available, also run the relevant local smoke:

```powershell
& .\scripts\smoke-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00001 -ChartFormat jpg
& .\scripts\smoke-hdr10plus-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00800 -ChartFormat jpg
```
