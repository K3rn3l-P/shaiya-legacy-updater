<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$body     = read_json_body();
$userId   = clean_user_id((string)($body['userId'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));

$ip    = client_ip();
$ipKey = 'login_ip:' . $ip;

// Per-IP rate limit shared with login.php: max 20 attempts per 15 min window
$retryAfter = 0;
if (!updater_ratelimit_allow($ipKey, 20, 900, $retryAfter)) {
    header('Retry-After: ' . $retryAfter);
    respond(['ok' => false, 'message' => 'Too many requests. Try again later.'], 429);
}

$row = saved_account_is_allowed($deviceId, $userId);
if (!$row) {
    respond(['ok' => false, 'message' => 'Saved-account login is not available. Please enter your password again.']);
}
if (!ensure_active_login_row($row)) {
    respond(['ok' => false, 'message' => 'This account is not allowed to login.']);
}

$passwordForGame = generate_login_token((string)$row['UserID']);

save_device_account($deviceId, (int)$row['UserUID'], (string)$row['UserID']);
respond(success_response((string)$row['UserID'], (int)$row['UserUID'], $passwordForGame));
