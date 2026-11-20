# Legacy Shaiya Updater Fork

This repository is a fork of [Legacy Shaiya Updater](https://github.com/elessarshaiya/shaiya-legacy-updater).

# Legacy Shaiya Updater with Modern Core

This repository provides a legacy-style Shaiya launcher UI with a modernized runtime core for login, patch/update, repair, and SAH/ESAH handling.

The UI keeps the classic launcher look, while behavior is driven by server-side configuration (`launcher.ini`) instead of local updater INI files.

## Main features

- Legacy WPF launcher UI targeting **.NET Framework 4.8** (`x86`).
- Plain PS_UserData login through PHP + Microsoft SQL Server.
- Saved-account support using a device hash + `UserUID` (no plain password table in updater DB).
- Server-side runtime config via `ServerSide/Updater/launcher.ini` exposed by `launcher.php`.
- Patch flow with temporary `.download` file, checksum/size validation, transactional backup, rollback.
- ZIP traversal protection in patch extraction.
- Repair/verify mode based on `manifest.json` `files` entries.
- SAH/ESAH-aware update path (`data.sah` / `update.sah`).
- Integrated launch loading panel (no separate mini-loader window).
- Optional offscreen hide/restore of `game.exe` until ready.
- PT0182 server-selection automation with one-shot hook and resilient fallback/recovery behavior.

## Public-release notes

This repository was cleaned for public source sharing:

- `ServerSide/Updater/api/config.sample.php` is the public SQL config template.
- `ServerSide/Updater/api/config.php` is deployment-only (ignored by Git).
- `ServerSide/Updater/launcher.sample.ini` is documentation/example only.
- `ServerSide/Updater/launcher.ini` is deployment runtime config (ignored by Git in production setups).
- `ServerSide/Updater/launcher.php` serves **only** `launcher.ini`.
- SAH/ESAH keys in sample files/tools are public placeholders. Replace for production.

## Project layout

```txt
Updater.LegacyUI.ModernCore.sln   Main Visual Studio solution
Updater/                          WPF launcher source
Updater/Game/                     Offsets, process/window control, auto server-select
Updater/Update/                   Patch, repair, manifest, transaction, SAH/ESAH
Updater/Auth/                     Login API client and saved-account helpers
Updater/Core/                     Config, paths, INI, logging, timeout web client
ServerSide/Updater/               PHP API, launcher config, manifest, tools, patch folders
ServerSide_SQL/                   SQL scripts for saved-account support
SahCryptTool/                     C++ ESAH helper

docs/                             Install/login/cleanup/release notes
```

## Build requirements

- Windows
- Visual Studio 2022
- .NET Framework 4.8 Developer Pack
- `x86` build target

Build updater only:

```bat
build_updater_only.bat
```

Build full solution:

```bat
build.bat
```

Or open:

```txt
Updater.LegacyUI.ModernCore.sln
```

## Server deployment

Default IIS path:

```txt
C:\inetpub\wwwroot\Updater
```

Basic steps:

1. Copy `ServerSide/Updater/*` to `C:\inetpub\wwwroot\Updater`.
2. Copy `ServerSide/Updater/api/config.sample.php` to `ServerSide/Updater/api/config.php` on server.
3. Edit `config.php` with real SQL Server credentials.
4. Copy `ServerSide/Updater/launcher.sample.ini` to `ServerSide/Updater/launcher.ini`.
5. Edit `launcher.ini` with real URLs + production SAH/ESAH key.
6. Run SQL script if saved-account login is needed:

```txt
ServerSide_SQL/01_updater_auth_tables.sql
```

Security notes:

- `ServerSide/Updater/.htaccess` is production-hardened.
- Local-only endpoints are enforced both by `.htaccess` and by `ServerSide/Updater/local_only.php` (application-level guard).
- Diagnostics (`test-*.php`) and tooling (`tools/*`) are localhost-only by default.
- Runtime payload URLs (`Patches/*`, `Files/*`) remain public for launcher clients.

Test endpoints (from localhost/server):

```txt
http://127.0.0.1/Updater/test-env.php
http://127.0.0.1/Updater/test-db.php
http://127.0.0.1/Updater/test-config.php
```

Expected DB test shape:

```json
{
  "ok": true,
  "message": "Database connection OK.",
  "database": "PS_UserData"
}
```

## Patch deployment

Put patch files in:

```txt
C:\inetpub\wwwroot\Updater\Patches
```

Generate patch manifest entries:

```txt
http://127.0.0.1/Updater/tools/patch_hashes.php
```

Copy generated `patches` array into `manifest.json`.

Patch files are ZIP payloads with `.patch` extension. Data patches should include `update.sah` + `update.saf`.

## Repair deployment

Put full repair files in:

```txt
C:\inetpub\wwwroot\Updater\Files
```

Generate repair manifest entries:

```txt
http://127.0.0.1/Updater/tools/file_hashes.php
```

Copy generated `files` array into `manifest.json`.

## Active game offsets (PT0182)

Current active set (`game-pt-ps0182.exe`):

```txt
LoginPointer    = 0x007C48FC
PatchLoginFlow1 = 0x004D4EBF
PatchLoginFlow2 = 0x004D1F5F
PatchLoginFlow3 = 0x004D0DCD
```

EP5 fallback offsets remain commented in `Updater/Game/GameOffsets.cs`.

## PT0182 automatic server selection (current behavior)

After credential injection, launcher attempts direct server selection by:

1. Waiting until PT0182 server-list state is initialized.
2. Finding `CSelectServer` object in process memory.
3. Writing selected index fields.
4. Installing one-shot hook to execute original select block once.

Active values:

```txt
CSelectServer vtable       = 0x007519B4
SelectedIndex offset #1    = +0x0BD8
SelectedIndex offset #2    = +0x1E98
Global state pointer       = 0x022EED30
Server list offset         = +0x0270
Server count offset        = +0x0274
One-shot branch            = 0x0050CC01
Original select block      = 0x0050CCCE
```

### Resilience/recovery logic

- If server-list pointer remains `0`, launcher can early-exit attempts (`AutoSelectServerObjectMissingAssumeSkipAttempts`).
- On this condition, launcher can perform one recovery cycle (reinjection + second auto-select attempt).
- If still not ready, launcher falls back to manual server selection.

## Skip server selection mode (`CONFIG.INI`)

Local client file can force skip mode:

```ini
[ADVANCED]
SKIPSERVERSELECTION=1
```

In this mode launcher:

- skips memory-based auto-select hook,
- waits for TCP readiness (`30800 -> 30810`) with skip timeout,
- if timeout occurs, performs one recovery cycle,
- if still not ready, reveals manual login state cleanly.

## Launcher-integrated loading flow

When game starts, launcher keeps showing an internal loading panel while `game.exe` runs login/server state.

By default game window is moved offscreen and restored when flow is considered ready.

## Runtime configuration keys (`[Game]`)

```ini
Executable=game.exe
Arguments=start game
RequireOriginalClientFiles=false
BlockPatchWhenGameRunning=true

AutoSelectServer=true
AutoSelectServerDelayMs=2500
AutoSelectServerIndex=0
AutoSelectServerRetries=30
AutoSelectServerRetryDelayMs=500
AutoSelectServerObjectMissingAssumeSkipAttempts=6
AutoSelectRetryWhenServerListMissing=true
AutoSelectRetryDelayMs=1200

LoginTcpPort=30800
GameTcpPort=30810
TcpPortTransitionDelayMs=2000
TcpPortTransitionTimeoutMs=30000
SkipServerSelectionTcpTimeoutMs=12000

HideLoginScreens=true
LoginOverlayText=Connecting to the server...
LoginOverlaySubText=Preparing character selection. Please wait.
LoginOverlayRevealDelayMs=6000
LoginOverlayFallbackRevealDelayMs=1000
SkipServerSelectionRevealDelayMs=2000
LoginFlowPatchRestoreDelayMs=2000

HideGameWindowUntilReady=true
GameWindowHideMode=Offscreen
GameWindowHideWaitMs=8000
GameWindowOffscreenX=-32000
GameWindowOffscreenY=-32000
```

```ini
[SahCrypt]
Enabled=true
UnlockPatchSah=true
ProtectDataSahAfterPatch=true
ProtectDataSahAfterGameExit=true
; KeyHex / KeyText / KeyBase64 / Key (one format only)
```

## Logging behavior

- **Debug build**: detailed `DEBUG/INFO/WARN/ERROR` diagnostics are written (including technical details and stack traces when available).
- **Release build**: logging is intentionally minimal and sanitized for security:
  - only `ERROR` entries are persisted,
  - messages are generic (`Operation failed. See support documentation.`),
  - only exception type is kept (example: `ExceptionType=InvalidOperationException`),
  - no stack trace / sensitive internals are written.

## SahCrypt key locations (must match)

For ESAH workflows, keep server/runtime and tool keys aligned:

- Server runtime key: `ServerSide/Updater/launcher.ini` -> `[SahCrypt]` (`KeyHex` preferred).
- Public template: `ServerSide/Updater/launcher.sample.ini` -> `[SahCrypt]`.
- C++ tool key: `SahCryptTool/main.cpp` -> `kFixedKey`.

If keys are different, `SahCryptTool` may encrypt/decrypt correctly for itself but launcher runtime decryption can fail.

## Automatic SahCrypt key generation on build (Visual Studio)

You can regenerate and align SahCrypt keys automatically on each build.

Use this pre-build command in either project (or both):

```cmd
powershell -ExecutionPolicy Bypass -File "$(SolutionDir)scripts\generate-sahcrypt-key.ps1"
```

> Important: configure the pre-build script in **only one** project (`Updater` or `SahCryptTool`) for normal solution builds.
> If both projects execute it in the same build batch, the key is regenerated twice.
> The second execution overwrites the first key, which can cause confusion when testing/deploying synchronized runtime/tool keys.

### Option A: `Updater` project

1. Right-click project `Updater` -> `Properties`.
2. Open `Build Events` -> `Pre-build event command line`.
3. Paste the command above.
4. Save and build.

### Option B: `SahCryptTool` project

1. Right-click project `SahCryptTool` -> `Properties`.
2. Open `Build Events` -> `Pre-Build Event` -> `Command Line`.
3. Paste the command above.
4. Ensure **Use In Build** is set to **Yes**.
5. Save and build.

What the script updates:

- `SahCryptTool/main.cpp` (`kFixedKey`)
- `ServerSide/Updater/launcher.ini` (`[SahCrypt] KeyHex`)

Expected build output lines:

- `SahCrypt key generated and applied.`
- `Updated: ...SahCryptTool\main.cpp`
- `Updated: ...ServerSide\Updater\launcher.ini`

This keeps runtime and tool keys aligned without manual edits.

## Additional docs

- `docs/INSTALL.md`
- `docs/PLAIN_LOGIN.md`
- `docs/CLEANUP_CHANGELOG.md`
- `docs/PUBLIC_RELEASE_CHECKLIST.md`
- `ServerSide/Updater/README.md`
- `SahCryptTool/README.md`
