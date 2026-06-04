-- Tabella token monouso per saved-account login (updater)
-- Deploy: eseguire su PS_UserData

USE PS_UserData;
GO

IF OBJECT_ID('dbo.Updater_LoginToken', 'U') IS NULL
CREATE TABLE dbo.Updater_LoginToken (
    Token     varchar(16)  NOT NULL CONSTRAINT PK_Updater_LoginToken PRIMARY KEY,
    UserID    varchar(18)  NOT NULL,
    ExpiresAt datetime2(0) NOT NULL,
    CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- ps_login ODBC user — DELETE per consumare token, SELECT per OUTPUT clause, INSERT non necessario (PHP inserisce)
GRANT SELECT, INSERT, DELETE ON dbo.Updater_LoginToken TO [S4Ncu7@m0B3n3#];
GO

-- PHP API user — sostituire con il login SQL reale usato da config.php sul server XAMPP
-- GRANT SELECT, INSERT, DELETE ON dbo.Updater_LoginToken TO [php_api_user];
-- GO
