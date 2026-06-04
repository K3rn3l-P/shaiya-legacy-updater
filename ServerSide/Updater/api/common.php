<?php
// Compatibility-oriented common API code for IIS + PHP sqlsrv.
// QueryTimeout is used only on sqlsrv_query, never sqlsrv_connect.

ini_set('display_errors', '0');
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$configFile = __DIR__ . '/config.php';
if (!is_file($configFile)) {
    $configFile = __DIR__ . '/config.sample.php';
}
$config = require $configFile;

function respond($data, $code = 200) {
    http_response_code($code);
    echo json_encode($data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

function read_json_body() {
    $raw = file_get_contents('php://input');
    if ($raw === false || trim($raw) === '') return [];
    if (strlen($raw) > 4096) respond(['ok' => false, 'message' => 'Request body too large.'], 400);
    $data = json_decode($raw, true);
    return is_array($data) ? $data : [];
}

function client_ip() {
    foreach (['HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR'] as $key) {
        if (empty($_SERVER[$key])) continue;
        $value = trim((string)$_SERVER[$key]);
        if ($key === 'HTTP_X_FORWARDED_FOR') {
            $parts = explode(',', $value);
            $value = trim($parts[0]);
        }
        if ($value !== '') return substr($value, 0, 45);
    }
    return '0.0.0.0';
}

function require_sqlsrv() {
    if (!function_exists('sqlsrv_connect')) {
        respond([
            'ok' => false,
            'message' => 'Database driver not available.',
        ], 500);
    }
}

function db_error_text() {
    if (!function_exists('sqlsrv_errors')) return 'sqlsrv is unavailable';
    $errors = sqlsrv_errors(SQLSRV_ERR_ERRORS);
    if (!$errors) return 'unknown database error';
    $out = [];
    foreach ($errors as $e) {
        $out[] = '[' . ($e['SQLSTATE'] ?? '') . '/' . ($e['code'] ?? '') . '] ' . ($e['message'] ?? '');
    }
    return implode(' | ', $out);
}

function db_conn() {
    static $conn = null;
    global $config;
    if ($conn) return $conn;

    require_sqlsrv();

    $db = $config['db'];
    $opts = [
        'Database' => $db['database'],
        'CharacterSet' => 'UTF-8',
        'LoginTimeout' => isset($db['login_timeout']) ? (int)$db['login_timeout'] : 5,
        'TrustServerCertificate' => !empty($db['trust_server_certificate']),
        'Encrypt' => !empty($db['encrypt']),
    ];

    if (!empty($db['user'])) {
        $opts['UID'] = $db['user'];
        $opts['PWD'] = $db['password'];
    }

    $conn = sqlsrv_connect($db['server'], $opts);
    if (!$conn) {
        error_log('[updater-api] db_conn failed: ' . db_error_text());
        respond(['ok' => false, 'message' => 'Database connection failed.'], 500);
    }
    return $conn;
}

function db_query($sql, $params = []) {
    global $config;
    $queryOptions = [];
    if (isset($config['db']['query_timeout'])) {
        $queryOptions['QueryTimeout'] = (int)$config['db']['query_timeout'];
    }
    $stmt = sqlsrv_query(db_conn(), $sql, $params, $queryOptions);
    if (!$stmt) {
        error_log('[updater-api] db_query failed: ' . db_error_text());
        respond(['ok' => false, 'message' => 'Database query failed.'], 500);
    }
    return $stmt;
}

function db_fetch_one($sql, $params = []) {
    $stmt = db_query($sql, $params);
    $row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC);
    if ($row === false || $row === null) return null;
    return $row;
}

function clean_user_id($userId) {
    global $config;
    $userId = trim((string)$userId);
    $max = (int)$config['security']['max_userid_length'];
    if ($userId === '' || strlen($userId) > $max) respond(['ok' => false, 'message' => 'Invalid user ID.']);
    if (!preg_match('/^[A-Za-z0-9_@!#.\-]+$/', $userId)) respond(['ok' => false, 'message' => 'Invalid user ID format.']);
    return $userId;
}

function clean_password($password) {
    global $config;
    $password = (string)$password;
    $max = (int)$config['security']['max_password_length'];
    if ($password === '' || strlen($password) > $max) respond(['ok' => false, 'message' => 'Invalid password.']);
    return $password;
}

function clean_device_id($deviceId) {
    $deviceId = trim((string)$deviceId);
    if ($deviceId === '') return '';
    return substr($deviceId, 0, 128);
}

function is_allowed_status($status) {
    global $config;
    $allowed = array_map('intval', $config['auth']['allowed_statuses']);
    return in_array((int)$status, $allowed, true);
}

function table_exists($tableName) {
    $row = db_fetch_one("SELECT CASE WHEN OBJECT_ID(?, N'U') IS NULL THEN 0 ELSE 1 END AS ExistsFlag", [$tableName]);
    return $row && (int)$row['ExistsFlag'] === 1;
}

function verify_enpassword($password, $enpassword) {
    if (empty($enpassword)) return false;
    if (substr($enpassword, 0, 13) === 'pbkdf2sha256:') {
        $parts = explode(':', $enpassword, 4);
        if (count($parts) !== 4) return false;
        $salt     = hex2bin($parts[2]);
        $expected = hex2bin($parts[3]);
        $computed = hash_pbkdf2('sha256', $password, $salt, (int)$parts[1], 32, true);
        return hash_equals($expected, $computed);
    }
    return password_verify($password, $enpassword);
}

function check_password_safe($userId, $password) {
    global $config;

    $sql = "EXEC dbo.CheckPw_Safe @UserID=?, @Pw=?";
    $queryOptions = [];
    if (isset($config['db']['query_timeout'])) {
        $queryOptions['QueryTimeout'] = (int)$config['db']['query_timeout'];
    }

    $stmt = @sqlsrv_query(db_conn(), $sql, [$userId, $password], $queryOptions);
    if ($stmt === false) {
        $missingProc = false;
        if (function_exists('sqlsrv_errors')) {
            $errors = sqlsrv_errors(SQLSRV_ERR_ERRORS);
            if (is_array($errors)) {
                foreach ($errors as $e) {
                    if ((int)($e['code'] ?? 0) === 2812) {
                        $missingProc = true;
                        break;
                    }
                }
            }
        }

        if (!$missingProc) {
            error_log('[updater-api] check_password_safe failed: ' . db_error_text());
            respond(['ok' => false, 'message' => 'Database query failed.'], 500);
        }

        $row = db_fetch_one(
            "SELECT TOP 1 UserUID, UserID, Enpassword, Status, Leave
             FROM dbo.Users_Master WITH (NOLOCK)
             WHERE UserID COLLATE SQL_Latin1_General_CP1_CS_AS = ?",
            [$userId]
        );
        if (!$row || !verify_enpassword($password, (string)($row['Enpassword'] ?? ''))) {
            return null;
        }
        return $row;
    }

    $row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC);
    if ($row === false || $row === null) return null;
    return $row;
}

