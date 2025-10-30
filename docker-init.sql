USE master;
GO

-- Create user login if it does not exist yet
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'myuser')
BEGIN
    CREATE LOGIN [myuser] WITH PASSWORD = '123password%', CHECK_POLICY = OFF;
    --ALTER SERVER ROLE [sysadmin] ADD MEMBER [myuser];
    PRINT 'Login myuser created';
END
ELSE
BEGIN
  PRINT 'Login myuser already exists';
END
GO

-- Create a database if it does not exist yet
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'STAG-AUIUI-P8VT')
BEGIN
    CREATE DATABASE [STAG-AUIUI-P8VT];
    PRINT 'Database STAG-AUIUI-P8VT created';
END
ELSE
BEGIN
  PRINT 'Database STAG-AUIUI-P8VT already exists';
END
GO

USE [STAG-AUIUI-P8VT];
GO

-- Create user for the database if it does not exist yet
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'myuser')
BEGIN
    CREATE USER [myuser] FOR LOGIN [myuser];
    PRINT 'User myuser created in database STAG-AUIUI-P8VT';
END
ELSE
BEGIN
    PRINT 'User myuser already exists in database STAG-AUIUI-P8VT';
END
GO

-- Grant db_owner role to myuser if not already assigned
IF NOT EXISTS (
    SELECT * FROM sys.database_role_members
    WHERE role_principal_id = DATABASE_PRINCIPAL_ID('db_owner')
    AND member_principal_id = DATABASE_PRINCIPAL_ID('myuser')
)
BEGIN
    ALTER ROLE db_owner ADD MEMBER [myuser];
    PRINT 'Granted db_owner role to myuser in STAG-AUIUI-P8VT';
END
ELSE
BEGIN
    PRINT 'User myuser already has db_owner role in STAG-AUIUI-P8VT';
END
GO
