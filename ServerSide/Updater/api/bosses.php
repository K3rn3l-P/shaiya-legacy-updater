<?php
$mainHtdocs = dirname(__DIR__, 2);
require_once $mainHtdocs . '/security_bootstrap.php';
appApplySecurityHeaders();
require_once $mainHtdocs . '/db_pdo.php';

header('Content-Type: application/json; charset=UTF-8');

if (!appRateLimitAllow('updater_bosses:' . appGetClientIp(), 15, 60)) {
    http_response_code(429);
    echo json_encode(['ok' => false, 'message' => 'Rate limit exceeded.']);
    exit;
}

// Load configured bosses (MobID → RespawnSeconds)
$bosses = [];
try {
    $cfgStmt = $pdoAdmin->query(
        "SELECT MobID, RespawnSeconds FROM PS_WebSite.dbo.Web_Bosses WHERE IsActive = 1 ORDER BY SortOrder ASC, BossID ASC"
    );
    foreach ($cfgStmt->fetchAll(PDO::FETCH_ASSOC) as $r) {
        $bosses[(int)$r['MobID']] = (int)$r['RespawnSeconds'];
    }
} catch (Throwable $e) {}

if (empty($bosses)) {
    $bosses = [2469 => 54000, 835 => 21600, 1259 => 21600];
}

$bossIds = array_keys($bosses);
$ph      = implode(',', array_fill(0, count($bossIds), '?'));

// Mob names + maps
$mobInfo = [];
try {
    $s = $pdoAdmin->prepare(
        "SELECT m.MobID, m.MobName, m.Level, mn.MapName
         FROM PS_GameDefs.dbo.Mobs m
         LEFT JOIN PS_GameDefs.dbo.MapNames mn ON mn.MapID = m.MapID
         WHERE m.MobID IN ($ph)"
    );
    $s->execute($bossIds);
    foreach ($s->fetchAll(PDO::FETCH_ASSOC) as $row) {
        $mobInfo[(int)$row['MobID']] = [
            'name'    => mb_convert_encoding((string)$row['MobName'], 'UTF-8', 'Windows-1251'),
            'level'   => (int)($row['Level'] ?? 0),
            'mapName' => mb_convert_encoding((string)($row['MapName'] ?? ''), 'UTF-8', 'Windows-1251'),
        ];
    }
} catch (Throwable $e) {}

// Last death per boss
$bossDeaths = [];
try {
    $s = $pdoAdmin->prepare(
        "SELECT MobID, CharName, ActionTime
         FROM (
             SELECT MobID, CharName, ActionTime,
                    ROW_NUMBER() OVER (PARTITION BY MobID ORDER BY ActionTime DESC) AS rn
             FROM PS_GameLog.dbo.Boss_Death_Log
             WHERE MobID IN ($ph)
         ) t WHERE rn = 1"
    );
    $s->execute($bossIds);
    foreach ($s->fetchAll(PDO::FETCH_ASSOC) as $row) {
        $bossDeaths[(int)$row['MobID']] = $row;
    }
} catch (Throwable $e) {}

$now    = time();
$result = [];
foreach ($bosses as $mobId => $respawnSeconds) {
    $info       = $mobInfo[$mobId] ?? ['name' => 'Boss #' . $mobId, 'level' => 0, 'mapName' => ''];
    $death      = $bossDeaths[$mobId] ?? null;
    $lastKiller = '';
    $secondsUntilSpawn = 0; // 0 = alive / unknown

    if ($death) {
        $lastKiller        = mb_convert_encoding((string)$death['CharName'], 'UTF-8', 'Windows-1251');
        $deathTs           = strtotime((string)$death['ActionTime']);
        $nextSpawn         = $deathTs + $respawnSeconds;
        $remaining         = $nextSpawn - $now;
        if ($remaining > 0) $secondsUntilSpawn = (int)$remaining;
    }

    $result[] = [
        'name'              => $info['name'],
        'level'             => $info['level'],
        'mapName'           => $info['mapName'],
        'lastKilledBy'      => $lastKiller,
        'secondsUntilSpawn' => $secondsUntilSpawn,
        'respawnSeconds'    => $respawnSeconds,
    ];
}

echo json_encode(['ok' => true, 'bosses' => $result], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
