# Report Format Policy

This fork has two different output families:

- human-readable reports, where compatibility with original BDInfo matters;
- application snapshots, where reliable GUI reload and chart regeneration matter more than preserving early proof-of-concept file shapes.

## Format Roles

### Text Report (`.txt`)

The text report is the legacy, user-facing BDInfo report.

Policy:
- Keep the output as close to original/upstream BDInfo as practical.
- Change text output only to fix clearly wrong information, add explicitly intended data, or preserve existing behavior after a refactor.
- Any intentional text report change must be called out in the PR notes and release notes.
- For text report changes, include a before/after snippet when practical.

### BDInfo Snapshot (`.bdinfo`)

The `.bdinfo` file is an application snapshot, not a public stable API.

Current user-visible behavior during the Snapshot v2 transition:
- XML `.bdinfo` and JSON `.bdinfo` can both be exported from the current CLI/GUI paths.
- ZIP-compressed XML/JSON `.bdinfo` can both be exported from the current CLI/GUI paths.
- Snapshot v2 compressed JSON is supported by the serializer/loader and covered by fixtures.
- The GUI loader auto-detects Snapshot v2, JSON, XML, and ZIP-compressed `.bdinfo` files.

Target policy:
- The canonical snapshot format is compressed JSON `.bdinfo`.
- CLI `-r bdinfo` should mean canonical compressed JSON `.bdinfo`.
- GUI text export remains the default human-readable export.
- Raw JSON can remain available as a development/debug export.
- XML is deprecated. Do not extend XML unless a specific compatibility need is identified.
- Support for old proof-of-concept XML/JSON `.bdinfo` files is best effort only; do not block Snapshot v2 on preserving old POC shapes.

### Compressed `.bdinfo`

Compression is a storage/container choice, not a separate data model.

Policy:
- Compressed and uncompressed snapshots must load to equivalent report data.
- Compression is preferred for normal exchange/storage because raw JSON snapshots are large.
- Compression must not change report semantics.

### Charts

Charts are derived visual artifacts.

Policy:
- Charts are not a canonical report format.
- GUI chart generation comes from upstream BDInfo behavior.
- CLI chart export is automation around the existing chart generation path.
- Snapshot data should contain enough chart source data to regenerate charts without the original disc.

## Snapshot v2 Requirements

Snapshot v2 should have an explicit envelope so future versions can evolve without guessing by extension or serializer details.

Required envelope fields:
- `format`: `BDInfo_clicli`
- `schemaVersion`: integer version, starting with `2`
- `payloadKind`: `reportSnapshot`
- `createdBy`: application name/version
- `createdAt`: timestamp in a stable format
- `payload`: report snapshot data

Snapshot data should include:
- report data needed to regenerate the text report;
- chart source data needed by the GUI chart views;
- scan metadata useful for users, such as volume label, playlist names, scan time, and app version;
- non-fatal scanner warnings or diagnostics when they are useful and structured.

Snapshot data should not include:
- temporary local output paths;
- machine-specific cache paths;
- assumptions that the original disc is still available.

## Migration Plan

1. Document this policy before changing serializers.
2. Audit `ReportModels`, `ReportSerializer`, `CliReportWriter`, `GuiReportLoader`, and GUI export selection against this policy.
3. Add Snapshot v2 DTO/envelope support.
4. Make compressed JSON `.bdinfo` the canonical snapshot output.
5. Keep XML load/export only if it remains cheap; otherwise remove or hide XML export as part of a deliberate PR.
6. Update fixtures and smoke scripts for Snapshot v2.
7. Record every intentional text report change separately from snapshot-format changes.
