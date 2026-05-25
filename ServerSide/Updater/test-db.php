<?php
require_once __DIR__ . '/local_only.php';
updater_enforce_local_only();

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

require_once __DIR__ . '/api/common.php';

try {
    $row = db_fetch_one('SELECT DB_NAME() AS DbName');
    respond([
        'ok' => true,
        'message' => 'Database connection OK.',
        'database' => $row && isset($row['DbName']) ? (string)$row['DbName'] : 'unknown'
    ]);
} catch (Throwable $t) {
    respond([
        'ok' => false,
        'message' => 'Database test failed.',
        'detail' => $t->getMessage()
    ], 500);
}
