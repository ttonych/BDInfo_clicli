# Report Pipeline Audit

This audit maps the current report code to the policy in `docs/REPORT_FORMATS.md`.

## Current Pipeline

### Text Report

Entry points:
- CLI: `CliReportWriter.WriteReports` creates a `FormReport`, calls `Generate`, and writes `FormReport.ReportText`.
- GUI: `FormReport.Generate` builds the visible text report and the export button can write the same text box content to `.txt`.

Current behavior:
- Text report generation is owned by `FormReport.Generate`.
- The text report is built directly from runtime `BDROM`, `TSPlaylistFile`, `TSStream`, and `ScanBDROMResult` objects.
- Text export is not serialized through `ReportModels`.

Policy impact:
- Keep this path stable and close to upstream/original BDInfo.
- Snapshot v2 work should not rewrite text report output unless a separate PR explicitly documents the text diff.

### Snapshot Model

Files:
- `BDInfo/Report/ReportModels.cs`
- `BDInfo/Report/ReportSerializer.cs`

Current behavior:
- `BDInfoReportData` is the root persistence DTO.
- There is no explicit envelope with `format`, `schemaVersion`, or `payloadKind`.
- XML and JSON serialize the same `BDInfoReportData` graph through `DataContractSerializer` / `DataContractJsonSerializer`.
- Runtime objects are converted into DTOs by `BDInfoReportSerializer.BuildDiscData`.
- DTOs are converted back into runtime objects by `BDInfoReportSerializer.CreateBDROM`.

Observed risks:
- The root DTO is both the payload and the format contract, which leaves no clean place for schema/version metadata.
- `StreamData` is a broad flattened DTO with video, audio, subtitle, graphics, and common fields on the base type. This is easy to serialize but creates noisy raw JSON/XML and weakens type boundaries.
- Some runtime details are persisted because they are needed for charts and round-trip, but there is no clear split between report data, chart source data, scan metadata, and diagnostics.
- `SourcePath` is saved from the scanned source, then overwritten with the loaded `.bdinfo` path during load. Snapshot v2 should separate source identity from loaded-from path, and avoid relying on local machine paths as core data.
- `ScanFileExceptionData` currently persists stack traces. That is useful for debugging, but too noisy and potentially machine-specific for a normal exchange snapshot.

### Snapshot Serialization

Current behavior:
- Raw XML and raw JSON are written directly to `.bdinfo`.
- Compressed `.bdinfo` is a ZIP archive with `report.xml` or `report.json`.
- Load detects ZIP by `PK`, then prefers `report.json`, then `report.xml`, then the first non-empty entry.
- Non-ZIP load detects JSON by the first non-whitespace byte `{` or `[`; otherwise it assumes XML.

Policy impact:
- Snapshot v2 should prefer compressed JSON and identify the format by envelope fields.
- ZIP entry selection should be stricter for Snapshot v2, because "first non-empty entry" is useful for POC compatibility but weak for a canonical format.
- Raw JSON can remain useful for development/debug output.
- XML should not receive new features unless there is a deliberate compatibility reason.

### CLI Export

Files:
- `BDInfo/CliArgumentParser.cs`
- `BDInfo/CliReportWriter.cs`
- `BDInfo/ConsoleRunner.cs`

Current behavior:
- Default CLI report format is text.
- `-r bdinfo` means Snapshot v2 compressed JSON `.bdinfo`.
- `-r bdinfo-json` and `-r json` mean legacy JSON `.json.bdinfo`.
- `-r bdinfo-xml` and `-r xml` mean legacy XML `.xml.bdinfo`.
- `-z/--compress` controls compression for legacy XML and JSON snapshot formats; Snapshot v2 is always compressed.
- Snapshot v2, legacy XML, and legacy JSON use distinct filenames when requested together.

Policy status:
- CLI `bdinfo` now matches the canonical compressed JSON policy.
- Raw JSON and XML remain explicit legacy/development exports.

