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

Status: in progress.

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

## Backlog

These are not first in line unless a real disc exposes a bug:

- Review old parser TODOs in `BDInfo/BDROM`.
- Investigate known codec metadata limitations inherited from upstream BDInfo.
- Consider enabling GitHub Issues if a public bug backlog becomes useful.
- Consider adding sample sanitized report fixtures if licensing and size are acceptable.
