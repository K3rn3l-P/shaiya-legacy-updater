USE PS_UserData;
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name=N'GameSecuritySessions' AND schema_id=SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.GameSecuritySessions (
        TokenHash   varbinary(32) NOT NULL CONSTRAINT PK_GSS PRIMARY KEY,
        UserUID     int           NOT NULL,
        UserID      nvarchar(50)  NOT NULL,
        ClientIP    nvarchar(45)  NULL,
        CreatedAt   datetime2(0)  NOT NULL CONSTRAINT DF_GSS_Created DEFAULT SYSUTCDATETIME(),
        ExpiresAt   datetime2(0)  NOT NULL
    );
    CREATE INDEX IX_GSS_Expiry ON dbo.GameSecuritySessions(ExpiresAt);
    PRINT 'Table created.';
END
ELSE
    PRINT 'Already exists.';

-- SecEventWriter: INSERT per logging + DELETE per invalidare sessioni al logout.
-- Senza DELETE il PLAYER_LEAVE_WORLD non pulisce GameSecuritySessions.
IF NOT EXISTS (
    SELECT 1 FROM sys.database_permissions
    WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID('SecEventWriter')
      AND major_id = OBJECT_ID('dbo.GameSecuritySessions')
      AND permission_name = 'DELETE'
      AND state = 'G'
)
BEGIN
    GRANT DELETE ON dbo.GameSecuritySessions TO [SecEventWriter];
    PRINT 'GRANT DELETE su GameSecuritySessions a SecEventWriter applicato.';
END
ELSE
    PRINT 'GRANT DELETE gia presente.';
