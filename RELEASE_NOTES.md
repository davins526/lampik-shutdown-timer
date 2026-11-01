# Lampik v0.5.0-beta.1 — Convenience & Safety

**Windows 10/11 x64 · Russian / English · Pre-release**

## Changes
- Optional **launch with Windows** (current user only); starts hidden in the system tray.
- Scheduled timer snapshots saved locally with an atomic file replacement.
- Safe recovery: after restart, future timers wait for a manual **restore / восстанови** command. They never run automatically from a stale saved state.
- Expired or cancelled timers cannot silently shut down Windows on startup.
- Shortcut conflict validation in the glass settings screen.
- Windows CI checks for recovery and startup behavior.

## Important
- Timers still require the application to remain running and Windows to be awake.
- The installer is not code-signed.
- Save unsaved documents before trying power actions.
- This beta has automatic build and parser tests, but still requires manual Windows UI testing.
