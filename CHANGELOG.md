# Changelog

## 1.10.0 - 2026-07-23

- Added an enabled-by-default option to restore system settings before explicit application exit.
- Restores desktop icon visibility, taskbar visibility, the Windows 11 context menu, and only Windows Update policy values managed by desktolls.
- Blocks exit when elevation is cancelled or post-restore validation fails, preventing silent partial cleanup.
- Keeps ordinary window close and hide-to-tray behavior unchanged.

## 1.9.0 - 2026-07-23

- Reorganized the long settings page into System, Input & Performance, and Download top-level tabs.
- Limited scrolling to the active category and reset each newly selected page to its top.
- Added stable tab dimensions, selected/hover/focus states, icons, and keyboard-accessible navigation.

## 1.8.0 - 2026-07-23

- Added a persistent switch for the Windows taskbar auto-hide state.
- Applied and verified changes through the Windows Shell appbar API without registry edits, Explorer restarts, or elevation.
- Added startup state synchronization and side-effect-free Shell interop self-tests.

## 1.7.0 - 2026-07-21

- Removed the Steam web-acceleration interface and all related proxy, certificate, hosts, route-selection, and recovery code.
- Removed the ASP.NET Core and YARP dependencies from the application.
- Confirmed that no desktolls Steam hosts entries, trusted root certificate, or local port 443 listener remained before publishing.

## 1.6.0 - 2026-07-20

- Replaced the unstable direct-hosts Steam experiment with a persistent local HTTPS reverse proxy.
- Added a desktolls-only DPAPI-protected root CA, SNI certificates, and reversible LocalMachine trust installation.
- Added a YARP proxy bound only to `127.0.0.1:443`, with an exact Steam domain allowlist and no request logging.
- Added multi-provider DoH route refresh, real upstream certificate validation, connection failover, and ten-minute route rotation.
- Added startup recovery, full hosts/certificate state validation, protected shutdown cleanup, and loopback TLS self-tests.

## 1.5.0 - 2026-07-20

- Added certificate-free Steam store, community, and Workshop web-access optimization.
- Added multi-provider DoH resolution and TLS-validated public-IP latency selection.
- Added allowlisted, tagged, reversible hosts updates through a one-time elevated helper.
- Added independent Steam groups, connectivity status, manual refresh, backups, and malformed-marker protection.

## 1.4.0 - 2026-07-19

- Added distinct global feedback sounds for physical `Ctrl+C` and `Ctrl+V` shortcuts.
- Added independent persistent switches and preview buttons for copy and paste sounds.
- Added non-blocking keyboard monitoring that does not intercept keys or read clipboard contents.

## 1.3.1 - 2026-07-18

- Added automatic file-name detection from `Content-Disposition`, redirects, MIME types, and file signatures.
- Added an option to disable automatic detection and enter a file name manually.
- Added protection against overwriting a different path if the server changes the suggested name.

## 1.3.0 - 2026-07-18

- Added the HTTP/HTTPS custom downloader.
- Added 1, 2, 4, and 8-thread Range downloads with retries and single-stream fallback.
- Added progress, speed, cancellation, temporary-file cleanup, and overwrite confirmation.

## 1.2.0 - 2026-07-18

- Added the reversible Windows automatic-update policy switch.

## 1.1.0 - 2026-07-16

- Added current-process-only working-set optimization with a configurable interval.

## 1.0.0 - 2026-07-16

- Initial release with desktop icon toggling, the Win10 context menu, startup behavior, and the configurable auto-clicker.