### GUI Export And Load

Files:
- `BDInfo/FormReport.cs`
- `BDInfo/GuiReportExportSelection.cs`
- `BDInfo/GuiReportLoader.cs`

Current behavior:
- GUI export dialog first filter is text `.txt`.
- GUI Snapshot v2 export is available as compressed JSON `.bdinfo`.
- Legacy JSON/XML exports are available under explicit legacy labels and distinct `.json.bdinfo` / `.xml.bdinfo` suffixes.
- GUI default extension is `.txt`.
- GUI loader accepts any `.bdinfo`, then delegates format detection to `BDInfoReportSerializer.Load`.

Policy status:
- GUI default export is text.
- Snapshot export uses canonical Snapshot v2 compressed JSON.
- XML remains visible only as an explicit legacy export.

### Charts

Current behavior:
- GUI charts are generated from restored runtime objects.
- CLI chart export automates `FormChart`.
- Snapshot data includes enough stream diagnostics and playlist/stream structure for chart regeneration in current smoke coverage.

Policy impact:
- Charts should remain derived artifacts.
- Snapshot v2 must preserve chart source data, but should not treat rendered image files as report data.

### Fixtures And Smoke

Files:
- `scripts/smoke-report-roundtrip.ps1`
- `tests/fixtures/report-roundtrip`

Current behavior:
- Fixtures cover raw XML, raw JSON, compressed XML, and compressed JSON.
- The smoke script creates synthetic `BDInfoReportData` directly through PowerShell reflection.
- The smoke verifies load, runtime object reconstruction, stream diagnostics, and scan file exceptions.

Policy impact:
- Snapshot v2 fixtures should focus on canonical compressed JSON.
- Old XML/JSON POC fixtures do not need permanent compatibility guarantees.
- If XML load is retained temporarily, keep a small XML fixture only as a legacy/best-effort check.

## Recommended Implementation Plan

### PR A: Snapshot v2 DTO Envelope

Add a new envelope type around the current payload:
- `format`
- `schemaVersion`
- `payloadKind`
- `createdBy`
- `createdAt`
- `payload`

Keep the initial payload close to current `BDInfoReportData` to reduce blast radius. Do not rewrite text report output in this PR.

### PR B: Canonical JSON Save/Load

Add explicit save/load paths for Snapshot v2 compressed JSON:
- write ZIP `.bdinfo` with a deterministic JSON entry name;
- detect Snapshot v2 by envelope fields;
- keep raw JSON output available only through an explicit format name;
- keep old POC load only as best effort if it remains cheap.

### PR C: CLI Format Semantics

Change CLI semantics to match policy:
- `-r bdinfo` writes canonical compressed JSON `.bdinfo`;
- raw JSON uses an explicit name such as `bdinfo-json`;
- XML uses an explicit deprecated name if still available;
- update help, README, and smoke scripts in the same PR.

This is a user-visible CLI behavior change and must be called out in release notes.

### PR D: GUI Export Semantics

Change GUI export defaults to match policy:
- text `.txt` is the default GUI export;
- compressed JSON `.bdinfo` is the preferred snapshot export;
- XML export is deprecated, hidden, or removed by explicit decision.

This is a user-visible GUI behavior change and must be called out in release notes.

### PR E: Fixtures And Cleanup

Update test fixtures and smoke scripts:
- add Snapshot v2 compressed JSON fixture;
- remove or demote old POC fixtures;
- verify compressed and raw JSON load to equivalent data when raw JSON remains available;
- avoid blocking Snapshot v2 on old POC XML/JSON compatibility.

### PR F: DTO Size/Shape Cleanup

After Snapshot v2 is working, reduce DTO noise:
- split common stream fields from video/audio/graphics/text-specific fields where useful;
- remove fields that are not needed for report text, chart regeneration, or structured diagnostics;
- avoid persisting local paths or stack traces in normal snapshots.

Do this after Snapshot v2 semantics are covered by smoke tests.
