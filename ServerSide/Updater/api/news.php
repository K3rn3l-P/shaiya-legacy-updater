<?php
// Uses main website's db infrastructure (has PS_WebSite access)
$mainHtdocs = dirname(__DIR__, 2);
require_once $mainHtdocs . '/security_bootstrap.php';
appApplySecurityHeaders();
require_once $mainHtdocs . '/db_pdo.php';

header('Content-Type: application/json; charset=UTF-8');

if (!appRateLimitAllow('updater_news:' . appGetClientIp(), 30, 60)) {
    http_response_code(429);
    echo json_encode(['ok' => false, 'message' => 'Rate limit exceeded.']);
    exit;
}

function strip_preview_news(string $html, int $max = 220): string {
    $plain = strip_tags($html);
    $plain = preg_replace('/\s+/', ' ', $plain);
    $plain = html_entity_decode(trim($plain), ENT_QUOTES | ENT_HTML5, 'UTF-8');
    if (mb_strlen($plain) > $max) {
        $plain = mb_substr($plain, 0, $max) . '...';
    }
    return $plain;
}

try {
    $stmt = $pdoAdmin->query(
        "SELECT TOP 10 NewsID, Title, Content, Author, CreatedAt
         FROM PS_WebSite.dbo.Web_News
         WHERE IsHidden = 0
         ORDER BY CreatedAt DESC"
    );
    $rows = $stmt ? $stmt->fetchAll(PDO::FETCH_ASSOC) : [];
} catch (Throwable $e) {
    http_response_code(500);
    echo json_encode(['ok' => false, 'message' => 'Database error.']);
    exit;
}

$news = [];
foreach ($rows as $row) {
    $news[] = [
        'id'      => (int)$row['NewsID'],
        'title'   => (string)$row['Title'],
        'preview' => strip_preview_news((string)($row['Content'] ?? '')),
        'author'  => (string)$row['Author'],
        'date'    => date('F j, Y', strtotime($row['CreatedAt'])),
    ];
}

echo json_encode(['ok' => true, 'news' => $news], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
