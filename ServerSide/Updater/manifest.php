<?php
declare(strict_types=1);
require_once __DIR__ . '/manifest_core.php';

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

echo generate_manifest_json(__DIR__);
