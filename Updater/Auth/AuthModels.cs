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
}