function ensure_active_login_row($row) {
    if (!isset($row['Status'])) return false;
    if (!is_allowed_status((int)$row['Status'])) return false;
    if (isset($row['Leave']) && (int)$row['Leave'] !== 0) return false;
    return true;
}

function save_device_account($deviceId, $userUid, $userId) {
    if ($deviceId === '') return;
    if (!table_exists('dbo.Updater_SavedDeviceAccount')) return;
    $ip = client_ip();
    $sql = "
DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
IF EXISTS (SELECT 1 FROM dbo.Updater_SavedDeviceAccount WHERE DeviceIdHash=@DeviceHash AND UserUID=?)
BEGIN
    UPDATE dbo.Updater_SavedDeviceAccount
    SET UserID=?, LastUsedAt=SYSUTCDATETIME(), LastIP=?
    WHERE DeviceIdHash=@DeviceHash AND UserUID=?;
END
ELSE
BEGIN
    INSERT INTO dbo.Updater_SavedDeviceAccount(DeviceIdHash, UserUID, UserID, CreatedAt, LastUsedAt, LastIP)
    VALUES(@DeviceHash, ?, ?, SYSUTCDATETIME(), SYSUTCDATETIME(), ?);
END";
    db_query($sql, [$deviceId, (int)$userUid, $userId, $ip, (int)$userUid, (int)$userUid, $userId, $ip]);
}

function saved_account_is_allowed($deviceId, $userId) {
    if ($deviceId === '' || !table_exists('dbo.Updater_SavedDeviceAccount')) return null;
    return db_fetch_one(
        "DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
         SELECT TOP 1 u.UserUID, u.UserID, u.Status, u.Leave
         FROM dbo.Updater_SavedDeviceAccount s WITH (NOLOCK)
         INNER JOIN dbo.Users_Master u WITH (NOLOCK) ON u.UserUID = s.UserUID
         WHERE s.DeviceIdHash = @DeviceHash AND u.UserID COLLATE SQL_Latin1_General_CP1_CS_AS = ?",
        [$deviceId, $userId]
    );
}

