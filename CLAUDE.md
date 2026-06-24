# ⚠️ SICUREZZA — REGOLA ASSOLUTA (LEGGERE PRIMA DI TUTTO)

**Gioco multiplayer mondiale → mindset produzione sempre, anche in fase di test.**

## Updater.exe = UNTRUSTED. Nessun segreto nel binario.

`Updater.exe` è distribuito nella cartella client. È un binario .NET 4.8 x86:
decompilabile con ILSpy o dnSpy in secondi, codice C# quasi identico all'originale visibile.

**Qualsiasi chiave/segreto hardcoded in Utilities.cs, AuthClient.cs, ecc. → estratta da qualsiasi giocatore.**

**Pattern obbligatorio:** Updater invia dati grezzi (es. HWID) → server PHP firma/valida con chiave da GMSecurity.ini → risponde con risultato → Updater salva localmente.
Chiavi risiedono SOLO in `GMSecurity.ini` e `config.php` sul server (mai nel binario client).

Vedi infrastruttura completa e percorsi: `C:\Users\lol\source\repo\CLAUDE.md`

---

# Istruzioni — shaiya-legacy-updater

Updater WPF (.NET 4.8, x86) che lancia `game.exe`, inietta credenziali in memoria e gestisce il login automatico per il client Shaiya EP6/EP8 custom.

---

## Struttura progetto

```
Updater/
  Core/AppConfig.cs          — configurazione caricata da launcher.php server-side
  Game/GameOffsets.cs        — indirizzi fissi nel binario game.exe (VERIFICARE con Ghidra!)
  Game/GameTcpPortTransitionWatcher.cs — rileva transizione login(30800)→game(30810)
  MemoryManager.cs           — ReadProcessMemory / WriteProcessMemory wrapper
  MainWindow.xaml.cs         — LaunchGameWorker, TryInjectCredentials, InjectCredentials
  Auth/AuthClient.cs         — login.php / login-saved.php / saved-accounts.php
  Auth/AuthModels.cs         — LoginResponse { ok, userId, userUid, passwordForGame }
  app.manifest               — requireAdministrator (necessario: game.exe è admin, stesso IL)
  Updater.csproj             — ApplicationManifest=app.manifest
ServerSide/Updater/
  launcher.php               — configurazione server-side (TCP ports, hide modes, ecc.)
  api/auth/login.php         — valida password, ritorna passwordForGame = $password (plaintext)
  api/auth/login-saved.php   — DISABILITATO: saved-account login rotto con PBKDF2 (vedi sotto)
  api/common.php             — DB helpers, check_password_safe, get_db_password_for_game
```

---

## Prerequisiti critici — Manifest requireAdministrator

**`game.exe` ha `level="requireAdministrator"` nel manifest PE (a `0x02315B24`).**  
Senza manifest admin nell'updater, `OpenProcess(PROCESS_VM_READ)` fallisce con Win32=5 (ACCESS_DENIED) perché i due processi girano a Integrity Level diversi (Medium vs High).

`Updater/app.manifest` e `<ApplicationManifest>` in `.csproj` sono necessari e non devono essere rimossi.

---

## Game offsets (game.exe) — verificati con Ghidra 2026-06-02

Binary: `C:\Users\lol\Desktop\Shaiya Core Client-10\Shaiya Core Client\game.exe`  
Image base: `0x00400000`, x86 32-bit PE

### Login struct

Il puntatore globale `0x007C48FC` punta all'oggetto C++ login screen.

| Offset | Campo | Note |
|---|---|---|
| `+0x000` | vtable | C++ vtable |
| `+0x004` | username | 32 byte ASCII null-padded |
| `+0x027` (39) | password | 32 byte ASCII null-padded |
| `+0x714` | state | 0=username, 1=password, 2=server-select |
| `+0x3213` | credentials_ready | 1 = dati da UI pronti |
| `+0x4EAC` | server_select_mode | 0=login screen, ≠0=server select |

### GameOffsets.cs — indirizzi attivi

```csharp
LoginPointer             = 0x007C48FC    // puntatore GENERICO alla schermata attiva (molti writer)
LoginVTable              = 0x0074FFC0    // CLogin::vftable; [LoginPointer]==LoginVTable => schermata login attiva
UserIdOffsetFromPointer  = 4             // +4 = username buffer
TempPasswordOffsetFromPointer = 39       // +0x27 = password buffer
UserIdBufferLength       = 32
TempPasswordBufferLength = 32

PatchLoginFlow1 = 0x004D4EBF  // original: F6 40 20 80 75 09 F6 80 A0 00 (10 bytes)
PatchLoginFlow2 = 0x004D1F5F  // original: 75 67 68 20 07 7C (6 bytes, JNZ short)
PatchLoginFlow3 = 0x004D0DCD  // original: 0F 85 B0 00 00 00 (6 bytes, JNZ near)
```

### Packet login inviato al server (opcode 0xA102, 50 byte)

```
[0x00] opcode 0xA102       (2 byte)
[0x02] username            (32 byte, null-padded)
[0x22] password            (primi 16 byte del buffer)
```

`FUN_005ec610` costruisce il packet → `FUN_005ea9a0` invia.  
Solo 16 byte di password sono inviati al server. `passwordForGame` deve essere plaintext ≥ 16 char.

### Funzioni chiave (game.exe)

