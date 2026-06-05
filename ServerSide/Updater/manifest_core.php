<?php
declare(strict_types=1);

// Shared manifest generation — included by manifest.php and launcher.php.
// Per-file SHA256 uses mtime-based cache in {dir}/.hashes/*.cache (format: "{mtime}:{hash}").

function generate_manifest_json(string $baseDir): string
{
    $filesDir     = $baseDir . DIRECTORY_SEPARATOR . 'Files';
    $patchesDir   = $baseDir . DIRECTORY_SEPARATOR . 'Patches';
    $hashesDir    = $filesDir . DIRECTORY_SEPARATOR . '.hashes';
    $manifestBase = $baseDir . DIRECTORY_SEPARATOR . 'manifest.json';

    $base = [
        'clientVersion'      => 0,
        'launcherVersion'    => 1,
        'minLauncherVersion' => 1,
        'maintenance'        => false,
        'maintenanceMessage' => '',
        'launcherFile'       => '',
        'launcherSha256'     => '',
        'patches'            => [],
        'files'              => [],
        'news'               => [],
    ];

    if (is_file($manifestBase)) {
        $raw = @file_get_contents($manifestBase);
        if ($raw !== false) {
            $raw = ltrim($raw, "\xEF\xBB\xBF");
            $decoded = json_decode($raw, true);
            if (is_array($decoded)) {
                foreach ($decoded as $k => $v) {
                    // files[] and patches[] are generated dynamically — skip static values.
                    // clientVersion is overridden below if patches are found.
                    if ($k !== 'files' && $k !== 'patches') {
                        $base[$k] = $v;
                    }
                }
            }
        }
    }

    // --- Scan Patches/ and generate patches[] ---
    $patches = _scan_patches($patchesDir);
    if (!empty($patches)) {
        $base['patches']       = $patches;
        $base['clientVersion'] = count($patches); // version = sort position (1-based)
    }

    // --- Scan Files/ and generate files[] ---
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
            if (preg_match('/(^|\/)\./', $rel)) continue;
            $found[] = [$abs, $rel];
        }
        usort($found, static function ($a, $b) { return strnatcasecmp($a[1], $b[1]); });

        foreach ($found as [$abs, $rel]) {
            $entries[] = [
                'path'     => $rel,
                'file'     => 'Files/' . $rel,
                'sha256'   => _manifest_cached_sha256($abs, $rel, $hashesDir),
                'size'     => (int) filesize($abs),
                'required' => true,
            ];
        }
    }
    $base['files'] = $entries;

    return (string) json_encode($base, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
}

// ---------------------------------------------------------------------------
// Scan Patches/ — returns patches[] sorted by natural filename order.
// Version = 1-based sort index (identical to tools/patch_hashes.php behaviour).
// SHA256 cached in Patches/.hashes/{filename}.cache → "{mtime}:{hash}".
// ---------------------------------------------------------------------------
function _scan_patches(string $patchesDir): array
{
    if (!is_dir($patchesDir)) return [];

    $hashesDir = $patchesDir . DIRECTORY_SEPARATOR . '.hashes';
    if (!is_dir($hashesDir)) @mkdir($hashesDir, 0755, true);

    $files = [];
    foreach (new DirectoryIterator($patchesDir) as $info) {
        if (!$info->isFile()) continue;
        if (strcasecmp($info->getExtension(), 'patch') !== 0) continue;
        $files[] = $info->getPathname();
    }

    if (empty($files)) return [];

    sort($files, SORT_NATURAL | SORT_FLAG_CASE);

    $version = 1;
    $items   = [];
    foreach ($files as $absPath) {
        $filename = basename($absPath);
        $items[] = [
            'version'   => $version++,
            'file'      => 'Patches/' . $filename,
            'sha256'    => _manifest_cached_sha256($absPath, $filename, $hashesDir),
            'size'      => (int) filesize($absPath),
            'encrypted' => false,
        ];
    }

    return $items;
}

// ---------------------------------------------------------------------------
// SHA256 with mtime-based cache.
// Cache file: {hashesDir}/{rel}.cache  →  content: "{mtime}:{sha256hex}"
// ---------------------------------------------------------------------------
function _manifest_cached_sha256(string $abs, string $rel, string $hashesDir): string
{
    $cacheFile = $hashesDir . DIRECTORY_SEPARATOR
               . str_replace('/', DIRECTORY_SEPARATOR, $rel) . '.cache';
    $mtime = (int) filemtime($abs);

    if (is_file($cacheFile)) {
        $data = @file_get_contents($cacheFile);
        if ($data !== false) {
            $sep = strpos($data, ':');
            if ($sep !== false && (int) substr($data, 0, $sep) === $mtime) {
                return substr($data, $sep + 1);
            }
        }
    }

    $hash        = (string) hash_file('sha256', $abs);
    $cacheSubDir = dirname($cacheFile);
    if (!is_dir($cacheSubDir)) @mkdir($cacheSubDir, 0755, true);
    @file_put_contents($cacheFile, $mtime . ':' . $hash, LOCK_EX);
    return $hash;
}
