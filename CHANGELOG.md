# Changelog

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