function read_saved_accounts($deviceId) {
    if ($deviceId === '' || !table_exists('dbo.Updater_SavedDeviceAccount')) return [];
    $stmt = db_query(
        "DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
         SELECT u.UserID
         FROM dbo.Updater_SavedDeviceAccount s WITH (NOLOCK)
         INNER JOIN dbo.Users_Master u WITH (NOLOCK) ON u.UserUID = s.UserUID
         WHERE s.DeviceIdHash = @DeviceHash
         ORDER BY s.LastUsedAt DESC, s.CreatedAt DESC",
        [$deviceId]
    );
    $out = [];
    while (($row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC)) !== null) {
        if ($row !== false && isset($row['UserID'])) $out[] = (string)$row['UserID'];
    }
    return $out;
}

function remove_saved_account($deviceId, $userId) {
    if ($deviceId === '' || !table_exists('dbo.Updater_SavedDeviceAccount')) return;
    db_query(
        "DECLARE @DeviceHash varbinary(32) = HASHBYTES('SHA2_256', CONVERT(varbinary(256), ?));
         DELETE s
         FROM dbo.Updater_SavedDeviceAccount s
         INNER JOIN dbo.Users_Master u ON u.UserUID = s.UserUID
         WHERE s.DeviceIdHash = @DeviceHash AND u.UserID COLLATE SQL_Latin1_General_CP1_CS_AS = ?",
        [$deviceId, $userId]
    );
}

function get_sah_key_hex() {
    global $config;
    $key = isset($config['sah_key_hex']) ? trim((string)$config['sah_key_hex']) : '';
    return (strlen($key) === 64 && ctype_xdigit($key)) ? $key : null;
}

// Calcola il token effimero per l'autenticazione client (0xA101 RSA handshake).
// Stesso algoritmo di verify_rsa_client_token() in sdev-login.dll:
//   HMAC-SHA256("client:" + floor(time/300), client_secret) → primi 4 byte LE come hex uint32.
// Il client NON ha il segreto: riceve solo il token pre-computato qui.
function get_launch_token() {
    global $config;
    $secret = isset($config['client_secret']) ? trim((string)$config['client_secret']) : '';
    if ($secret === '') return null;
    $window = (int)floor(time() / 300);
    $hmac = hash_hmac('sha256', 'client:' . $window, $secret, true); // 32 byte raw
    $tokenInt = unpack('V', substr($hmac, 0, 4))[1];                  // uint32 LE
    return str_pad(dechex($tokenInt), 8, '0', STR_PAD_LEFT);          // 8 hex chars
}

function success_response($userId, $userUid, $passwordForGame) {
    $resp = [
        'ok' => true,
        'message' => 'Login successful.',
        'userId' => $userId,
        'userUid' => (int)$userUid,
        'passwordForGame' => $passwordForGame,
    ];
    $sahKey = get_sah_key_hex();
    if ($sahKey !== null) {
        $resp['sahKey'] = $sahKey;
    }
    $launchToken = get_launch_token();
    if ($launchToken !== null) {
        $resp['launchToken'] = $launchToken;
    }
    return $resp;
}

function generate_login_token($userId) {
    global $config;
    $secret = isset($config['token_secret']) ? (string)$config['token_secret'] : '';
    if ($secret === '') {
        respond(['ok' => false, 'message' => 'Token secret not configured.'], 500);
    }
    $window = (int)(time() / 300);
    $hmac = hash_hmac('sha256', $userId . ':' . $window, $secret);
    return substr($hmac, 0, 15);
}

// ---------------------------------------------------------------------------
// Rate limiting — file-based, no DB required.
// State stored in system temp dir (outside web root).
// ---------------------------------------------------------------------------

function _updater_ratelimit_dir(): string {
    return rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'updater_auth';
}

function _updater_ratelimit_ensure_dir(): bool {
    $dir = _updater_ratelimit_dir();
    if (!is_dir($dir)) { @mkdir($dir, 0750, true); }
    return is_dir($dir);
}

