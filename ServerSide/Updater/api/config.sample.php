<?php
// Public sample configuration for Updater API.
// Copy to config.php on deployment server and fill real values.

$readEnv = static function ($name, $default = '') {
    $value = getenv($name);
    if ($value === false || $value === null || $value === '') return $default;
    return $value;
};

return [
    'db' => [
        'server' => '127.0.0.1',
        'database' => 'PS_UserData',
        'user' => $readEnv('DB_USER', ''),
        'password' => $readEnv('DB_PASSWORD', ''),
        'trust_server_certificate' => true,
        'encrypt' => false,
        'login_timeout' => 5,
        'query_timeout' => 15,
    ],
    'auth' => [
        'game_password_mode' => 'plain',
        'allow_saved_login_by_device' => true,
        'allowed_statuses' => [0, 16, 32, 48, 80],
    ],
    'security' => [
        'max_userid_length' => 18,
        'max_password_length' => 32,
    ],
    // Liveness probe target for api/status.php (login server / ps_login).
    // Defaults to 127.0.0.1:30800 (apache on the same host as ps_login). Override if apache runs
    // on a different machine than the login server.
    'status' => [
        'login_host' => '127.0.0.1',
        'login_port' => 30800,
        'timeout' => 1.0,
        'cache_ttl' => 10, // seconds; caps how often the TCP probe runs (DoS amplification guard)
    ],
    // Shared secret for saved-account HMAC token.
    // Must match [SETTINGS] TokenSecret in Data/GMSecurity.ini on the login server.
    // Generate: php -r "echo bin2hex(random_bytes(32));"
    'token_secret' => $readEnv('TOKEN_SECRET', ''),
];
