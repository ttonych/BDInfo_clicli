# Manual GUI Smoke

Use this checklist for GUI behavior that cannot be fully validated by command-line smoke scripts.

## Preconditions

- Build `BDInfo.sln` in Release.
- Use a decrypted Blu-ray root when scanning a real disc, for example `V:\`.
- Keep the tested build output path and disc/playlist IDs in the PR notes.

## Automated GUI Launch Smoke

Run this first to verify that the GUI starts without arguments and with a committed Snapshot v2 `.bdinfo` fixture:

```powershell
& .\scripts\smoke-gui-launch.ps1 -BuildOutputPath .\BDInfo\bin\Release
```

This smoke only checks that the main window appears and stays alive briefly. It does not verify visual layout, export dialogs, chart rendering, or user interaction.

## Manual Snapshot v2 Export/Load Smoke

1. Launch `BDInfo.exe` with no arguments.
2. Confirm the main window opens normally.
3. Select a decrypted Blu-ray root, for example `V:\`.
4. Scan a known playlist.
5. Open the report view.
6. Click **Export Report...**.
7. Confirm the default export filter is text `.txt`.
8. Export the text report and confirm the file extension is `.txt`.
9. Export **BDInfo Snapshot v2** and confirm the file extension is `.bdinfo`.
10. Export legacy JSON and confirm the file extension is `.json.bdinfo`.
11. Export legacy XML and confirm the file extension is `.xml.bdinfo`.
12. Close BDInfo.
13. Reopen BDInfo with the exported Snapshot v2 `.bdinfo`.
14. Confirm the loaded report shows playlists and streams.
15. Open the report view and confirm the text report can be regenerated.
16. Open chart views and confirm charts render without the original disc.

## PR Notes

Record:

- BDInfo build commit.
- Disc volume label.
- Playlist ID.
- Exported file names.
- Whether text, Snapshot v2, legacy JSON, legacy XML, report reload, and charts passed.
- Any visual or usability issues observed.
