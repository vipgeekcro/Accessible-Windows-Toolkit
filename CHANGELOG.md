# Changelog

All notable changes to Accessible Windows Toolkit will be documented in this file.

## [1.0.0] - 2026-10-02

### Initial Release

- First public release of Accessible Windows Toolkit.
- Introduced the initial debloating module for removing supported optional Windows applications and components.
- Added automatic system scanning so that only currently installed supported items are displayed.
- Included a catalog of 137 supported applications and components across Microsoft, Xbox, Widgets, third-party, and OEM categories.
- Added separate removal methods for AppX/MSIX packages, Microsoft Teams, WinGet applications, and OneDrive.
- Added post-removal verification to confirm whether each selected item was actually removed.
- Added batch processing that continues with remaining items if an individual removal fails.
- Added removal results showing successfully removed, failed, and already absent items.
- Added automatic removal-session logging with retention of the 10 most recent logs.
- Designed the WPF interface for full keyboard operation and screen reader accessibility, with particular attention to NVDA.
- Added accessible keyboard navigation, standard checkboxes, predictable focus handling, and keyboard shortcuts.
- Distributed as a portable, self-contained 64-bit Windows application with no separate .NET installation required.
