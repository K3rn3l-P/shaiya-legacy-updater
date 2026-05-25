<?php
require_once __DIR__ . '/local_only.php';
updater_enforce_local_only();

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$launcherIni = __DIR__ . DIRECTORY_SEPARATOR . 'launcher.ini';
if (!is_file($launcherIni)) {
    http_response_code(500);
    echo json_encode([
        'ok' => false,
        'message' => 'launcher.ini not found.'
    ], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

$cfg = parse_ini_file($launcherIni, true, INI_SCANNER_RAW);
if (!is_array($cfg)) {
    http_response_code(500);
    echo json_encode([
        'ok' => false,
        'message' => 'launcher.ini could not be parsed.'
    ], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

$required = [
    ['Server', 'BaseUrl'],
    ['Server', 'ManifestFile'],
    ['Game', 'Executable'],
    ['Game', 'Arguments'],
    ['Launcher', 'MinLauncherVersion'],
    ['Update', 'DownloadTimeoutSeconds'],
];

$missing = [];
foreach ($required as $pair) {
    [$section, $key] = $pair;
    if (!isset($cfg[$section]) || !array_key_exists($key, $cfg[$section])) {
        $missing[] = $section . '.' . $key;
    }
}

echo json_encode([
    'ok' => count($missing) === 0,
    'message' => count($missing) === 0 ? 'launcher.ini parsed successfully.' : 'launcher.ini parsed with missing keys.',
    'missing' => $missing,
    'sections' => array_keys($cfg)
], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
