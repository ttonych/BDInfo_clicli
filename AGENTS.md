# AGENTS.md

This repository is a long-lived personal fork of BDInfo. Treat it as a production tool even when changes are small: preserve GUI behavior, keep CLI behavior explicit, and verify report round-trips before shipping.

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

Clean temporary smoke output before committing.

## Review Checklist

Before finalizing a PR, verify:

- GUI launch with no arguments still opens the GUI.
- GUI launch with a single path argument still opens the GUI and loads that path/report.
- CLI mode is entered only for explicit CLI usage, such as options or source plus destination.
- Text, XML `.bdinfo`, JSON `.bdinfo`, and compressed `.bdinfo` exports do not overwrite each other unexpectedly.
- Exported `.bdinfo` files load back into the GUI and can regenerate reports/charts without the original disc.
- README/help text matches the actual CLI behavior.

## GitHub Workflow

- Open a PR for each task. Do not batch unrelated findings.
- Fill in `.github/pull_request_template.md`.
- Wait for GitHub Actions to pass before merging.
- Dependabot is enabled for NuGet packages and GitHub Actions. Treat its PRs like normal code changes: review the diff, wait for CI, and merge only when the update is relevant.
- Use tag pushes for releases. The build workflow attaches `BDInfo_clicli.zip` to tags matching `v*.*.*`.
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
