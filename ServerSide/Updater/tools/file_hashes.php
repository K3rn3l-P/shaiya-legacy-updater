<?php
require_once dirname(__DIR__) . '/local_only.php';
updater_enforce_local_only();

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$baseDir = dirname(__DIR__);
$filesDir = $baseDir . DIRECTORY_SEPARATOR . 'Files';

if (!is_dir($filesDir)) {
    http_response_code(500);
    echo json_encode([
        'ok' => false,
        'message' => 'Files folder not found.',
        'path' => $filesDir,
    ], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

$items = [];
$it = new RecursiveIteratorIterator(
    new RecursiveDirectoryIterator($filesDir, FilesystemIterator::SKIP_DOTS)
);

$files = [];
foreach ($it as $fileInfo) {
    if (!$fileInfo->isFile()) continue;
    $files[] = $fileInfo->getPathname();
}

sort($files, SORT_NATURAL | SORT_FLAG_CASE);

foreach ($files as $absPath) {
    $relative = str_replace('\\', '/', substr($absPath, strlen($filesDir) + 1));
    $items[] = [
        'path' => $relative,
        'file' => 'Files/' . $relative,
        'sha256' => hash_file('sha256', $absPath),
        'size' => filesize($absPath),
        'required' => true,
    ];
}

echo json_encode([
    'ok' => true,
    'message' => 'Repair file entries generated. Copy "files" into manifest.json.',
    'files' => $items,
], JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
