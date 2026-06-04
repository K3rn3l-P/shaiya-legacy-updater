<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$ip = client_ip();
$ipKey = 'login_ip:' . $ip;
$retryAfter = 0;
if (!updater_ratelimit_allow($ipKey, 20, 900, $retryAfter)) {
    header('Retry-After: ' . $retryAfter);
    respond(['ok' => false, 'message' => 'Too many requests.'], 429);
}

$body = read_json_body();
$userId   = clean_user_id((string)($body['userId'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
remove_saved_account($deviceId, $userId);
respond(['ok' => true, 'message' => 'Saved account removed.']);
