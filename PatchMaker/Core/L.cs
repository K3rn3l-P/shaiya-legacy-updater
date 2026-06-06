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
        public static string ErrKeyRequired   => S("ERRORE: SahKeyHex non configurato (vedi Impostazioni).", "ERROR: SahKeyHex not configured (see Settings).");
        public static string ErrBuildFirst    => S("ERRORE: genera prima la patch (▶ Genera Patch).", "ERROR: generate the patch first (▶ Generate Patch).");
        public static string ErrSshFirst      => S("ERRORE: connettiti al server SSH prima di caricare.", "ERROR: connect to SSH server before uploading.");

        // ── Patch result ─────────────────────────────────────────
        public static string PatchReady(string name) => S($"✓ Patch pronta: {name}", $"✓ Patch ready: {name}");

        // ── Key reading ──────────────────────────────────────────
        public static string ReadingGmSec(string p)         => S($"Lettura GMSecurity.ini dal server: {p}", $"Reading GMSecurity.ini from server: {p}");
        public static string KeyReadFromServer(int len)      => S($"SahKeyHex letto dal server ({len} char)", $"SahKeyHex read from server ({len} chars)");
        public static string ErrKeyNotFoundServer            => S("ERRORE: SahKeyHex non trovato in GMSecurity.ini del server.", "ERROR: SahKeyHex not found in server GMSecurity.ini.");
        public static string SshReadFailed                   => S("Impossibile leggere dal server, provo file locale...", "Cannot read from server, trying local file...");
        public static string ErrKeyNotFoundLocal(string f)  => S($"ERRORE: SahKeyHex non trovato in {f}", $"ERROR: SahKeyHex not found in {f}");
        public static string KeyReadFromFile(string f, int len) => S($"SahKeyHex letto da {f} ({len} char)", $"SahKeyHex read from {f} ({len} chars)");

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
