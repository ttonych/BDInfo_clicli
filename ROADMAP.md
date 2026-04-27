# BDInfo_clicli Roadmap

This roadmap is the working order for future Codex-assisted maintenance. Each item should be handled as a small pull request into `UHD_Support`, following `AGENTS.md`.

## 1. Add Automated `.bdinfo` Round-Trip Smoke

Status: done.

Goal: make the fork's main feature verifiable without relying only on manual GUI checks.

Scope:
- Add a small local smoke script or test utility that loads representative `.bdinfo` files.
- Verify JSON and XML `.bdinfo` files deserialize into usable report data.
- Verify compressed `.bdinfo` files still load.
- Keep the test fixture small enough for the repository, or document why a generated fixture is used instead.

Done when:
- The check runs locally from PowerShell.
- The check is documented in `AGENTS.md` or README.
- GitHub Actions runs the check when possible.
- Manual GUI round-trip remains listed only for cases that cannot be automated yet.

## 2. Add Shared Local Smoke Scripts

Status: done.

Goal: stop relying on copied command blocks for the `V:\` real-disc smoke.

Scope:
- Add `scripts/smoke-local.ps1`.
- Support configurable source path, output path, playlist, chart format, and report formats.
- Clean temporary output safely.
- Return a non-zero exit code when reports or charts are missing.

Done when:
- The script runs the current `--list`, plain export, chart export, and compressed export smoke.
- `AGENTS.md` references the script instead of duplicating the full command block.
- The script does not require `V:\`; it accepts it as a default or parameter.

## 3. Consolidate README Files

Status: done.

Goal: remove documentation drift between `README.md` and `README_CLICLI.MD`.

Scope:
- Decide the canonical README.
- Either delete the duplicate, turn it into a short pointer, or make generation/copying explicit.
- Ensure CLI help examples match the real binary output.

Done when:
- There is one authoritative user-facing README.
- Any remaining secondary README clearly points to the canonical file.
- No CLI option is documented differently across files.

## 4. Normalize Repository Text Formatting

Status: done.

Goal: reduce noisy diffs and make future patches safer.

Scope:
- Add or update `.gitattributes`.
- Decide line-ending policy for C# and Markdown files.
- Normalize files in a dedicated formatting-only PR.
- Avoid mixing formatting normalization with behavior changes.

Done when:
- `git diff --check` is clean.
- Future `apply_patch` edits do not rewrite large unrelated chunks.
- The PR contains no behavior changes.

## 5. Split and Harden CLI Internals

Status: done.

Progress:
- Argument parsing is extracted into `CliArgumentParser`.
- CLI report writing and output naming are extracted into `CliReportWriter`.
- CLI chart format parsing and validation are extracted into `CliChartFormat`.

Goal: reduce risk in `ConsoleRunner.cs` without changing behavior.

Scope:
- Extract argument parsing into a focused helper.
- Extract report format selection and output naming into testable helpers.
- Extract chart format parsing and validation into testable helpers.
- Add narrow tests or smoke checks around each extracted behavior.

Done when:
- GUI launch rules remain unchanged.
- Existing CLI examples still work.
- XML, JSON, text, compressed reports, and chart exports keep their current filenames.
- Each refactor PR is small enough to review independently.

## 6. Strengthen GUI Report Export and Load Coverage

Status: done.

Progress:
- GUI report export default filename generation is extracted into `ReportFileNameHelper`.
- GUI `.bdinfo` load conversion is extracted into `GuiReportLoader`.
- GUI report export format selection is extracted into `GuiReportExportSelection`.
- README describes GUI export defaults, export formats, and `.bdinfo` load behavior.

Goal: protect the GUI workflows that cannot be fully validated by CLI smoke.

Scope:
- Audit GUI export defaults, overwrite behavior, and file extension handling.
- Add non-UI helpers where possible so naming and serialization behavior can be tested without opening WinForms.
- Keep manual GUI checks documented for the remaining interactive parts.

Done when:
- Export naming is deterministic for empty, missing, or unsafe volume labels.
- Load report handles JSON, XML, and compressed files consistently.
- The README describes GUI export/load behavior accurately.

## 7. Release Readiness Pass

Status: done.

Progress:
- Full local verification from `AGENTS.md` passed.
- GitHub Actions is green on `UHD_Support`.
- Release workflow created `v0.7.6.2-clicli.1` with `BDInfo_clicli.zip`.

Goal: produce a clean tagged release only after the maintenance queue is stable.

Scope:
- Run full local verification from `AGENTS.md`.
- Confirm GitHub Actions is green on `UHD_Support`.
- Confirm release workflow attaches `BDInfo_clicli.zip` on tags matching `v*.*.*`.
- Pick the next tag using `v0.7.6.2-clicli.N`.

Done when:
- The release tag exists on GitHub.
- The release has a ZIP artifact.
- Release notes summarize the CLI/report/automation fixes since the previous tag.

## Post-release Maintenance

These items should be handled after `v0.7.6.2-clicli.1`, one small PR at a time.

### 8. Recheck HDR10+ Carryover Fix

Status: done.

Progress:
- Added an automated smoke that verifies a fresh HEVC stream does not inherit HDR10+ state from a previous HEVC stream in the same process.
- Added a local real-disc HDR10+ smoke script for sample media and verified `V:\` / `00800.MPLS` exports `HDR10+` in text, XML `.bdinfo`, and JSON `.bdinfo` reports with charts.
- Added a same-process local swap smoke that scans HDR10+ media, pauses for a manual non-HDR10+ disc swap, then checks that the second report does not contain `HDR10+`.
- Added a synthetic-first same-process mode and verified it against a real non-HDR10+ disc at `V:\` / `00001.MPLS`; the second text, XML `.bdinfo`, and JSON `.bdinfo` reports did not contain `HDR10+`.
- Completed a full real-disc same-process swap: HDR10+ `V:\` / `00800.MPLS` (`ELVIS`) followed by HDR10/non-HDR10+ `V:\` / `00000.MPLS` (`THE_AFRICAN_QUEEN`), with no `HDR10+` in the second text, XML `.bdinfo`, or JSON `.bdinfo` report.

Goal: verify that the documented HDR10+ carryover fix still behaves correctly after the report/export/load refactors.

Scope:
- Find the exact stream metadata path that sets and clears HDR10+ state.
- Reproduce the original carryover scenario when possible: scan or load an HDR10+ title, then scan or load a non-HDR10+ title in the same process.
- Check both direct disc scan and saved `.bdinfo` load paths if suitable sample material is available.
- For manual disc swapping, keep one BDInfo process open: scan HDR10+ `V:\` / `00800.MPLS`, swap to a known non-HDR10+ disc, scan its playlist, export the second report, and confirm `HDR10+` is absent.
- Add a narrow automated check if the behavior can be represented without copyrighted sample media; otherwise document the manual verification steps.

Done when:
- HDR10+ does not leak from one title/stream/report into the next.
- The README claim remains accurate or is corrected.
- Any remaining manual-only requirement is written down clearly.

### 9. Add Small Report Fixtures

Status: done.

Progress:
- Added deterministic synthetic `.bdinfo` fixtures for XML, JSON, compressed XML, and compressed JSON report round-trip coverage.

Goal: move more report behavior into CI without depending on `V:\`.

Scope:
- Decide whether synthetic `.bdinfo` fixtures can cover XML, JSON, compressed XML, and compressed JSON.
- Keep fixtures small and free of real disc metadata that should not be committed.
- Extend `smoke-report-roundtrip.ps1` or add a focused script when fixtures are useful.

Done when:
- CI checks representative `.bdinfo` files without requiring a real disc.
- Local real-disc smoke remains available for scanner/chart coverage.

### 10. Audit BDROM Parser TODOs

Status: done.

Progress:
- Added `docs/BDROM_TODO_AUDIT.md` with a prioritized parser/scanner TODO list and verification guidance.

Goal: identify high-risk parser/scanner TODOs before changing inherited scanner logic.

Scope:
- Review TODOs in `BDInfo/BDROM`.
- Group them by risk: correctness, crash potential, metadata quality, and cosmetic cleanup.
- Fix only small, well-understood issues immediately; move uncertain scanner behavior into documented follow-up items.

Done when:
- There is a prioritized parser/scanner issue list.
- Any code changes are backed by local verification or explicit manual test notes.

### 11. Project Tracking

Status: done.

Progress:
- Enabled GitHub Issues for `ttonych/BDInfo_clicli`.
- Added repository labels: `cli`, `gui`, `report`, `scanner`, `release`, and `needs-sample`.
- Created actionable scanner issues #49 and #50 from `docs/BDROM_TODO_AUDIT.md`.
- Added `docs/PROJECT_TRACKING.md`.

Goal: make future maintenance easier to track outside this working thread.

Scope:
- Decide whether GitHub Issues should be enabled.
- If enabled, add labels such as `bug`, `cli`, `gui`, `report`, `scanner`, `release`, and `needs-sample`.
- Convert confirmed backlog items into issues only when they are actionable.

Done when:
- Active work has a clear tracking location.
- Non-actionable observations do not clutter the repository.

## Backlog

These are not first in line unless a real disc exposes a bug:

- Review old parser TODOs in `BDInfo/BDROM`.
- Investigate known codec metadata limitations inherited from upstream BDInfo.
- Consider enabling GitHub Issues if a public bug backlog becomes useful.
- Consider adding sample sanitized report fixtures if licensing and size are acceptable.

## Scanner Hardening And `clicli.2` Release

This phase followed the initial post-release maintenance queue. The goal was to make the smallest useful scanner hardening changes, verify them with the existing smoke scripts and available discs, then cut the next maintenance release.

### 12. Add Defensive Playlist Bounds Checks

Issue: [#50 Add defensive bounds checks for playlist chapter parsing](https://github.com/ttonych/BDInfo_clicli/issues/50)

Status: done in [#53](https://github.com/ttonych/BDInfo_clicli/pull/53).

Goal: reduce crash risk in playlist/chapter parsing without changing normal report output.

Scope:
- Harden `BDInfo/BDROM/TSPlaylistFile.cs` around chapter clip indexes and stream-entry length advances.
- Preserve current behavior for supported chapter type `1`.
- Avoid broad parser refactors.

Done when:
- Malformed or unusual chapter indexes are ignored or handled conservatively instead of throwing.
- Existing report round-trip smoke still passes.
- A real-disc smoke is run when a decrypted BD root is available.
- Issue #50 is closed by the PR.

### 13. Harden PMT Descriptor Parsing And Diagnostics

Issue: [#49 Harden PMT descriptor parsing and diagnostics](https://github.com/ttonych/BDInfo_clicli/issues/49)

Status: done in [#54](https://github.com/ttonych/BDInfo_clicli/pull/54).

Goal: make PMT descriptor handling safer and stop parser internals from writing unstructured diagnostics into console output.

Scope:
- Add bounded PMT descriptor parsing in `BDInfo/BDROM/TSStreamFile.cs`.
- Preserve current behavior for malformed or unknown descriptors.
- Avoid report output changes unless backed by sample media.
- Keep this separate from playlist/chapter hardening.

Done when:
- PMT descriptor parsing is bounded.
- Internal parser errors do not pollute CLI output.
- Existing smoke scripts pass.
- A suitable real-disc smoke is run or the remaining sample requirement is documented on #49.

### 14. Full Verification Pass

Status: done before `v0.7.6.2-clicli.2`.

Goal: verify the scanner-hardening phase before tagging.

Scope:
- Run all required checks from `AGENTS.md`.
- Run real-disc smoke on any available discs.
- Confirm GitHub Actions are green on `UHD_Support`.
- Check open issues for any regressions introduced by #50/#49.

Done when:
- Local verification is clean.
- GitHub Actions are green.
- Remaining issues are either unrelated or explicitly deferred.

Verification completed:
- `git diff --check`.
- Visual Studio MSBuild Release build.
- `BDInfo.exe --help`.
- `scripts/smoke-report-roundtrip.ps1`.
- `scripts/smoke-hdr10plus-carryover.ps1`.
- `scripts/smoke-local.ps1 -BuildOutputPath .\BDInfo\bin\Release -SourcePath V:\ -Playlist 00000 -ChartFormat jpg` on `THE_AFRICAN_QUEEN` / `00000.MPLS`.
- GitHub Actions passed on `UHD_Support`.

### 15. Release `v0.7.6.2-clicli.2`

Status: done.

Goal: publish the next maintenance build after scanner hardening.

Scope:
- Tag the next release as `v0.7.6.2-clicli.2` unless a newer versioning decision is made.
- Confirm the release workflow attaches `BDInfo_clicli.zip`.
- Write release notes covering scanner hardening, smoke coverage, fixtures, HDR10+ verification, and project tracking.

Done when:
- The tag exists on GitHub.
- The GitHub release has `BDInfo_clicli.zip`.
- Release notes are accurate and linked to the relevant PRs/issues.

Release:
- [`v0.7.6.2-clicli.2`](https://github.com/ttonych/BDInfo_clicli/releases/tag/v0.7.6.2-clicli.2)
- Includes `BDInfo_clicli.zip`.

## Report Format Policy And Snapshot v2

This phase defines the report/snapshot format direction before changing serializers. The text report remains the legacy user-facing output; `.bdinfo` becomes an internal application snapshot format with compressed JSON as the target canonical representation.

### 16. Document Report Format Policy

Status: done in [#59](https://github.com/ttonych/BDInfo_clicli/pull/59).

Goal: make the format policy explicit so future code changes do not preserve accidental proof-of-concept behavior.

Scope:
- Document text report compatibility rules.
- Document `.bdinfo` as an application snapshot, not a public stable API.
- Document compressed JSON as the target canonical snapshot format.
- Document XML as deprecated/best-effort proof-of-concept compatibility.
- Document charts as derived visual artifacts, not report source of truth.

Done when:
- `docs/REPORT_FORMATS.md` exists.
- README and `AGENTS.md` point to the policy.
- Future PRs have clear rules for text report changes and snapshot changes.

### 17. Audit Current Report Pipeline

Status: done in `docs/REPORT_PIPELINE_AUDIT.md`.

Goal: map current code to the new policy before refactoring.

Scope:
- Audit `BDInfo/Report/ReportModels.cs`.
- Audit `BDInfo/Report/ReportSerializer.cs`.
- Audit `BDInfo/CliReportWriter.cs`.
- Audit `BDInfo/GuiReportLoader.cs`.
- Audit GUI export selection and default behavior.

Done when:
- The current text-report path and snapshot path are documented.
- Serializer drift and compatibility risks are listed.
- Snapshot v2 implementation tasks are split into small PRs.

### 18. Implement Snapshot v2 Envelope

Status: done.

Goal: add an explicit snapshot envelope with schema/version metadata.

Scope:
- Add `format`, `schemaVersion`, `payloadKind`, `createdBy`, `createdAt`, and `payload`.
- Add a serializer entry point that writes compressed JSON Snapshot v2 archives.
- Keep existing CLI and GUI defaults unchanged until their dedicated follow-up items.

Done when:
- Snapshot v2 archives are versioned compressed JSON with a deterministic `snapshot.json` entry.
- Loader identifies Snapshot v2 by envelope fields, not only extension.
- Round-trip smoke covers Snapshot v2.

### 19. Update Report Fixtures And Documentation

Status: done.

Goal: make tests and user docs match Snapshot v2.

Scope:
- Regenerate or replace report round-trip fixtures.
- Update README examples and format descriptions.
- Update `AGENTS.md` verification notes if commands or expectations change.

Done when:
- CI validates Snapshot v2 round-trip.
- README no longer presents XML and JSON snapshots as equally preferred future formats.
- Any intentional text report output changes are separately documented.

### 20. Align CLI Snapshot Semantics

Status: done.

Goal: make CLI report format names match the Snapshot v2 policy.

Scope:
- Change `-r bdinfo` to write canonical compressed JSON `.bdinfo`.
- Keep raw JSON under an explicit development/debug format name.
- Keep XML only under an explicit deprecated name if XML export remains available.
- Update CLI help, README, smoke scripts, and PR/release notes.

Done when:
- CLI behavior matches `docs/REPORT_FORMATS.md`.
- Existing text report behavior is unchanged unless separately documented.
- Snapshot round-trip smoke covers canonical compressed JSON.

### 21. Align GUI Export Semantics

Status: done.

Goal: make GUI export defaults match the Snapshot v2 policy.

Scope:
- Make text `.txt` the default GUI export.
- Make compressed JSON `.bdinfo` the preferred snapshot export.
- Decide whether XML export is hidden, removed, or left as explicit deprecated export.
- Update README and manual verification notes.

Done when:
- GUI export default is text.
- Snapshot export is still available.
- User-visible behavior changes are called out in PR/release notes.

## Post Snapshot v2 Stabilization

This phase starts after Snapshot v2 is available from both CLI and GUI. The goal is to verify the user-visible workflows, prepare a maintenance release, and only then reduce snapshot payload size/noise with small, reversible changes.

### 22. Manual GUI Snapshot v2 Smoke

Status: in progress.

Goal: verify the interactive GUI workflows that local CLI smoke cannot fully cover.

Scope:
- Launch the GUI with no arguments and confirm it opens normally.
- Launch the GUI with a single disc/report path and confirm the GUI path still works.
- Scan a real disc, export text `.txt`, Snapshot v2 `.bdinfo`, legacy JSON `.json.bdinfo`, and legacy XML `.xml.bdinfo`.
- Load the exported Snapshot v2 `.bdinfo` back into the GUI and confirm playlists, streams, text report, and charts are usable without the original disc.
- Record the disc ID, playlist ID, exported file names, and any visual/manual observations in PR notes.

Done when:
- GUI default export is visibly text `.txt`.
- GUI Snapshot v2 export loads back and regenerates the report/chart views.
- Legacy JSON/XML exports are still reachable under explicit legacy labels.

Progress:
- Added `scripts/smoke-gui-launch.ps1` for no-argument and single Snapshot v2 `.bdinfo` GUI launch checks.
- Added `docs/MANUAL_GUI_SMOKE.md` for the remaining visual export/load/chart checklist.

### 23. Snapshot v2 Release Prep

Status: pending.

Goal: prepare the next maintenance release after the Snapshot v2 behavior changes.

Scope:
- Review README, `docs/REPORT_FORMATS.md`, `AGENTS.md`, and PR notes for release-note accuracy.
- Confirm local build and required smoke scripts are green on `UHD_Support`.
- Confirm GitHub Actions is green on the latest `UHD_Support`.
- Choose the next release tag, expected to be `v0.7.6.2-clicli.3` unless a newer versioning decision is made.
- Draft release notes that explicitly call out CLI/GUI report format behavior changes.

Done when:
- Release notes mention `-r bdinfo` now writes Snapshot v2 compressed JSON.
- Release notes mention GUI export now defaults to text and offers Snapshot v2 `.bdinfo`.
- The release checklist is ready for a tag push.

### 24. Audit Snapshot DTO Payload

Status: pending.

Goal: decide what data Snapshot v2 should keep before removing or reshaping fields.

Scope:
- Audit `BDInfoReportData`, `BDROMData`, `PlaylistData`, `StreamFileData`, `StreamData`, and scan diagnostics.
- Classify fields as required for text report regeneration, required for charts, useful metadata, legacy compatibility, or removable noise.
- Pay special attention to local paths, stack traces, duplicated stream fields, and fields only meaningful during a live scan.
- Document the decision before changing the DTO shape.

Done when:
- A payload audit document lists keep/remove/defer decisions.
- Each proposed removal has a verification strategy.
- Risky changes are split into separate follow-up PRs.

### 25. Trim Snapshot Local/Machine Data

Status: pending.

Goal: remove the lowest-risk machine-specific data from normal Snapshot v2 output.

Scope:
- Avoid persisting temporary output paths and machine-specific cache paths in Snapshot v2.
- Decide whether scan exception stack traces should be omitted, summarized, or kept only in debug/development exports.
- Preserve enough source identity for users to understand what disc/report was scanned.
- Keep legacy XML/JSON loader behavior best-effort and avoid broad compatibility work.

Done when:
- Snapshot v2 no longer stores clearly local-only data unless explicitly justified.
- Round-trip smoke and committed fixtures are updated.
- GUI load/report/chart behavior remains intact.

### 26. Reduce Stream DTO Noise

Status: pending.

Goal: reduce broad flattened stream payloads only after the safer Snapshot v2 cleanup is stable.

Scope:
- Split common stream fields from video/audio/text/graphics-specific data where it materially reduces noise or mistakes.
- Remove duplicated or unused fields only when report regeneration and chart generation do not need them.
- Keep text report output unchanged unless a separate PR deliberately documents the text diff.
- Measure fixture size before and after so the cleanup has evidence.

Done when:
- Snapshot v2 fixture size is reduced or the audit explains why it should not be.
- Round-trip smoke covers the changed DTO shape.
- Any old proof-of-concept XML/JSON compatibility loss is explicitly accepted or avoided.

### 27. Tag Snapshot v2 Maintenance Release

Status: pending.

Goal: publish the Snapshot v2 behavior changes once verification is complete.

Scope:
- Tag the release after manual GUI smoke and release prep are complete.
- Confirm the release workflow attaches `BDInfo_clicli.zip`.
- Confirm generated release notes are accurate, then edit them if needed.

Done when:
- The tag exists on GitHub.
- The GitHub release has `BDInfo_clicli.zip`.
- Release notes link the Snapshot v2 PRs and list user-visible report/export changes.
