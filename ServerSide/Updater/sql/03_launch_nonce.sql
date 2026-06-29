-- Tabella nonce launchToken single-use, user-bound (per-lancio).
-- Sostituisce il vecchio launchToken HMAC globale-per-finestra (replicabile).
-- Flusso: api/auth/launch-provision.php genera+INSERT (PHP user) ad ogni lancio;
--         sdev-login.dll (0xA101) verifica + UPDATE Used=1 (consumo single-use).
-- Deploy: eseguire su PS_UserData. Idempotente.

USE PS_UserData;
GO

IF OBJECT_ID('dbo.GameLaunchNonces', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GameLaunchNonces (
        UserUID   int          NOT NULL CONSTRAINT PK_GameLaunchNonces PRIMARY KEY,
        Nonce     bigint       NOT NULL,   -- uint32 (0..4294967295) → bigint
        ExpiresAt datetime2(0) NOT NULL,
        Used      bit          NOT NULL CONSTRAINT DF_GLN_Used DEFAULT 0,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_GLN_Created DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_GLN_Expiry ON dbo.GameLaunchNonces(ExpiresAt);
    PRINT 'Table dbo.GameLaunchNonces created.';
END
ELSE
    PRINT 'dbo.GameLaunchNonces already exists.';
GO

-- sdev-login ODBC user (SHAIYA_LOGDB_USER = SecEventWriter):
-- SELECT per leggere il nonce, UPDATE per marcarlo Used=1 (single-use atomico).
-- Il PHP API user (DB_USER) eredita INSERT/DELETE/SELECT dai ruoli db_datawriter/reader
-- (come gia avviene per dbo.GameSecuritySessions).
IF NOT EXISTS (
    SELECT 1 FROM sys.database_permissions
    WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID('SecEventWriter')
      AND major_id = OBJECT_ID('dbo.GameLaunchNonces')
      AND permission_name = 'SELECT' AND state = 'G'
)
BEGIN
    GRANT SELECT ON dbo.GameLaunchNonces TO [SecEventWriter];
    PRINT 'GRANT SELECT su GameLaunchNonces a SecEventWriter applicato.';
END
ELSE
    PRINT 'GRANT SELECT gia presente.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_permissions
    WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID('SecEventWriter')
      AND major_id = OBJECT_ID('dbo.GameLaunchNonces')
      AND permission_name = 'UPDATE' AND state = 'G'
)
BEGIN
    GRANT UPDATE ON dbo.GameLaunchNonces TO [SecEventWriter];
    PRINT 'GRANT UPDATE su GameLaunchNonces a SecEventWriter applicato.';
END
ELSE
    PRINT 'GRANT UPDATE gia presente.';
GO
