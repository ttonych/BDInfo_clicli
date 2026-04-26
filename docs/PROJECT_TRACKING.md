# Project Tracking

GitHub Issues are enabled for this fork. Use issues for actionable maintenance work, not for every observation or uncertain parser limitation.

## Labels

Repository-specific labels:

- `cli`: command-line interface behavior.
- `gui`: WinForms GUI behavior.
- `report`: report generation, serialization, and loading.
- `scanner`: BDROM parser and stream scanner behavior.
- `release`: release packaging and versioning.
- `needs-sample`: needs real disc or sample media to verify safely.

The default GitHub labels remain available for general triage.

## Current Issues

- [#49 Harden PMT descriptor parsing and diagnostics](https://github.com/ttonych/BDInfo_clicli/issues/49)
- [#50 Add defensive bounds checks for playlist chapter parsing](https://github.com/ttonych/BDInfo_clicli/issues/50)

Both issues came from `docs/BDROM_TODO_AUDIT.md` and should be handled as small PRs into `UHD_Support`.

## Rules

- Open an issue only when the next action is clear.
- Add `needs-sample` when a real disc or sample media is required before changing scanner behavior.
- Keep speculative codec/parser limitations in docs until a sample or report makes the problem actionable.
- Close issues from PR descriptions when the fix is merged and verified.
