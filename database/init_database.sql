/* ============================================================
   番茄专注 · 微信登录后端 数据库初始化脚本（幂等，可重复执行）
   适用：SQL Server 2016 及以上
   说明：程序启动时也会自动建库建表（Database:AutoInitialize），
         本脚本用于手动初始化或排查问题。
   ============================================================ */

IF DB_ID(N'ZhuanzhuWeb') IS NULL
BEGIN
    CREATE DATABASE [ZhuanzhuWeb];
END
GO

USE [ZhuanzhuWeb];
GO

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
        OpenId        NVARCHAR(64)  NOT NULL,
        UnionId       NVARCHAR(64)  NULL,
        SessionKey    NVARCHAR(256) NULL,
        NickName      NVARCHAR(64)  NOT NULL CONSTRAINT DF_AppUsers_NickName DEFAULT (N'微信用户'),
        CreateTime    DATETIME2(0)  NOT NULL CONSTRAINT DF_AppUsers_CreateTime DEFAULT (SYSDATETIME()),
        LastLoginTime DATETIME2(0)  NOT NULL CONSTRAINT DF_AppUsers_LastLoginTime DEFAULT (SYSDATETIME()),
        LastLoginIp   NVARCHAR(64)  NULL
    );
    PRINT '已创建表 dbo.AppUsers';
END
ELSE
BEGIN
    PRINT '表 dbo.AppUsers 已存在，跳过';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_AppUsers_OpenId' AND object_id = OBJECT_ID(N'dbo.AppUsers'))
BEGIN
    CREATE UNIQUE INDEX UX_AppUsers_OpenId ON dbo.AppUsers(OpenId);
    PRINT '已创建唯一索引 UX_AppUsers_OpenId';
END
GO

SELECT Id, OpenId, NickName, CreateTime, LastLoginTime FROM dbo.AppUsers ORDER BY Id;
GO
