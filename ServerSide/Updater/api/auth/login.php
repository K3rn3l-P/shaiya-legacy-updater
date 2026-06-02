<?php
declare(strict_types=1);
require_once __DIR__ . '/../common.php';

$body = read_json_body();
$userId = clean_user_id((string)($body['userId'] ?? ''));
$password = clean_password((string)($body['password'] ?? ''));
$deviceId = clean_device_id((string)($body['deviceId'] ?? ''));
$saveAccount = !empty($body['saveAccount']);

$row = check_password_safe($userId, $password);
if (!$row) {
    respond(['ok' => false, 'message' => 'Invalid ID or password.']);
}

$userUid = (int)$row['UserUID'];
$status = (int)$row['Status'];
if (!ensure_active_login_row($row)) {
    respond(['ok' => false, 'message' => 'This account is not allowed to login. Status=' . $status]);
}

if ($saveAccount) {
    save_device_account($deviceId, $userUid, $userId);
}

// The game login server (sdev-login) now verifies credentials via PBKDF2 against Enpassword.
// verify_pbkdf2(Enpassword, received_password) expects the plaintext password as input.
// Returning the DB Pw field would fail because Pw != plaintext (e.g. Pw = UserUID "2").
$passwordForGame = $password;
respond(success_response($userId, $userUid, $passwordForGame));
