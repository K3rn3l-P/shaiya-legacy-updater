<?php
$mainHtdocs = dirname(__DIR__, 2);
require_once $mainHtdocs . '/security_bootstrap.php';
appApplySecurityHeaders();

header('Content-Type: application/json; charset=UTF-8');

if (!appRateLimitAllow('updater_rankings:' . appGetClientIp(), 15, 60)) {
    http_response_code(429);
    echo json_encode(['ok' => false, 'message' => 'Rate limit exceeded.']);
    exit;
}

// Serve from cache when fresh (avoids heavy multi-DB join on every request).
$cacheFile = sys_get_temp_dir() . '/sdu_rankings_' . md5(__FILE__) . '.json';
$cacheTtl  = 120; // seconds
if (file_exists($cacheFile) && (time() - filemtime($cacheFile)) < $cacheTtl) {
    echo file_get_contents($cacheFile);
    exit;
}

require_once $mainHtdocs . '/db_pdo.php';

$classes = [
    0 => [0 => 'Fighter', 1 => 'Defender', 5 => 'Priest'],
    1 => [2 => 'Ranger',  3 => 'Archer',   4 => 'Mage'],
    2 => [2 => 'Assassin',4 => 'Pagan',    5 => 'Oracle'],
    3 => [0 => 'Warrior', 1 => 'Guardian', 3 => 'Hunter'],
];

try {
    $stmt = $pdoAdmin->query("
        SELECT TOP 15
            c.CharName, c.Family, c.Job, c.Level, c.K1 AS kills,
            umg.Country, g.GuildName, c.LoginStatus
        FROM PS_GameData.dbo.Chars c
        JOIN PS_GameData.dbo.UserMaxGrow umg ON c.UserUID = umg.UserUID
        JOIN PS_UserData.dbo.Users_Master u   ON c.UserUID = u.UserUID
        LEFT JOIN PS_GameData.dbo.GuildChars gc ON c.CharID = gc.CharID
        LEFT JOIN PS_GameData.dbo.Guilds g      ON gc.GuildID = g.GuildID AND g.Del = 0
        WHERE c.Del = 0 AND c.K1 >= 0 AND umg.Country IN (0,1)
          AND u.Status NOT IN (16,32,48,64)
        ORDER BY c.K1 DESC, c.Level DESC
    ");
    $rows = $stmt ? $stmt->fetchAll(PDO::FETCH_ASSOC) : [];
} catch (Throwable $e) {
    http_response_code(500);
    echo json_encode(['ok' => false, 'message' => 'Database error.']);
    exit;
}

$result = [];
foreach ($rows as $i => $row) {
    $family    = (int)$row['Family'];
    $job       = (int)$row['Job'];
    $className = $classes[$family][$job] ?? 'Unknown';
    $result[]  = [
        'rank'      => $i + 1,
        'name'      => mb_convert_encoding((string)$row['CharName'], 'UTF-8', 'Windows-1251'),
        'className' => $className,
        'level'     => (int)$row['Level'],
        'kills'     => (int)$row['kills'],
        'guild'     => !empty($row['GuildName'])
                           ? mb_convert_encoding((string)$row['GuildName'], 'UTF-8', 'Windows-1251')
                           : '',
        'country'   => (int)$row['Country'],
        'online'    => (int)$row['LoginStatus'] === 1,
    ];
}

$json = json_encode(['ok' => true, 'rankings' => $result], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

// Atomic cache write.
$tmpFile = $cacheFile . '.tmp.' . getmypid();
if (@file_put_contents($tmpFile, $json) !== false) @rename($tmpFile, $cacheFile);

echo $json;
