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

