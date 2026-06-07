using System;
using System.Windows;

namespace PatchMaker
{
    public static class L
    {
        public enum Lang { It, En }

        public static Lang Current { get; private set; } = Lang.It;
        public static event Action Changed;

        public static void Switch(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;

            var app = Application.Current;
            ResourceDictionary old = null;
            foreach (var d in app.Resources.MergedDictionaries)
                if (d.Source != null && d.Source.OriginalString.Contains("/Lang/"))
                { old = d; break; }
            if (old != null) app.Resources.MergedDictionaries.Remove(old);

            string src = lang == Lang.En ? "/Lang/en.xaml" : "/Lang/it.xaml";
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,," + src)
            });

            Changed?.Invoke();
        }

        // Helper
        private static string S(string it, string en) => Current == Lang.It ? it : en;

        // ── App ──────────────────────────────────────────────────
        public static string Startup       => S("PatchMaker avviato.", "PatchMaker started.");
        public static string SettingsSaved => S("Impostazioni salvate.", "Settings saved.");

        // ── Workspace ────────────────────────────────────────────
        public static string WsPathRequired => S("Specificare la cartella workspace.", "Please specify the workspace folder.");
        public static string WsCreated(string p)     => S($"Struttura workspace creata: {p}", $"Workspace structure created: {p}");
        public static string WsFilesHint(string p)   => S($"  → Metti i file da patchare in: {p}", $"  → Put files to patch in: {p}");
        public static string WsExamples              => S("  → Esempi:", "  → Examples:");
        public static string WsOutputHint(string p)  => S($"  → Patch generate in: {p}", $"  → Patches generated in: {p}");

        // ── Patch number ─────────────────────────────────────────
        public static string PatchNumber(int n)   => S($"Numero patch: {n:D4}", $"Patch number: {n:D4}");
        public static string SshAutoNotConnected  => S("Server SSH non connesso, numero patch 'auto' non disponibile. Uso 1.", "SSH server not connected, auto patch number unavailable. Using 1.");
        public static string SshNextPatch(int n)  => S($"Prossimo numero patch rilevato dal server: {n:D4}", $"Next patch number detected from server: {n:D4}");

        // ── Errors (UI) ──────────────────────────────────────────
        public static string ErrDescRequired  => S("ERRORE: inserire una descrizione.", "ERROR: please enter a description.");
        public static string ErrKeyRequired   => S("ERRORE: PatchKeyHex non configurato (vedi Impostazioni).", "ERROR: PatchKeyHex not configured (see Settings).");
        public static string ErrBuildFirst    => S("ERRORE: genera prima la patch (▶ Genera Patch).", "ERROR: generate the patch first (▶ Generate Patch).");
        public static string ErrSshFirst      => S("ERRORE: connettiti al server SSH prima di caricare.", "ERROR: connect to SSH server before uploading.");

        // ── Patch result ─────────────────────────────────────────
        public static string PatchReady(string name) => S($"✓ Patch pronta: {name}", $"✓ Patch ready: {name}");

        // ── Key reading ──────────────────────────────────────────
        public static string ReadingGmSec(string p)         => S($"Lettura GMSecurity.ini dal server: {p}", $"Reading GMSecurity.ini from server: {p}");
        public static string KeyReadFromServer(int sahLen, int patchLen) => S($"Chiavi lette dal server — SahKeyHex ({sahLen} char), PatchKeyHex ({patchLen} char)", $"Keys read from server — SahKeyHex ({sahLen} chars), PatchKeyHex ({patchLen} chars)");
        public static string KeyReadFromServer(int len)      => S($"Chiave letta dal server ({len} char)", $"Key read from server ({len} chars)");
        public static string ErrKeyNotFoundServer            => S("ERRORE: chiavi non trovate in GMSecurity.ini del server.", "ERROR: keys not found in server GMSecurity.ini.");
        public static string SshReadFailed                   => S("Impossibile leggere dal server, provo file locale...", "Cannot read from server, trying local file...");
        public static string ErrKeyNotFoundLocal(string f)  => S($"ERRORE: chiavi non trovate in {f}", $"ERROR: keys not found in {f}");
        public static string KeyReadFromFile(string f, int len) => S($"Chiave letta da {f} ({len} char)", $"Key read from {f} ({len} chars)");

        // ── SSH service ──────────────────────────────────────────
        public static string SshErrorOccurred(string msg)              => $"SSH.NET ErrorOccurred: {msg}";
        public static string SshConnectedFmt(string host, string user) => S($"SFTP connesso a {host} come {user}", $"SFTP connected to {host} as {user}");
        public static string SshPatchesListWarn(string msg)            => S($"Avviso: impossibile elencare Patches/ — {msg}", $"Warning: cannot list Patches/ — {msg}");
        public static string SshUploadFmt(string file, string path)    => S($"Upload SFTP: {file} → {path}", $"SFTP upload: {file} → {path}");
        public static string SshUploadDone    => S("Upload completato.", "Upload complete.");
        public static string ManifestNotFound => S("manifest.json non trovato sul server, verrà creato.", "manifest.json not found on server, it will be created.");
        public static string ManifestUpdating => S("Aggiornamento manifest.json...", "Updating manifest.json...");
        public static string ManifestUpdated(int count, int ver, string hash) => S(
            $"manifest.json aggiornato: {count} patch(es), nuova versione={ver}, sha256={hash}...",
            $"manifest.json updated: {count} patch(es), new version={ver}, sha256={hash}...");
        public static string SshNotConnected => S("SSH non connesso.", "SSH not connected.");
        public static string ReadFileFailed(string path, string msg) => S(
            $"ReadTextFile fallito ({path}): {msg}",
            $"ReadTextFile failed ({path}): {msg}");

        // ── PatchBuilder ─────────────────────────────────────────
        public static string AnalyzingData               => S("Analisi struttura data/...", "Analyzing data/ structure...");
        public static string FilesDetected(string layout) => S($"File rilevati:\n{layout}", $"Files detected:\n{layout}");
        public static string GeneratingSah               => S("Generazione SAH + SAF...", "Generating SAH + SAF...");
        public static string SahSafSizes(long sah, long saf) => $"SAH: {sah} KB, SAF: {saf} KB";
        public static string EncryptingSah               => S("Cifratura SAH → ESAH AES-256-GCM...", "Encrypting SAH → ESAH AES-256-GCM...");
        public static string UpdateSahSize(long kb)      => $"update.sah: {kb} KB";
        public static string UpdateSafSize(long kb)      => $"update.saf: {kb} KB";
        public static string CreatingZip(string name)    => S($"Creazione ZIP → {name}", $"Creating ZIP → {name}");
        public static string PatchDone(string name, long kb) => $"✓ Patch: {name} ({kb} KB)";

        // ── Verifica patch ───────────────────────────────────────
        public static string VerifyStart(string name)  => S($"Verifica patch: {name}", $"Verifying patch: {name}");
        public static string VerifyNoSah               => S("ERRORE: update.sah non trovato nel .patch.", "ERROR: update.sah not found in .patch file.");
        public static string VerifyBadMagic            => S("ERRORE: update.sah non è ESAH né SAH valido.", "ERROR: update.sah is neither valid ESAH nor SAH.");
        public static string VerifyDecrypting          => S("Decifratura ESAH AES-256-GCM...", "Decrypting ESAH AES-256-GCM...");
        public static string VerifyDecryptFailed       => S("ERRORE: decifratura fallita — chiave errata?  NTSTATUS=0xC000A002 = tag mismatch.", "ERROR: decryption failed — wrong key?  NTSTATUS=0xC000A002 = tag mismatch.");
        public static string VerifyOk(int n)           => S($"✓ SAH valido — {n} file:", $"✓ Valid SAH — {n} files:");

        // ── Estrai patch ──────────────────────────────────────────
        public static string ExtractStart(string name, string dir)  => S($"Estrazione {name} → {dir}", $"Extracting {name} → {dir}");
        public static string ExtractNoSah                           => S("ERRORE: update.sah non trovato dopo estrazione.", "ERROR: update.sah not found after extraction.");
        public static string ExtractDecrypting                      => S("Decifratura update.sah (ESAH → SAH)...", "Decrypting update.sah (ESAH → SAH)...");
        public static string ExtractDecryptOk(long kb)              => S($"✓ update.sah decifrato ({kb} KB, plain SAH).", $"✓ update.sah decrypted ({kb} KB, plain SAH).");
        public static string ExtractAlreadyPlain                    => S("update.sah è già plain SAH (non cifrato).", "update.sah is already plain SAH (not encrypted).");
        public static string ExtractDone(string dir)                => S($"✓ Estrazione completata: {dir}", $"✓ Extraction complete: {dir}");

        // ── SAH Crypt ─────────────────────────────────────────────
        public static string ErrSelectSahFirst   => S("Selezionare prima un file .sah.", "Select a .sah file first.");
        public static string ErrKeyRequiredSah   => S("Chiave SAH non impostata — aprire ⚙ Impostazioni.", "SAH key not configured — open ⚙ Settings.");
        public static string SahAlreadyEncrypted => S("File già cifrato (ESAH) — usare Decifra prima.", "File already encrypted (ESAH) — use Decrypt first.");
        public static string SahAlreadyPlain     => S("File già in chiaro (SAH) — usare Cifra prima.", "File already plain SAH — use Encrypt first.");
        public static string SahBadMagic         => S("File non riconosciuto (né SAH né ESAH).", "File not recognized (neither SAH nor ESAH).");
        public static string SahDecryptFailed    => S("Decifratura fallita — chiave errata?", "Decryption failed — wrong key?");
        public static string SahEncryptOk(string file, string backup, long kb) => S(
            $"✔ Cifrato: {file}  ·  backup → {backup}  ({kb} KB)",
            $"✔ Encrypted: {file}  ·  backup → {backup}  ({kb} KB)");
        public static string SahDecryptOk(string file, string backup, long kb) => S(
            $"✔ Decifrato: {file}  ·  backup → {backup}  ({kb} KB)",
            $"✔ Decrypted: {file}  ·  backup → {backup}  ({kb} KB)");

        // ── PatchBuilder mode ────────────────────────────────────
        public static string SahPlainMode => S("SAH non cifrato (modalità plain).", "SAH not encrypted (plain mode).");

        // ── Exception messages (SahSafBuilder / PatchBuilder) ────
        public static string ErrDataDirNotFound(string p) => S($"Cartella data/ non trovata: {p}", $"data/ folder not found: {p}");
        public static string ErrSahInvalid               => S("SAH generato non valido (magic SAH mancante).", "Generated SAH is invalid (SAH magic missing).");
        public static string ErrDataNotFound_Describe    => S("(data/ non trovata)", "(data/ not found)");
        public static string LabelEmpty                  => S("  (vuota)", "  (empty)");

        // ── UI status (set from code, not XAML) ──────────────────
        public static string StatusConnected    => S("● Connesso", "● Connected");
        public static string StatusDisconnected => S("● Disconnesso", "● Disconnected");
        public static string BtnConnect         => S("Connetti", "Connect");
        public static string BtnDisconnect      => S("Disconnetti", "Disconnect");
        public static string SshTestOk          => S("Test connessione OK.", "Connection test OK.");
        public static string Err(string msg)    => S("ERRORE: ", "ERROR: ") + msg;
    }
}
