# Lampik v0.6.0-beta.1 — Safety and Quick Cancel

**Pre-release · Windows 10/11 x64 · Russian / English**

## What's new / Что нового
- **Large, prominent Cancel Timer button** remains visible in both the regular and compact window whenever a timer is active. No need to minimize Lampik or open the tray to cancel.
- **Non-modal countdown warning** during the last 60 seconds with large **Cancel** and **+10 minutes** buttons. The dialog remains available even when the main window is hidden.
- **Closing the countdown warning cancels** the scheduled power action.
- **Safer execution policy:** if the machine wakes or the app resumes more than five seconds after the scheduled deadline, the power action is discarded rather than performed late.
- Bilingual labels for the warning and delay actions.
- Additional automated tests for warning timing, snooze, late execution and WPF warning construction.

## Safety notes
The regular text command and hotkey workflows remain available. Windows shutdown still uses `shutdown.exe` without `/f`; save unsaved work before testing.

Automated Windows compilation and tests are required before this pre-release is published. Manual Windows testing of sleep, hibernation, installer removal and 100/125/150% DPI is still necessary.
