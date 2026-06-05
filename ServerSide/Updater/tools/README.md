# Updater Tools — Diagnostic Endpoints (localhost only)

Questi endpoint sono accessibili **solo da localhost** (`127.0.0.1` / `::1`).
Non sono mai nel workflow di produzione — servono solo per ispezione e debug.

---

## Patch workflow (nuovo — completamente automatico)

### Un solo passo: deposita il file

```
Patches/
  0001_initial_client.patch
  0002_fix_login.patch
  0003_new_content.patch
```

Il sistema fa tutto il resto automaticamente:

| Cosa | Come |
|---|---|
| SHA256 + size | Calcolato da `manifest_core.php` con cache mtime (come `Files/`) |
| `patches[]` nel manifest | Generato dinamicamente ad ogni richiesta |
| `clientVersion` | = numero totale di patch trovate |
| Hash del manifest in `launcher.ini` | Calcolato inline da `launcher.php` |

**Nessuna chiamata a `patch_hashes.php`, nessuna modifica a `manifest.json`.**

---

### Naming convention — OBBLIGATORIA

I file devono avere un prefisso numerico zero-padded che determina l'ordine di applicazione:

```
NNNN_descrizione.patch
```

Esempi corretti:
```
0001_initial_client.patch     ← versione 1
0002_fix_login_flow.patch     ← versione 2
0015_new_dungeon.patch        ← versione 15
```

**Regola**: sort naturale del filesystem = ordine versioni = version number (1-based).
Il client applica patch da `Local_ClientVersion + 1` fino a `clientVersion` **sequenzialmente**.

---

### Vincolo critico: nessun salto di versione

Il client C# cerca versione 1, poi 2, poi 3... Se manca la versione 2, lancia:

```
FileNotFoundException: Patch manifest entry missing for version 2.
```

Questo vale per il client, non per il server. Il server serve qualsiasi file presente.

**Non cancellare patch vecchie** finché non sei sicuro che tutti i client abbiano già superato quella versione.

---

### Come funziona internamente

```
manifest.php (o launcher.php)
  └─ include manifest_core.php
       └─ generate_manifest_json()
            ├─ scan Patches/*.patch  →  sorted SORT_NATURAL
            │    per ogni file:
            │      legge Patches/.hashes/{name}.cache ({mtime}:{sha256})
            │      se stale (mtime cambiato) → ricalcola SHA256 e aggiorna cache
            │    ritorna patches[] con version=1,2,3...
            │    sets clientVersion = count(patches)
            └─ scan Files/  →  files[] (repair)
```

URL di download costruito dal client:

```
item.file = "Patches/0001_initial_client.patch"
→ URL: BaseUrl + "Patches/0001_initial_client.patch"
→ es.: https://duff.pinto-lime.ts.net/Updater/Patches/0001_initial_client.patch
```

---

### Cache SHA256

```
Patches/.hashes/0001_initial_client.patch.cache
  contenuto: "1748000000:a3f4c2..."
             └── mtime └── sha256hex
```

Ricalcolata automaticamente se il file viene sostituito (mtime cambia).
**Accesso HTTP bloccato** da `Patches/.hashes/.htaccess`.

---

### Aggiungere una patch — passo per passo

1. Crea il file `.patch` (ZIP contenente i file da aggiornare e/o `update.sah`+`update.saf`)
2. Rinominalo con il prefisso corretto:
   ```
   0004_fix_graphics.patch
   ```
3. Copialo in `Patches/`
4. Fine. Il manifest si aggiorna automaticamente alla prossima richiesta client.

---

### Rimuovere patch obsolete (sicuro solo se tutti i client sono aggiornati)

1. Verifica che il client minimo supportato abbia già applicato quella patch
2. Cancella il file da `Patches/`
3. Cancella la cache corrispondente da `Patches/.hashes/` (opzionale, nessun danno se rimane)

⚠ Non rinumerare i file rimasti — il sort order cambierebbe, rompendo i client a metà aggiornamento.

---

## `patch_hashes.php` — solo diagnostico

Non è più nel workflow. Usa solo per verificare cosa vede il sistema:

```
http://127.0.0.1/Updater/tools/patch_hashes.php
```

Ritorna lo stesso array `patches[]` che il manifest conterrà (utile per debug).

---

## `file_hashes.php` — solo diagnostico

```
http://127.0.0.1/Updater/tools/file_hashes.php
```

Mostra i file in `Files/` (repair). Anche questi sono generati automaticamente — usare solo per verifica.

---

## Endpoint diagnostici generali

```
http://127.0.0.1/Updater/test-env.php      — PHP version, extensions
http://127.0.0.1/Updater/test-db.php       — connessione DB
http://127.0.0.1/Updater/test-config.php   — valori config.php
```
