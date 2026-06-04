<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$body     = read_json_body();
$userId   = clean_user_id((string)($body['userId'] ?? ''));
$password = clean_password((string)($body['password'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
$saveAccount = !empty($body['saveAccount']);

$ip       = client_ip();
$lockKey  = 'login_user:' . strtolower($userId);
$ipKey    = 'login_ip:' . $ip;

// Per-IP rate limit: max 20 attempts per 15 min window
$retryAfter = 0;
if (!updater_ratelimit_allow($ipKey, 20, 900, $retryAfter)) {
    header('Retry-After: ' . $retryAfter);
    respond(['ok' => false, 'message' => 'Too many requests. Try again later.'], 429);
}

// Per-user progressive lockout
$lockout = updater_auth_lockout_check($lockKey);
if ($lockout['locked']) {
    header('Retry-After: ' . $lockout['remaining_seconds']);
    respond(['ok' => false, 'message' => 'Account temporarily locked. Try again in ' . ceil($lockout['remaining_seconds'] / 60) . ' min.'], 429);
}

$row = check_password_safe($userId, $password);
if (!$row) {
    updater_auth_lockout_record_failure($lockKey);
    respond(['ok' => false, 'message' => 'Invalid ID or password.']);
}

$userUid = (int)$row['UserUID'];
$status  = (int)$row['Status'];
if (!ensure_active_login_row($row)) {
    respond(['ok' => false, 'message' => 'This account is not allowed to login.']);
}

updater_auth_lockout_reset($lockKey);

if ($saveAccount) {
    save_device_account($deviceId, $userUid, $userId);
}

// The game login server (sdev-login) verifies credentials via PBKDF2 against Enpassword.
// verify_pbkdf2(Enpassword, received_password) expects the plaintext password as input.
$passwordForGame = $password;
respond(success_response($userId, $userUid, $passwordForGame));
