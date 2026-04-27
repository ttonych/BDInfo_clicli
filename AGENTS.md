# AGENTS.md

This repository is a long-lived personal fork of BDInfo. Treat it as a production tool even when changes are small: preserve GUI behavior, keep CLI behavior explicit, and verify report round-trips before shipping.

Report format policy lives in `docs/REPORT_FORMATS.md`. In short: keep text reports close to original BDInfo unless fixing wrong output; call out every intentional text report change in PR/release notes; treat `.bdinfo` as an internal application snapshot format; target compressed JSON as the canonical Snapshot v2 format; treat XML as deprecated/best-effort proof-of-concept compatibility.

## Operating Rules

- Work from a clean, named branch for every task. Use `codex/<short-task-name>` unless the user asks for another branch name.
- Do not commit directly to `UHD_Support` for normal work. Open a pull request back into `UHD_Support`.
- Never rewrite or discard user changes. If the worktree is dirty, inspect it first and keep unrelated edits intact.
- Keep changes scoped. Avoid broad refactors unless the task is specifically a refactor.
- Prefer small, reviewable commits. One bug fix or workflow change per commit is the default.
- Do not push until local verification has passed and the user has agreed to publish the branch.

## Build Commands

This is a classic .NET Framework WinForms project. Use Visual Studio MSBuild, not `dotnet build`, for authoritative local builds.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' BDInfo.sln /t:Restore /p:Configuration=Release /p:Platform='Any CPU'
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' BDInfo.sln /p:Configuration=Release /p:Platform='Any CPU' /m
```

If MSBuild is not at that path, locate it with:

```powershell
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
```

## Required Verification

For any code change:

```powershell
git diff --check
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' BDInfo.sln /p:Configuration=Release /p:Platform='Any CPU' /m
& .\BDInfo\bin\Release\BDInfo.exe --help
& .\scripts\smoke-report-roundtrip.ps1 -BuildOutputPath .\BDInfo\bin\Release
& .\scripts\smoke-hdr10plus-carryover.ps1 -BuildOutputPath .\BDInfo\bin\Release
```

For CLI/report/export changes, also run a real-disc smoke when a decrypted BD root is available, for example `V:\`:

```powershell
& .\scripts\smoke-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00107 -ChartFormat jpg
```

For HDR10+ scanner changes, also run a real HDR10+ disc smoke when sample media is available:

```powershell
& .\scripts\smoke-hdr10plus-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00800 -ChartFormat jpg
```

When verifying the HDR10+ carryover bug with manual disc swapping, keep BDInfo running in the same process:

1. Open the GUI and scan an HDR10+ title, for example `V:\` / `00800.MPLS`.
2. Confirm the first report shows `HDR10+`.
3. Without closing BDInfo, swap to a non-HDR10+ disc and scan a known non-HDR10+ playlist.
4. Export the second report and confirm it does not contain `HDR10+`.
5. Record both disc/playlist IDs in the PR notes.

The same-process swap can also be checked through the local PowerShell smoke. This script loads `BDInfo.exe` once and runs both scans through `ConsoleRunner` in that same PowerShell process:

```powershell
& .\scripts\smoke-hdr10plus-swap-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -FirstSourcePath V:\ -FirstPlaylist 00800 -SecondSourcePath V:\ -WaitForDiscSwap
```

When only a non-HDR10+ disc is available, use the synthetic-first mode to seed HDR10+ state before scanning the real disc in the same process:

```powershell
& .\scripts\smoke-hdr10plus-swap-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SyntheticFirst -SecondSourcePath V:\ -SecondPlaylist 00001
```

For unattended coordination with this thread, use signal files so the smoke can pause after the first scan and continue after the disc swap:

```powershell
& .\scripts\smoke-hdr10plus-swap-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -FirstSourcePath V:\ -FirstPlaylist 00800 -SecondSourcePath V:\ -SecondPlaylist 00000 -SwapReadySignalPath .\tmp_swap\swap-ready.signal -DiscReadySignalPath .\tmp_swap\disc-ready.signal -KeepOutput
```

Clean temporary smoke output before committing.

If report fixture models change intentionally, regenerate the synthetic committed fixtures with:

```powershell
& .\scripts\smoke-report-roundtrip.ps1 -BuildOutputPath .\BDInfo\bin\Release -UpdateFixtures
```

## Review Checklist

Before finalizing a PR, verify:

- GUI launch with no arguments still opens the GUI.
- GUI launch with a single path argument still opens the GUI and loads that path/report.
- CLI mode is entered only for explicit CLI usage, such as options or source plus destination.
- Text, Snapshot v2 `bdinfo`, legacy `bdinfo-json`, legacy `bdinfo-xml`, and compressed legacy paths do not overwrite each other unexpectedly.
- Exported `.bdinfo` files and committed Snapshot v2 fixtures load back into the GUI and can regenerate reports/charts without the original disc.
- README/help text matches the actual CLI behavior.

## GitHub Workflow

- Open a PR for each task. Do not batch unrelated findings.
- Fill in `.github/pull_request_template.md`.
- Wait for GitHub Actions to pass before merging.
- Dependabot is enabled for NuGet packages and GitHub Actions. Treat its PRs like normal code changes: review the diff, wait for CI, and merge only when the update is relevant.
- Use tag pushes for releases. The build workflow attaches `BDInfo_clicli.zip` and asks GitHub to generate release notes for tags matching `v*.*.*`.
- Suggested tag format for this fork: `v0.7.6.2-clicli.N`.

Recommended GitHub repository settings:

- Protect `UHD_Support`.
- Require the `Build (BDInfo_clicli)` workflow before merging.
- Require pull requests before merging to `UHD_Support`.
- Disable force pushes on protected branches.

## Commit Style

Use concise imperative commit messages:

```text
Fix CLI report export edge cases
Add PR workflow documentation
Harden BDInfo report round-trip loading
```

In commit bodies, include the important verification commands when the change touches CLI, reports, or CI.
