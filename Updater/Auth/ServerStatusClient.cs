using System;

namespace Shaiya_Duff_Updater
{
    // Lightweight liveness probe for the website/API and the login (game) server.
    // A single HTTPS call to api/status.php: if it answers, the website is up; the JSON tells
    // whether the login server TCP port is reachable (checked server-side via fsockopen).
    public static class ServerStatusClient
    {
        public struct Status
        {
            public bool Reachable; // api/status.php answered (website/API up)
            public bool Web;       // website online (true when Reachable)
            public bool Server;    // login server reachable (only meaningful when Reachable)
        }

        public static Status Get()
        {
            try
            {
                string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/status.php");
                StatusResponse r = JsonHttpClient.Get<StatusResponse>(url);
                if (r == null)
                    return new Status { Reachable = false, Web = false, Server = false };

                return new Status { Reachable = true, Web = true, Server = r.server };
            }
            catch (Exception ex)
            {
                AppLogger.Debug("status", "Status probe failed: " + ex.Message);
                return new Status { Reachable = false, Web = false, Server = false };
            }
        }
    }
}
