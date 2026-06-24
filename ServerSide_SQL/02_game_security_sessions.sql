USE [PS_UserData]
GO

/*
    Security session tokens for sdev-client.dll tamper reporting.

    Flow:
      1. PHP login API generates a 64-hex random token per session.
      2. SHA256(token) stored here as varbinary(32) — raw token never touches DB.
      3. Token passed to game.exe via SDEV_SEC_TOKEN env var (deleted at DLL_PROCESS_ATTACH).
      4. sdev-client sends HTTP POST to /api/security_report.php with token in JSON body.
      5. PHP does HASHBYTES('SHA2_256', token) lookup — match = authenticated report.
      6. Token expires after 24h. Previous token revoked on next login (one token per user).

    If SDEV_SEC_TOKEN is absent at DLL attach → game exits immediately.
    Game can ONLY be launched through the updater.
*/

IF OBJECT_ID(N'dbo.GameSecuritySessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GameSecuritySessions
    (
        TokenHash   varbinary(32) NOT NULL,
        UserUID     int           NOT NULL,
        UserID      nvarchar(50)  NOT NULL,
        ClientIP    nvarchar(45)  NULL,
        CreatedAt   datetime2(0)  NOT NULL CONSTRAINT DF_GSS_CreatedAt DEFAULT SYSUTCDATETIME(),
        ExpiresAt   datetime2(0)  NOT NULL,
        CONSTRAINT PK_GameSecuritySessions PRIMARY KEY (TokenHash)
    );

    CREATE INDEX IX_GSS_Expiry ON dbo.GameSecuritySessions(ExpiresAt);
END
GO
