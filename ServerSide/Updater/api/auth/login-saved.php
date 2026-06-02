<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$body = read_json_body();
$userId = clean_user_id((string)($body['userId'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));

$row = saved_account_is_allowed($deviceId, $userId);
if (!$row) {
    respond(['ok' => false, 'message' => 'Saved-account login is not available. Please enter your password again.']);
}
if (!ensure_active_login_row($row)) {
    respond(['ok' => false, 'message' => 'This account is not allowed to login. Status=' . (int)$row['Status']]);
}

// TODO: Saved-account login cannot use the Pw field as passwordForGame now that sdev-login
// uses PBKDF2: verify_pbkdf2(Enpassword, received) expects the plaintext password.
// Pw != plaintext, so PBKDF2 would always fail. A one-time session token system is required:
//   1. PHP generates a random 16-byte token, stores it in an Updater_LoginToken table with expiry.
//   2. PHP returns the token as passwordForGame.
//   3. C++ login server checks the token table before PBKDF2 (token auth path).
// For now, force the user to re-enter their password.
respond(['ok' => false, 'message' => 'Saved-account login requires re-entering your password (PBKDF2 upgrade pending token support).']);

save_device_account($deviceId, (int)$row['UserUID'], (string)$row['UserID']);
respond(success_response((string)$row['UserID'], (int)$row['UserUID'], $passwordForGame));
