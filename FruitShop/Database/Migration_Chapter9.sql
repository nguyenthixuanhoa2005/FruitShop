-- Migration Chương 9: Mật khẩu mạnh, chống dò mật khẩu và bảo vệ tài khoản quản trị
-- Chạy an toàn nhiều lần (idempotent) trên SQL Server database fruit_shop

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.users', 'failed_login_count') IS NULL
    ALTER TABLE dbo.users ADD failed_login_count INT NOT NULL CONSTRAINT DF_users_failed_login_count DEFAULT 0;

IF COL_LENGTH('dbo.users', 'lockout_end') IS NULL
    ALTER TABLE dbo.users ADD lockout_end DATETIME2 NULL;

IF COL_LENGTH('dbo.users', 'last_password_changed_at') IS NULL
BEGIN
    ALTER TABLE dbo.users ADD last_password_changed_at DATETIME2 NULL;
    EXEC('UPDATE dbo.users SET last_password_changed_at = ISNULL(created_at, GETUTCDATE()) WHERE last_password_changed_at IS NULL');
END

IF COL_LENGTH('dbo.users', 'two_factor_enabled') IS NULL
    ALTER TABLE dbo.users ADD two_factor_enabled BIT NOT NULL CONSTRAINT DF_users_two_factor_enabled DEFAULT 0;

IF COL_LENGTH('dbo.users', 'two_factor_secret') IS NULL
    ALTER TABLE dbo.users ADD two_factor_secret NVARCHAR(128) NULL;

IF OBJECT_ID('dbo.password_histories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.password_histories (
        id INT IDENTITY(1,1) PRIMARY KEY,
        user_id INT NOT NULL,
        password_hash NVARCHAR(255) NOT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_password_histories_created_at DEFAULT GETUTCDATE(),
        CONSTRAINT FK_password_histories_users FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE CASCADE
    );

    CREATE INDEX idx_password_histories_user_created ON dbo.password_histories(user_id, created_at DESC);
END
GO
