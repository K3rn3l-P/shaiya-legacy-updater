<?php
declare(strict_types=1);

// Serve manifest.json dinamicamente: base config da manifest.json + files[] auto-generato da Files/.
// SHA256 cache in Files/.hashes/ — ricalcola solo se mtime del file cambia.

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

$baseDir      = __DIR__;
$filesDir     = $baseDir . DIRECTORY_SEPARATOR . 'Files';
$hashesDir    = $filesDir . DIRECTORY_SEPARATOR . '.hashes';
$manifestBase = $baseDir . DIRECTORY_SEPARATOR . 'manifest.json';

// --- Carica manifest base (clientVersion, patches, news, ecc.) ---
$base = [
    'clientVersion'      => 0,
    'launcherVersion'    => 1,
    'minLauncherVersion' => 1,
    'maintenance'        => false,
    'maintenanceMessage' => '',
    'launcherFile'       => '',
    'patches'            => [],
    'files'              => [],
    'news'               => [],
];
if (is_file($manifestBase)) {
    $raw = @file_get_contents($manifestBase);
    if ($raw !== false) {
        // Rimuovi BOM se presente
        $raw = ltrim($raw, "\xEF\xBB\xBF");
        $decoded = json_decode($raw, true);
        if (is_array($decoded)) {
            // Merge: base mantiene defaults, manifest.json sovrascrive
            foreach ($decoded as $k => $v) {
                if ($k !== 'files') $base[$k] = $v; // files[] viene generato dinamicamente
            }
        }
    }
}

// --- Scan Files/ e genera files[] ---
$entries = [];
if (is_dir($filesDir)) {
    if (!is_dir($hashesDir)) @mkdir($hashesDir, 0755, true);

    $it = new RecursiveIteratorIterator(
        new RecursiveDirectoryIterator($filesDir, FilesystemIterator::SKIP_DOTS)
    );

    $found = [];
    foreach ($it as $info) {
        if (!$info->isFile()) continue;
        $abs = str_replace('/', DIRECTORY_SEPARATOR, $info->getPathname());
        $rel = str_replace('\\', '/', substr($abs, strlen($filesDir) + 1));
        // Salta .hashes/ e qualsiasi dotfile/dotdirectory
        if (preg_match('/(^|\/)\./', $rel)) continue;
        $found[] = [$abs, $rel];
    }
    usort($found, function ($a, $b) { return strnatcasecmp($a[1], $b[1]); });

    foreach ($found as [$abs, $rel]) {
        $entries[] = [
            'path'     => $rel,
            'file'     => 'Files/' . $rel,
            'sha256'   => cached_sha256($abs, $rel, $hashesDir),
            'size'     => (int) filesize($abs),
            'required' => true,
        ];
    }
}

$base['files'] = $entries;
echo json_encode($base, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);

// ---------------------------------------------------------------------------
// Helper: SHA256 con cache basata su mtime
// Cache file: Files/.hashes/{rel}.cache  →  contenuto: "{mtime}:{hash}"
// ---------------------------------------------------------------------------
function cached_sha256(string $abs, string $rel, string $hashesDir): string
{
    $cacheFile = $hashesDir . DIRECTORY_SEPARATOR
               . str_replace('/', DIRECTORY_SEPARATOR, $rel) . '.cache';
    $mtime = (int) filemtime($abs);

    if (is_file($cacheFile)) {
        $data = @file_get_contents($cacheFile);
        if ($data !== false) {
            $sep = strpos($data, ':');
            if ($sep !== false && (int) substr($data, 0, $sep) === $mtime) {
                return substr($data, $sep + 1); // cache hit
            }
        }
    }

    // Cache miss — ricalcola
    $hash = hash_file('sha256', $abs);
    $cacheSubDir = dirname($cacheFile);
    if (!is_dir($cacheSubDir)) @mkdir($cacheSubDir, 0755, true);
    @file_put_contents($cacheFile, $mtime . ':' . $hash, LOCK_EX);
    return $hash;
}
