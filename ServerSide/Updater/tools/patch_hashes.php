<?php
require_once dirname(__DIR__) . '/local_only.php';
updater_enforce_local_only();

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$baseDir = dirname(__DIR__);
$patchDir = $baseDir . DIRECTORY_SEPARATOR . 'Patches';

if (!is_dir($patchDir)) {
    http_response_code(500);
    echo json_encode([
        'ok' => false,
        'message' => 'Patches folder not found.',
        'path' => $patchDir,
    ], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

$items = [];
$it = new RecursiveIteratorIterator(
    new RecursiveDirectoryIterator($patchDir, FilesystemIterator::SKIP_DOTS)
);

$files = [];
foreach ($it as $fileInfo) {
    if (!$fileInfo->isFile()) continue;
    if (strcasecmp($fileInfo->getExtension(), 'patch') !== 0) continue;
    $files[] = $fileInfo->getPathname();
}

sort($files, SORT_NATURAL | SORT_FLAG_CASE);

$version = 1;
foreach ($files as $absPath) {
    $relative = str_replace('\\', '/', substr($absPath, strlen($patchDir) + 1));
    $items[] = [
        'version' => $version++,
        'file' => 'Patches/' . $relative,
        'sha256' => hash_file('sha256', $absPath),
        'size' => filesize($absPath),
        'encrypted' => false,
    ];
}

echo json_encode([
    'ok' => true,
    'message' => 'Patch entries generated. Copy "patches" into manifest.json.',
    'patches' => $items,
], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
