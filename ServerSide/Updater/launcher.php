<?php
header('Content-Type: text/plain; charset=utf-8');
header('X-Content-Type-Options: nosniff');

$baseDir = __DIR__;
$realFile = $baseDir . DIRECTORY_SEPARATOR . 'launcher.ini';

if (!is_file($realFile)) {
    http_response_code(500);
    echo "; launcher.ini not found (launcher.sample.ini is example-only)\n";
    exit;
}

$contents = file_get_contents($realFile);
if ($contents === false) {
    http_response_code(500);
    echo "; failed to read launcher.ini\n";
    exit;
}

echo $contents;
