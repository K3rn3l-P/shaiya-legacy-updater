<?php
require_once __DIR__ . '/common.php';

$retryAfter = 0;
if (!updater_ratelimit_allow('news_api:' . client_ip(), 30, 60, $retryAfter)) {
    header('Retry-After: ' . $retryAfter);
    respond(['ok' => false, 'message' => 'Rate limit exceeded.'], 429);
}

function strip_preview(string $html, int $max = 220): string {
    $plain = strip_tags($html);
    $plain = preg_replace('/\s+/', ' ', $plain);
    $plain = html_entity_decode(trim($plain), ENT_QUOTES | ENT_HTML5, 'UTF-8');
    if (mb_strlen($plain) > $max) {
        $plain = mb_substr($plain, 0, $max) . '...';
    }
    return $plain;
}

$stmt = db_query(
    "SELECT TOP 10 NewsID, Title, Content, Author, CreatedAt
     FROM PS_WebSite.dbo.Web_News WITH (NOLOCK)
     WHERE IsHidden = 0
     ORDER BY CreatedAt DESC"
);

$news = [];
while (($row = sqlsrv_fetch_array($stmt, SQLSRV_FETCH_ASSOC)) !== null) {
    if ($row === false) continue;
    $createdAt = $row['CreatedAt'];
    if ($createdAt instanceof DateTime) {
        $dateStr = $createdAt->format('F j, Y');
    } else {
        $dateStr = date('F j, Y', strtotime((string)$createdAt));
    }
    $news[] = [
        'id'      => (int)$row['NewsID'],
        'title'   => (string)$row['Title'],
        'preview' => strip_preview((string)($row['Content'] ?? '')),
        'author'  => (string)$row['Author'],
        'date'    => $dateStr,
    ];
}

respond(['ok' => true, 'news' => $news]);
