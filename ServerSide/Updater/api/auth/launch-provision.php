<?php
// api/auth/launch-provision.php — provisioning per-lancio ("Start Game").
//
// Risolve la fragilita delle credenziali one-shot: l'updater chiama questo
// endpoint PRIMA di ogni avvio di game.exe usando il secToken di sessione
// (24h, GameSecuritySessions). Restituisce launchToken (nonce single-use
// user-bound) + sahKey freschi, cosi ogni lancio ha credenziali valide senza
// dover ri-loggare. Fail-closed: secToken assente/scaduto -> 401 (re-login).
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    respond(['ok' => false, 'message' => 'Method not allowed.'], 405);
}

$body     = read_json_body();
$secToken = trim((string)($body['secToken'] ?? ''));
if (!preg_match('/^[a-fA-F0-9]{64}$/', $secToken)) {
    respond(['ok' => false, 'message' => 'Re-login required.'], 401);
}

// Per-token rate limit: max 30 provision ogni 5 min (allineato a security_report.php)
$retryAfter = 0;
if (!updater_ratelimit_allow('lp:' . substr($secToken, 0, 8), 30, 300, $retryAfter)) {
    header('Retry-After: ' . $retryAfter);
    respond(['ok' => false, 'message' => 'Too many requests. Try again later.'], 429);
}

// Auth via secToken (stessa lookup di security_report.php)
$row = db_fetch_one(
    "SELECT TOP 1 UserUID, UserID FROM dbo.GameSecuritySessions
     WHERE TokenHash = HASHBYTES('SHA2_256', ?) AND ExpiresAt > SYSUTCDATETIME()",
    [$secToken]
);
if (!$row) {
    respond(['ok' => false, 'message' => 'Re-login required.'], 401);
}

$userUid = (int)$row['UserUID'];

// Richeck stato account ad ogni lancio: un ban durante la sessione (es. anti-tamper
// imposta Status=-5) deve bloccare anche i rilanci. Sessione revocata se non valida.
$urow = db_fetch_one(
    "SELECT TOP 1 Status, Leave FROM dbo.Users_Master WITH (NOLOCK) WHERE UserUID = ?",
    [$userUid]
);
if (!$urow || !ensure_active_login_row($urow)) {
    @db_query("DELETE FROM dbo.GameSecuritySessions WHERE UserUID = ?", [$userUid]);
    respond(['ok' => false, 'message' => 'This account is not allowed to login.'], 403);
}

$resp = ['ok' => true];

// launchToken: nonce single-use user-bound (Fix C ATTIVO). Consumato da sdev-login
// (consume_launch_nonce) al 0xA101 se GMSecurity.ini [SETTINGS] UseLaunchNonce=1.
// Rollback: rimettere get_launch_token() + UseLaunchNonce=0 (torna HMAC, no restart).
$launchToken = generate_and_save_launch_nonce($userUid);
if ($launchToken !== null) {
    $resp['launchToken'] = $launchToken;
}

// sahKey: chiave AES per data.sah (statica, da config server)
$sahKey = get_sah_key_hex();
if ($sahKey !== null) {
    $resp['sahKey'] = $sahKey;
}

// Sliding session: estende la validita del secToken corrente (rilanci nel tempo
// senza re-login finche la sessione resta attiva).
@db_query(
    "UPDATE dbo.GameSecuritySessions
     SET ExpiresAt = DATEADD(HOUR, 4, SYSUTCDATETIME())
     WHERE TokenHash = HASHBYTES('SHA2_256', ?)",
    [$secToken]
);

respond($resp);
