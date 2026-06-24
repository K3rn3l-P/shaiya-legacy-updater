<?php
// Liveness probe for the updater.
// The fact this script answers at all proves the website/API (apache) is up.
// "server" reflects whether the login server (ps_login) TCP port is reachable.
// No database access — keep this fast and dependency-free.

require_once __DIR__ . '/common.php';

$statusCfg = isset($config['status']) && is_array($config['status']) ? $config['status'] : [];
$host    = isset($statusCfg['login_host']) ? (string)$statusCfg['login_host'] : '127.0.0.1';
$port    = isset($statusCfg['login_port']) ? (int)$statusCfg['login_port'] : 30800;
$timeout = isset($statusCfg['timeout'])    ? (float)$statusCfg['timeout']   : 1.0;
$cacheTtl = isset($statusCfg['cache_ttl']) ? (int)$statusCfg['cache_ttl']   : 10;

// Cache the probe result so a flood of requests cannot turn each call into a blocking
// fsockopen (up to $timeout seconds when the server is down) — DoS amplification guard.
// Shared with modules/stats_logic.php (same temp file).
$cacheFile = sys_get_temp_dir() . DIRECTORY_SEPARATOR . 'shaiya_srv_status.json';
$serverUp = null;
if (is_file($cacheFile)) {
    $c = json_decode(@file_get_contents($cacheFile), true);
    if (is_array($c) && isset($c['ts'], $c['up']) && (time() - (int)$c['ts']) < $cacheTtl) {
        $serverUp = (bool)$c['up'];
    }
}

if ($serverUp === null) {
    $serverUp = false;
    $errno = 0;
    $errstr = '';
    $fp = @fsockopen($host, $port, $errno, $errstr, $timeout);
    if ($fp !== false) {
        $serverUp = true;
        @fclose($fp);
    }
    @file_put_contents($cacheFile, json_encode(['ts' => time(), 'up' => $serverUp]));
}

respond([
    'ok'     => true,
    'web'    => true,
    'server' => $serverUp,
    'ts'     => time(),
]);