| Indirizzo | Funzione | Note |
|---|---|---|
| `FUN_004d0a30` | LoginScreen::Update | Per-frame: UI text → struct+4/+0x27; Patch3 bypassa |
| `FUN_004d2b50` | LoginScreen::DoLogin | Verifica non-vuoto, copia a net buf, blocca su msg loop |
| `FUN_005a4d30` | NetSend wrapper | `__thiscall` su `DAT_022eed30` |
| `FUN_005ec610` | BuildAndSend | Packet 50 byte, chiama `FUN_005ea9a0` |

`FUN_004d2b50` ritorna 0 (no login) se username o password è stringa vuota (primo byte null).

### Effetto patch

| Patch | Effetto |
|---|---|
| Patch1 (`0x4D4EBF`) | Salta a `0x4D4F18` → chiama `FUN_004d2b50` bypassing key checks |
| Patch2 (`0x4D1F5F`) | Forza jump a `0x4D1FC8` (bypass UI trigger path) |
| Patch3 (`0x4D0DCD`) | Salta a `0x4D0EF3` → bypassa overwrite UI→struct per username e password |

**REGOLA**: Prima di modificare GameOffsets.cs, verificare i raw bytes con Ghidra MCP:
```
mcp__ghidrassistmcp__get_data_at address=0xXXXXXX len=16 program_name="game.exe"
```

---

## Funzione che apre il login (verificata Ghidra 2026-06-24)

`FUN_004d1970` = costruttore `CLogin`: carica `TA_2D_Login.tga`/`TA_2D_LoginBG.tga` e scrive la vtable
`MOV dword ptr [ESI],0x74ffc0` @ `0x004d19ad`. Imposta `server_select_mode (+0x4eac) = 0`.
L'updater valida `[LoginPointer]==0x0074FFC0` prima di iniettare (vedi `IsLoginScreenObject`).

## Gate di login (anti falso-successo)

L'updater NON avanza a server-select finché il login non è confermato. Segnale autorevole:
server-list pointer `*(0x022EED30)+0x270 != 0` con count valido (popolato dal login server SOLO dopo
auth riuscita) → `GameServerSelectAutomation.WaitForLoginReachedServerSelect`. Se non confermato entro
`LoginVerifyTimeoutMs + LoginMaxRetries*LoginVerifyRetryTimeoutMs` (re-injection tra i tentativi),
stato onesto "Login failed" e nessun avanzamento. SKIPSERVERSELECTION NON è più onorato: server-select sempre.

## Login flow — come funziona

```
Updater UI login
  ↓ HTTPS
api/auth/login.php
  → check_password_safe($userId, $password)   # verifica contro DB (PBKDF2 via SP o fallback)
  → passwordForGame = $password               # plaintext typed password
  ↓
Updater inietta in memoria game:
  pointer+4   = username (32 byte ASCII)
  pointer+39  = passwordForGame (32 byte ASCII)
  Patch1,2,3 applicati
  ↓
game.exe → FUN_004d2b50 → invia packet 0xA102 a loginserver:30800
  username (32 byte) + password_primi_16_byte
  ↓
sdev-login DLL (hook_0x406A8C):
  get_enpassword(db, username)               # legge Enpassword da Users_Master
  verify_pbkdf2(enpassword, received_pw)     # PBKDF2-SHA256 con CNG, expects PLAINTEXT
  argonResult = 1 se match, 0 se no
  EXEC usp_Try_GameLogin_Taiwan @ArgonResult=argonResult  # SP gestisce rate-limit e sessione
```

### Perché passwordForGame deve essere plaintext

`sdev-login/src/main.cpp` usa `BCryptDeriveKeyPBKDF2(password_received, strlen)` per verificare.  
Se si manda `Pw` dal DB (es. `"2"` = UserUID legacy), il PBKDF2 fallisce sempre.  
Il login manuale funziona perché il gioco manda la plaintext direttamente.

### Saved account login — stato ROTTO (PBKDF2)

`api/auth/login-saved.php` non ha accesso alla plaintext → ritorna errore.  
Fix futuro: sistema one-time token (PHP genera token random → DB `Updater_LoginToken` → C++ verifica token prima di PBKDF2).

---

## MCP GhidraAssist

Istanza locale `127.0.0.1:8080`. Per questo progetto usare `program_name="game.exe"`.

```
mcp__ghidrassistmcp__list_binaries
mcp__ghidrassistmcp__get_data_at address=0xXXXXXX len=16 program_name="game.exe"
mcp__ghidrassistmcp__disassemble_at address=0xXXXXXX length=128 program_name="game.exe"
mcp__ghidrassistmcp__get_code function=0xXXXXXX format=decompiler program_name="game.exe"
mcp__ghidrassistmcp__xrefs address=0xXXXXXX direction=to program_name="game.exe"
```

**Non fidarsi mai di bytes AI-generati.** Verificare sempre con `get_data_at` o `disassemble_at`.

---

## TCP port transition watcher

Login server: porta `30800`. Transizione a `30810` = login riuscito, game server connesso.  
Configurabile in `launcher.php` (`LoginTcpPort`, `GameTcpPort`).

---

## Server SSH

```
ssh -i C:\Users\lol\.ssh\id_ed25519 -o IdentitiesOnly=yes administrator@10.10.5.91
```

Usare per controllare processi live, log, DLL sul server (ps_login, ps_game, ecc.).
