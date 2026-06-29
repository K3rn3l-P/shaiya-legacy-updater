<?php
// api/security_report.php — riceve tamper report da sdev-client.dll
// Autenticazione: secToken (64 hex) verificato via SHA256 → GameSecuritySessions
// Enforcement: legge [ANTI_TAMPER] da GMSecurity.ini → kick/ban/block HWID

require_once __DIR__ . '/common.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    respond(['ok' => false, 'message' => 'Method not allowed.'], 405);
}

$body = read_json_body();
if (empty($body)) {
    $body = $_POST;
}

$secToken = trim((string)($body['secToken'] ?? ''));
if (!preg_match('/^[a-fA-F0-9]{64}$/', $secToken)) {
    respond(['ok' => false, 'message' => 'Unauthorized.'], 403);
}

$flags  = (int)hexdec(substr(preg_replace('/[^a-fA-F0-9]/', '', (string)($body['flags'] ?? '00')), 0, 2));
$detail = substr(preg_replace('/[^\x20-\x7E]/', '', (string)($body['detail'] ?? '')), 0, 120);

// Lookup via HASHBYTES
$row = db_fetch_one(
    "SELECT TOP 1 UserUID, UserID FROM dbo.GameSecuritySessions
     WHERE TokenHash = HASHBYTES('SHA2_256', ?) AND ExpiresAt > SYSUTCDATETIME()",
    [$secToken]
);

if (!$row) {
    respond(['ok' => false, 'message' => 'Unauthorized.'], 403);
}

$userId  = (string)$row['UserID'];
$userUID = (int)$row['UserUID'];
$ip      = client_ip();

// Rate limit: max 30 report per token ogni 5 min
$retryAfter = 0;
if (!updater_ratelimit_allow('sr:' . substr($secToken, 0, 8), 30, 300, $retryAfter)) {
    header('Retry-After: ' . $retryAfter);
    respond(['ok' => false, 'message' => 'Rate limited.'], 429);
}

// Decodifica flags (bit map identico a sdev-client/src/packet.cpp)
$names = [];
if ($flags & 0x01) $names[] = 'DLL_INJECT';
if ($flags & 0x02) $names[] = 'DEBUGGER';
if ($flags & 0x04) $names[] = 'MEM_PATCH';
if ($flags & 0x08) $names[] = 'CHEAT_PROC';
if ($flags & 0x10) $names[] = 'HWID_TAMPERED';
if ($flags & 0x20) $names[] = 'HWID_UNSIGNED';
$flagStr = $names ? implode(',', $names) : 'UNKNOWN';

// HWID_TAMPERED indica che device.id è stato modificato manualmente (sig invalida).
// Trattalo sempre come BanOnDllInject equivalente (se enforcement attivo).
if ($flags & 0x10) {
    $flags |= 0x01; // forza DLL_INJECT bit per enforcement check sotto
}

$line = sprintf("[%s] TAMPER | ip=%-15s | user=%-20s | uid=%-6d | flags=0x%02X [%s] | %s\r\n",
    date('Y-m-d H:i:s'), $ip, $userId, $userUID, $flags, $flagStr, $detail);

$logfile = 'C:/ShaiyaServer/Logs/security_' . date('Y-m-d') . '.log';
$ok = file_put_contents($logfile, $line, FILE_APPEND | LOCK_EX);
if ($ok === false) {
    @mkdir(dirname(__DIR__) . '/logs', 0750, true);
    file_put_contents(dirname(__DIR__) . '/logs/security_' . date('Y-m-d') . '.log',
        $line, FILE_APPEND | LOCK_EX);
}

$dbDetail = sprintf('flags=0x%02X [%s] %s', $flags, $flagStr, $detail);
@db_query(
    "INSERT INTO dbo.SecurityEvents (Username, IP, EventType, Detail) VALUES (?, ?, 'TAMPER', ?)",
    [$userId, $ip, substr($dbDetail, 0, 255)]
);

