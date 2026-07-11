using System.Collections.Generic;

namespace Shaiya_Duff_Updater
{
    public class LoginResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
        public string userId { get; set; }
        public int userUid { get; set; }
        public string passwordForGame { get; set; }
        // AES-256 key (64 hex chars) for data.sah decryption.
        // Present only when server has sah_key_hex configured and auth succeeded.
        public string sahKey { get; set; }
        // AES-256 key (64 hex chars) per decifrare patch/update files (update.sah).
        // Distinto da sahKey: ruotato automaticamente ogni notte dal server.
        public string patchKey { get; set; }
        // Token effimero (8 hex chars = uint32_t LE) pre-computato dal server.
        // Passato via SDEV_AUTH_KEY a game.exe. Valido ~10 min. Client non ha mai il segreto.
        public string launchToken { get; set; }
        // Token di sessione sicurezza (64 hex chars = 32 byte random).
        // SHA256(token) salvato in DB GameSecuritySessions. Valido 24h.
        // Passato via SDEV_SEC_TOKEN a game.exe per autenticare i tamper report HTTP.
        public string secToken { get; set; }
        // Firma HMAC-SHA256(hwid, HwidSigKey) calcolata dal server (64 hex chars lowercase).
        // Salvare in device.id.sig. La chiave NON è mai nel client.
        public string hwidSig { get; set; }
    }

    // Risposta di api/auth/launch-provision.php — credenziali effimere riemesse
    // ad ogni avvio (Start Game) autenticando con il secToken di sessione.
    public class LaunchProvisionResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
        public string launchToken { get; set; }
        public string sahKey { get; set; }
    }

    public class SavedAccountsResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
        public string[] accounts { get; set; }
    }

    public class GenericApiResponse
    {
        public bool ok { get; set; }
        public string message { get; set; }
    }

    // Response of api/status.php. The fact the call succeeds at all proves the website/API is up;
    // "server" reflects whether the login server (ps_login) TCP port is reachable.
    // "maintenance" reflects PS_UserData.dbo.MaintenanceMode (set from ShaiyaServerHub's
    // "Manutenzione" card): server-side gate already blocks normal-player login when true — this
    // flag is purely informational, to show a banner instead of disabling anything client-side.
    public class StatusResponse
    {
        public bool ok { get; set; }
        public bool web { get; set; }
        public bool server { get; set; }
        public bool maintenance { get; set; }
        public long ts { get; set; }
    }

    public class WebNewsItem
    {
        public int id { get; set; }
        public string title { get; set; }
        public string preview { get; set; }
        public string author { get; set; }
        public string date { get; set; }
    }

    public class WebNewsResponse
    {
        public bool ok { get; set; }
        public List<WebNewsItem> news { get; set; }
    }

    public class RankingItem
    {
        public int rank { get; set; }
        public string name { get; set; }
        public string className { get; set; }
        public int level { get; set; }
        public int kills { get; set; }
        public string guild { get; set; }
        public int country { get; set; }
        public bool online { get; set; }
    }

    public class RankingsResponse
    {
        public bool ok { get; set; }
        public List<RankingItem> rankings { get; set; }
    }

    public class BossItem
    {
        public string name { get; set; }
        public int level { get; set; }
        public string mapName { get; set; }
        public string lastKilledBy { get; set; }
        public int secondsUntilSpawn { get; set; }
        public int respawnSeconds { get; set; }
    }

    public class BossesResponse
    {
        public bool ok { get; set; }
        public List<BossItem> bosses { get; set; }
    }
}

