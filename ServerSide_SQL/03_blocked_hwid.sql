-- 03_blocked_hwid.sql
-- Tabella HWID bannati + stored procedure sp_BanUser
-- Usata da: sdev-login.dll (check al login)
--           security_report.php (ban su tamper report)
--           sdev.dll etain_shield (via SecEventWriter ODBC)
--
-- Eseguire su: PS_UserData
-- Richiede: SQL Server 2016+ (CREATE OR ALTER PROCEDURE)

USE [PS_UserData];
GO

-- ---------------------------------------------------------------------------
-- Tabella: dbo.BlockedHWID
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.BlockedHWID', N'U') IS NULL
    CREATE TABLE dbo.BlockedHWID (
        Hwid      varchar(32)  NOT NULL
                               CONSTRAINT PK_BlockedHWID PRIMARY KEY,
        Reason    varchar(256) NULL,
        CreatedAt datetime2(0) NOT NULL
                               CONSTRAINT DF_BHWID_CA DEFAULT SYSUTCDATETIME(),
        ExpiresAt datetime2(0) NULL  -- NULL = ban permanente
    );
GO

-- ---------------------------------------------------------------------------
-- Stored procedure: dbo.sp_BanUser
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_BanUser
    @UserUID  int,
    @DaysBann int,
    @Reason   nvarchar(255),
    @GMID     nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Users_Bann
        (UserUID, DaysBann, BanDate, Sbandate, Reason, GM_ID)
    VALUES
        (@UserUID,
         @DaysBann,
         GETDATE(),
         DATEADD(DAY, @DaysBann, GETDATE()),
         @Reason,
         @GMID);

    UPDATE dbo.Users_Master
       SET Status = -5,
           Leave  = 0
     WHERE UserUID = @UserUID;
END;
GO

-- ---------------------------------------------------------------------------
-- Permessi: SecEventWriter può chiamare sp_BanUser
-- (niente UPDATE diretto su Users_Master)
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'SecEventWriter')
    GRANT EXECUTE ON dbo.sp_BanUser TO [SecEventWriter];
GO
