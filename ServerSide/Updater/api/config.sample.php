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
    // Shared secret for saved-account HMAC token.
    // Must match [SETTINGS] TokenSecret in Data/GMSecurity.ini on the login server.
    // Generate: php -r "echo bin2hex(random_bytes(32));"
    'token_secret' => $readEnv('TOKEN_SECRET', ''),
    // AES-256 key for data.sah decryption — returned only after successful authentication.
    // Must be 64 hex characters (32 bytes). NEVER put this in launcher.php or any public endpoint.
    // Generate: php -r "echo bin2hex(random_bytes(32));"
    // This value must match SahCryptKeyHex previously used by the updater to encrypt data.sah.
    'sah_key_hex' => $readEnv('SAH_KEY_HEX', ''),
];
