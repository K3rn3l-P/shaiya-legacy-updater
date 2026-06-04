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
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
respond(['ok' => true, 'accounts' => read_saved_accounts($deviceId)]);
