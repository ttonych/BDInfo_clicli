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
