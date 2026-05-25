<?php

declare(strict_types=1);

function updater_is_loopback_ip(string $ip): bool
{
    $ip = trim($ip);
    if ($ip === '') return false;
    return in_array($ip, ['127.0.0.1', '::1', '::ffff:127.0.0.1'], true);
}

function updater_is_local_host_name(string $host): bool
{
    $host = strtolower(trim($host));
    if ($host === '') return false;

    if (($p = strpos($host, ':')) !== false) {
        $host = substr($host, 0, $p);
    }

    return in_array($host, ['localhost', '127.0.0.1', '::1'], true);
}

function updater_enforce_local_only(): void
{
    if (PHP_SAPI === 'cli') return;

    $remoteAddr = isset($_SERVER['REMOTE_ADDR']) ? (string)$_SERVER['REMOTE_ADDR'] : '';
    $host = isset($_SERVER['HTTP_HOST']) ? (string)$_SERVER['HTTP_HOST'] : '';
    if ($host === '' && isset($_SERVER['SERVER_NAME'])) $host = (string)$_SERVER['SERVER_NAME'];

    $xff = isset($_SERVER['HTTP_X_FORWARDED_FOR']) ? (string)$_SERVER['HTTP_X_FORWARDED_FOR'] : '';
    $cf = isset($_SERVER['HTTP_CF_CONNECTING_IP']) ? (string)$_SERVER['HTTP_CF_CONNECTING_IP'] : '';

    $forwardedIp = '';
    if ($xff !== '') {
        $parts = explode(',', $xff);
        $forwardedIp = trim((string)$parts[0]);
    }
    if ($forwardedIp === '' && $cf !== '') $forwardedIp = trim($cf);

    $isLocalRemote = updater_is_loopback_ip($remoteAddr);
    $isLocalHost = updater_is_local_host_name($host);

    // If a proxy forwards original client IP, enforce it to be loopback as well.
    $isLocalForwarded = true;
    if ($forwardedIp !== '') {
        $isLocalForwarded = updater_is_loopback_ip($forwardedIp);
    }

    if ($isLocalRemote && $isLocalHost && $isLocalForwarded) return;

    http_response_code(403);
    header('Content-Type: application/json; charset=utf-8');
    header('X-Content-Type-Options: nosniff');
    echo json_encode([
        'ok' => false,
        'message' => 'This endpoint is localhost-only.',
    ], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}