// -------------------------------------------------------------------------
// Enforcement — legge [ANTI_TAMPER] da GMSecurity.ini
// -------------------------------------------------------------------------
$iniPath = 'C:/ShaiyaServer/PSM_Client/Bin/Data/GMSecurity.ini';
$cfg = @parse_ini_file($iniPath, true);
$at  = is_array($cfg) && isset($cfg['ANTI_TAMPER']) ? $cfg['ANTI_TAMPER'] : [];

$logOnly = isset($at['LogOnly']) ? (int)$at['LogOnly'] : 1;

if (!$logOnly && $flags !== 0) {
    $doKick = false;
    $doBan  = false;

    if (($flags & 0x02) && !empty($at['KickOnDebugger']))  $doKick = true;
    if (($flags & 0x08) && !empty($at['KickOnCheatProc'])) $doKick = true;
    if (($flags & 0x01) && !empty($at['KickOnDllInject'])) $doKick = true;
    if (($flags & 0x04) && !empty($at['KickOnMemPatch']))  $doKick = true;

    if (($flags & 0x02) && !empty($at['BanOnDebugger']))   $doBan = true;
    if (($flags & 0x08) && !empty($at['BanOnCheatProc']))  $doBan = true;
    if (($flags & 0x01) && !empty($at['BanOnDllInject']))  $doBan = true;
    if (($flags & 0x04) && !empty($at['BanOnMemPatch']))   $doBan = true;

    if ($doBan) {
        $banReason = 'AutoBan:TAMPER:flags=0x' . sprintf('%02X', $flags);

        @db_query(
            "INSERT INTO dbo.Users_Bann (UserUID, DaysBann, BanDate, Sbandate, Reason, GM_ID)
             VALUES (?, 3650, GETDATE(), DATEADD(YEAR,10,GETDATE()), ?, 'sdev-auto')",
            [$userUID, $banReason]
        );
        @db_query(
            "UPDATE dbo.Users_Master SET Status=-5, Leave=0 WHERE UserUID=?",
            [$userUID]
        );

        // Blocca HWID se BlockHwidOnBan=1
        $blockHwid = isset($at['BlockHwidOnBan']) ? (int)$at['BlockHwidOnBan'] : 1;
        if ($blockHwid) {
            $hrow = db_fetch_one(
                "SELECT LastHWID FROM dbo.Members WHERE UserID=?",
                [$userId]
            );
            if ($hrow && !empty($hrow['LastHWID'])) {
                $hwid = (string)$hrow['LastHWID'];
                @db_query(
                    "IF NOT EXISTS (SELECT 1 FROM dbo.BlockedHWID WHERE Hwid=?)
                     INSERT INTO dbo.BlockedHWID (Hwid, Reason)
                     VALUES (?, ?)",
                    [$hwid, $hwid, 'AutoBan:' . $flagStr]
                );
            }
        }

        // Invalida secToken
        @db_query(
            "DELETE FROM dbo.GameSecuritySessions WHERE UserUID=?",
            [$userUID]
        );

        $actLine = sprintf("[%s] ACTION=BAN | user=%-20s | uid=%-6d | flags=0x%02X [%s]\r\n",
            date('Y-m-d H:i:s'), $userId, $userUID, $flags, $flagStr);
        file_put_contents($logfile, $actLine, FILE_APPEND | LOCK_EX);
    }

    if ($doKick) {
        // Canale comandi sicuro: PSM_Cmd.usp_RunCommand (account Ernoweb@, tier PLAYER -> /kickuid ok).
        @db_query(
            "EXEC [PSM_Cmd].[dbo].[usp_RunCommand] @service=N'ps_game', @command=?",
            ['/kickuid ' . $userUID]
        );

        $actLine = sprintf("[%s] ACTION=KICK | user=%-20s | uid=%-6d | flags=0x%02X [%s]\r\n",
            date('Y-m-d H:i:s'), $userId, $userUID, $flags, $flagStr);
        file_put_contents($logfile, $actLine, FILE_APPEND | LOCK_EX);
    }
}

respond(['ok' => true]);
