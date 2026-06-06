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

## SahCrypt key — dove si trova

La chiave AES-256 NON è più in `launcher.ini` (rimossa per sicurezza — era accessibile pubblicamente).

**Flusso attuale:**

```
GMSecurity.ini [SETTINGS] SahKeyHex=<64-hex>
    └─ api/config.php legge il valore
         └─ common.php get_sah_key_hex()
              └─ login.php success_response() → include "sahKey" nella risposta JSON
                   └─ client riceve la chiave solo dopo autenticazione riuscita
```

**Allineamento chiavi:**

| Componente | Dove si configura |
|---|---|
| Login server (risposta auth) | `GMSecurity.ini [SETTINGS] SahKeyHex=` |
| SahCryptTool (crea ESAH) | `SahCryptTool/main.cpp kFixedKey` |
| `launcher.ini [SahCrypt]` | Solo flag Enable/Unlock/Protect — nessuna chiave |

Le tre chiavi devono rappresentare gli stessi 32 byte. Se differiscono, `data.sah` creato da `sahcrypt.exe` non è decriptabile dal launcher.

## Generate a secure SahCrypt key (KeyHex)

Use a 32-byte random key (64 hex chars).

PowerShell (recommended on Windows):

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
```

Paste the result into `GMSecurity.ini` under `[SETTINGS]`:

```ini
[SETTINGS]
SahKeyHex=<64-hex-chars>
```

Also compile the same key into `SahCryptTool/main.cpp` (`kFixedKey`).

Do NOT put the key in `launcher.ini` — it is publicly accessible.

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

## Diagnostics endpoints (localhost only)

Use these during setup from the server itself:

```txt
http://127.0.0.1/Updater/test-env.php
http://127.0.0.1/Updater/test-db.php
http://127.0.0.1/Updater/test-config.php
```

## Patch workflow (automatico)

### Metodo raccomandato — Admin Panel web

Il modo più semplice è usare il **Patch Manager** nel pannello admin del sito:

```
Sito admin → WEBSITE → Patch Manager
```

**Procedura:**

1. Modifica i file game localmente → metti in `data/` con stesso percorso del data.sah/saf
2. Esegui `SHAIYA_MAKE.exe` localmente → ottieni `Files/data.sah` + `Files/data.saf`
3. Rinomina: `data.sah → update.sah`, `data.saf → update.saf`
4. Apri admin panel → WEBSITE → Patch Manager
5. Tab "Crea da SAH/SAF": carica `update.sah` + `update.saf`, inserisci descrizione
6. Click "Crea e Carica Patch" → fatto

Il server crea il ZIP, assegna la versione `NNNN`, aggiorna il manifest automaticamente.

**Alternativa:** se hai già il file `.patch` pronto (creato con `crea_patch.bat`), usa il tab "Upload .patch diretto".

### Metodo alternativo — Copia file manuale

**Un solo passo:** copia il file `.patch` nella cartella `Patches/` con nome numerato.

```
Patches/
  0001_initial_client.patch
  0002_fix_login.patch
  0003_new_content.patch
```

Il sistema genera automaticamente `patches[]`, `clientVersion` e `ManifestSha256` ad ogni richiesta.
Non è necessario modificare `manifest.json` né chiamare strumenti aggiuntivi.

### Naming convention (obbligatoria)

```
NNNN_descrizione.patch
```

- Prefisso numerico zero-padded = ordine di applicazione
- Sort naturale del filename = ordine versioni = version number (1-based)
- Nessun salto di versione: il client cerca v1, v2, v3... sequenzialmente

### Formato file `.patch`

ZIP contenente:
- (opzionale) `update.sah` + `update.saf` — merge nell'archivio SAH del gioco
- qualsiasi altro file — copiato nella dir del gioco con backup/rollback automatico

### Code alignment

- Client applica versioni da `Local_ClientVersion + 1` a `clientVersion` sequenzialmente.
- `clientVersion` = numero totale di `.patch` trovati in `Patches/` (auto).
- URL download: `BaseUrl + "Patches/" + filename`.
- Vedi `tools/README.md` per dettagli completi e diagnostica.

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
