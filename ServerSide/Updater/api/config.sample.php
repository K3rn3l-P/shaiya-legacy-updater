<?php
// Updater API — sample configuration.
// Copy to config.php on the deployment server and fill real values.
// Never commit config.php (contains real secrets from GMSecurity.ini).
//
// All shared secrets are read from GMSecurity.ini [SETTINGS] on the login server.
// Fallback to environment variables if the key is missing from the INI.
//
// Generate a 32-byte hex key (PowerShell):
//   [BitConverter]::ToString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).Replace('-','').ToLower()

$readEnv = static function ($name, $default = '') {
    $value = getenv($name);
    if ($value === false || $value === null || $value === '') return $default;
    return $value;
};

$_gmIni = 'C:\\ShaiyaServer\\PSM_Client\\Bin\\Data\\GMSecurity.ini';
$_gmSec = is_file($_gmIni) ? (@parse_ini_file($_gmIni, true, INI_SCANNER_RAW) ?: []) : [];
$_gm    = isset($_gmSec['SETTINGS']) ? $_gmSec['SETTINGS'] : [];
$readGm = static function ($key, $envFallback = '') use ($_gm, $readEnv) {
    $v = isset($_gm[$key]) ? trim((string)$_gm[$key]) : '';
    return $v !== '' ? $v : $readEnv($envFallback, '');
};

return [
    'db' => [
        'server'                   => '127.0.0.1',
        'database'                 => 'PS_UserData',
        'user'                     => $readEnv('DB_USER', ''),
        'password'                 => $readEnv('DB_PASSWORD', ''),
        'trust_server_certificate' => true,
        'encrypt'                  => false,
        'login_timeout'            => 5,
        'query_timeout'            => 15,
    ],
    'auth' => [
        'game_password_mode'          => 'plain',
        'allow_saved_login_by_device' => true,
        'allowed_statuses'            => [0, 16, 32, 48, 80],
    ],
    'security' => [
        'max_userid_length'   => 18,
        'max_password_length' => 32,
    ],
    // Secrets from GMSecurity.ini [SETTINGS] (auto-rotated at 04:00 by Rotate-ShaiyaKeys.ps1):
    'token_secret'  => $readGm('TokenSecret',  'TOKEN_SECRET'),   // HMAC saved-account login
    'client_secret' => $readGm('ClientSecret', 'CLIENT_SECRET'),  // 0xA101 client handshake token
    // AES-256 key for data.sah decryption (runtime, delivered to client on login).
    // Rotated manually only (major update) — requires re-encrypt of data.sah + re-distribution.
    'sah_key_hex'   => $readGm('SahKeyHex',    'SAH_KEY_HEX'),
    // AES-256 key for patch files (update.sah inside .patch ZIPs).
    // Rotated automatically at 04:00; all .patch files are re-encrypted by the rotation script.
    'patch_key_hex' => $readGm('PatchKeyHex',  'PATCH_KEY_HEX'),
];
