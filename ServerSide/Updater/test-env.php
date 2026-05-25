<?php
require_once __DIR__ . '/local_only.php';
updater_enforce_local_only();

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$baseDir = __DIR__;
$apiDir = $baseDir . DIRECTORY_SEPARATOR . 'api';
$configPhp = $apiDir . DIRECTORY_SEPARATOR . 'config.php';
$configSample = $apiDir . DIRECTORY_SEPARATOR . 'config.sample.php';

$out = [
    'ok' => true,
    'phpVersion' => PHP_VERSION,
    'sqlsrvLoaded' => function_exists('sqlsrv_connect'),
    'paths' => [
        'baseDir' => $baseDir,
        'apiDir' => $apiDir,
    ],
    'files' => [
        'launcherPhp' => is_file($baseDir . DIRECTORY_SEPARATOR . 'launcher.php'),
        'launcherIni' => is_file($baseDir . DIRECTORY_SEPARATOR . 'launcher.ini'),
        'manifestJson' => is_file($baseDir . DIRECTORY_SEPARATOR . 'manifest.json'),
        'apiConfigPhp' => is_file($configPhp),
        'apiConfigSamplePhp' => is_file($configSample),
    ],
];

if (!$out['sqlsrvLoaded']) {
    $out['ok'] = false;
    $out['message'] = 'php_sqlsrv extension is not loaded.';
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
