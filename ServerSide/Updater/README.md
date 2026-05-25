# ServerSide Updater Runtime

This folder contains the web runtime used by the launcher:

- launcher config endpoint (`launcher.php`)
- auth API (`api/*`)
- patch/repair manifest and tooling
- patch payload folders (`Patches`, `Files`)

## Security model (production)

By default, this repository is configured with these access rules:

- **Public**: runtime endpoints required by clients (`launcher.php`, `api/auth/*`, `manifest.json`, patch/file payload URLs).
- **Localhost only**: diagnostic endpoints (`test-env.php`, `test-db.php`, `test-config.php`) and `tools/*` hash generators.
- **Always blocked**: server config files (`*.ini`, `config.php`, `config.sample.php`) and hidden/private artifacts.

These rules are enforced by:

- `ServerSide/Updater/.htaccess`
- `ServerSide/Updater/tools/.htaccess`
- `ServerSide/Updater/local_only.php` (application-level localhost guard, effective also when `.htaccess` is ignored)

## Quick start (IIS)

Recommended path:

```txt
C:\inetpub\wwwroot\Updater
```

Steps:

1. Copy this folder to IIS web root.
2. Copy `api/config.sample.php` to `api/config.php`.
3. Fill SQL credentials in `api/config.php`.
4. Copy `launcher.sample.ini` to `launcher.ini`.
5. Edit `launcher.ini` values for your environment.

## Runtime config source

`launcher.php` serves **only** `launcher.ini`.

`launcher.sample.ini` is documentation/example only and is not used at runtime.

## Sensitive files (do not commit)

- `api/config.php`
- `launcher.ini`
- any private deployment key material

## Generate a secure SahCrypt key (KeyHex)

Use a 32-byte random key (64 hex chars).

PowerShell (recommended on Windows):

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
```

Paste the result into `launcher.ini`:

```ini
[SahCrypt]
KeyHex=<64-hex-chars>
```

Notes:

- Keep only one key format active (`KeyHex` **or** `KeyText` **or** `KeyBase64` **or** `Key`).
- If you use `SahCryptTool`, keep the same 32-byte key in the tool and in `launcher.ini`.
- Replace all sample/testing keys before production.

## SahCrypt key alignment (important)

To use encrypted `data.sah` reliably, keys must match across components:

1. Server runtime key in `launcher.ini`:

   ```ini
   [SahCrypt]
   KeyHex=<64-hex-chars>
   ```

2. Optional template key field in `launcher.sample.ini` (documentation only).

3. C++ tool key in `SahCryptTool/main.cpp` (`kFixedKey`) must represent the same 32 bytes.

If these keys differ, encrypted files created by `sahcrypt.exe` may not be decryptable by launcher runtime logic.

## Baseline `launcher.ini` (`[Game]`)

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

## Diagnostics endpoints (localhost only)

Use these during setup from the server itself:

```txt
http://127.0.0.1/Updater/test-env.php
http://127.0.0.1/Updater/test-db.php
http://127.0.0.1/Updater/test-config.php
```

## Patch workflow (step-by-step)

1. Put patch payload files in:

```txt
ServerSide/Updater/Patches
```

2. Ensure each patch file uses `.patch` extension.

3. Generate patch manifest entries (localhost only):

```txt
http://127.0.0.1/Updater/tools/patch_hashes.php
```

4. Copy the generated `patches` array into `manifest.json`.

5. Set `manifest.json` `clientVersion` to the target version expected after applying all patches.

6. Deploy `manifest.json`.

Code alignment:

- Launcher applies versions sequentially from `Local_ClientVersion + 1` to `clientVersion`.
- Each patch version must exist in manifest (`patches[].version`) and be reachable by URL (`patches[].file`).

## Repair workflow (step-by-step)

1. Put full repair payload files in:

```txt
ServerSide/Updater/Files
```

2. Generate repair entries (localhost only):

```txt
http://127.0.0.1/Updater/tools/file_hashes.php
```

3. Copy the generated `files` array into `manifest.json`.

4. Deploy updated `manifest.json`.

Code alignment:

- Repair verifies each file (`size` + `sha256` when provided).
- Missing/bad files are downloaded from `files[].file` and replaced transactionally.

## Manifest minimum shape

Current runtime expects:

```json
{
  "clientVersion": 0,
  "launcherVersion": 1,
  "minLauncherVersion": 1,
  "maintenance": false,
  "maintenanceMessage": "",
  "launcherFile": "",
  "patches": [],
  "files": [],
  "news": []
}
```

## Notes

- Keep `launcher.ini` and `api/config.php` server-local.
- Replace sample SAH/ESAH keys before production.
- If skip mode (`SKIPSERVERSELECTION=1`) is unstable, tune `SkipServerSelectionTcpTimeoutMs` first.
- `Patches` and `Files` payloads are intentionally public for launcher clients; only tooling and diagnostics are localhost-only.