// Sliding-window rate limiter. Returns true if request allowed.
// $retryAfterSeconds: set to seconds until reset when denied.
function updater_ratelimit_allow(string $key, int $maxAttempts, int $windowSeconds, int &$retryAfterSeconds = 0): bool {
    if (!_updater_ratelimit_ensure_dir()) return true;
    $file = _updater_ratelimit_dir() . DIRECTORY_SEPARATOR . 'rl_' . md5($key) . '.json';
    $handle = @fopen($file, 'c+');
    if (!$handle) return true;
    if (!flock($handle, LOCK_EX)) { fclose($handle); return true; }

    $now = time();
    $raw = stream_get_contents($handle);
    $ts = json_decode($raw ?: '[]', true);
    if (!is_array($ts)) $ts = [];

    // Prune old entries
    $cutoff = $now - $windowSeconds;
    $ts = array_values(array_filter($ts, static fn($t) => is_int($t) && $t >= $cutoff));

    if (count($ts) >= $maxAttempts) {
        $retryAfterSeconds = max(1, (min($ts) + $windowSeconds) - $now);
        ftruncate($handle, 0); rewind($handle);
        fwrite($handle, json_encode($ts));
        fflush($handle); flock($handle, LOCK_UN); fclose($handle);
        return false;
    }

    $ts[] = $now;
    ftruncate($handle, 0); rewind($handle);
    fwrite($handle, json_encode($ts));
    fflush($handle); flock($handle, LOCK_UN); fclose($handle);
    $retryAfterSeconds = 0;
    return true;
}

// Progressive lockout per user/key. Returns ['locked'=>bool, 'remaining_seconds'=>int, 'failures'=>int].
function updater_auth_lockout_check(string $key): array {
    if (!_updater_ratelimit_ensure_dir()) return ['locked' => false, 'remaining_seconds' => 0, 'failures' => 0];
    $file = _updater_ratelimit_dir() . DIRECTORY_SEPARATOR . 'lo_' . md5($key) . '.json';
    if (!file_exists($file)) return ['locked' => false, 'remaining_seconds' => 0, 'failures' => 0];
    $data = @json_decode(file_get_contents($file), true);
    if (!is_array($data)) return ['locked' => false, 'remaining_seconds' => 0, 'failures' => 0];
    $now = time();
    $lockUntil = (int)($data['lock_until'] ?? 0);
    $failures  = (int)($data['failures'] ?? 0);
    if ($lockUntil > $now) {
        return ['locked' => true, 'remaining_seconds' => $lockUntil - $now, 'failures' => $failures];
    }
    return ['locked' => false, 'remaining_seconds' => 0, 'failures' => $failures];
}

// Record a failed attempt. Returns ['failures'=>int, 'lock_seconds'=>int].
// Delay map: 1-3 attempts = 0s, 4=30s, 5=60s, 6=180s, 7=300s, 8+=900s (max 1800s).
function updater_auth_lockout_record_failure(string $key): array {
    if (!_updater_ratelimit_ensure_dir()) return ['failures' => 0, 'lock_seconds' => 0];
    $file = _updater_ratelimit_dir() . DIRECTORY_SEPARATOR . 'lo_' . md5($key) . '.json';
    $handle = @fopen($file, 'c+');
    if (!$handle) return ['failures' => 0, 'lock_seconds' => 0];
    if (!flock($handle, LOCK_EX)) { fclose($handle); return ['failures' => 0, 'lock_seconds' => 0]; }

    $raw = stream_get_contents($handle);
    $data = json_decode($raw ?: '{}', true);
    if (!is_array($data)) $data = [];

    $failures = (int)($data['failures'] ?? 0) + 1;
    static $delayMap = [4 => 30, 5 => 60, 6 => 180, 7 => 300];
    $lockSeconds = $delayMap[$failures] ?? ($failures >= 8 ? 900 : 0);
    if ($lockSeconds > 1800) $lockSeconds = 1800;

    $data = [
        'failures'   => $failures,
        'lock_until' => $lockSeconds > 0 ? (time() + $lockSeconds) : 0,
        'updated_at' => time(),
    ];

    ftruncate($handle, 0); rewind($handle);
    fwrite($handle, json_encode($data));
    fflush($handle); flock($handle, LOCK_UN); fclose($handle);
    return ['failures' => $failures, 'lock_seconds' => $lockSeconds];
}

// Reset lockout after successful login.
function updater_auth_lockout_reset(string $key): void {
    if (!_updater_ratelimit_ensure_dir()) return;
    $file = _updater_ratelimit_dir() . DIRECTORY_SEPARATOR . 'lo_' . md5($key) . '.json';
    @unlink($file);
    $rl = _updater_ratelimit_dir() . DIRECTORY_SEPARATOR . 'rl_' . md5('login_ip:' . client_ip()) . '.json';
    @unlink($rl);
}
