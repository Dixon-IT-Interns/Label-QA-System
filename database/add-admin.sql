USE [DixonLabelVerificationDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @FullName nvarchar(150) = N'Saksham Jain';
DECLARE @Username nvarchar(100) = N'saksham';
DECLARE @Email nvarchar(256) = N'sjsaksham.work@gmail.com';
DECLARE @PasswordHash nvarchar(512) = N'PASTE_RAW_BASE64_HASH_HERE';
DECLARE @UserId uniqueidentifier;
DECLARE @WasCreated bit = 0;

IF @PasswordHash = N'PASTE_RAW_BASE64_HASH_HERE'
   OR LEN(@PasswordHash) < 60
   OR LEN(@PasswordHash) % 4 <> 0
   OR @PasswordHash LIKE N'%[^A-Za-z0-9+/=]%'
    THROW 50001, 'Replace PasswordHash with the raw Base64 ASP.NET Identity hash.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ApplicationUsers', N'U') IS NULL
       OR OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
       OR COL_LENGTH(N'dbo.ApplicationUsers', N'BrandId') IS NULL
       OR COL_LENGTH(N'dbo.ApplicationUsers', N'IsActive') IS NOT NULL
    BEGIN
        THROW 50002, 'Run the current database initializer before adding an Admin.', 1;
    END;

    SELECT @UserId = Id
    FROM dbo.ApplicationUsers WITH (UPDLOCK, HOLDLOCK)
    WHERE Username = @Username;

    IF EXISTS
       (SELECT 1 FROM dbo.ApplicationUsers
        WHERE Email = @Email AND (@UserId IS NULL OR Id <> @UserId))
    BEGIN
        THROW 50003, 'The Admin email is already used by another account; no data was changed.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE Code = N'Admin')
        INSERT dbo.UserRoles(Code, Name) VALUES (N'Admin', N'Admin');

    IF @UserId IS NULL
    BEGIN
        INSERT dbo.ApplicationUsers (FullName, Username, Email, PasswordHash, Role, BrandId)
        VALUES (@FullName, @Username, @Email, @PasswordHash, N'Admin', NULL);

        SET @WasCreated = 1;
    END
    ELSE
    BEGIN
        UPDATE dbo.ApplicationUsers
        SET FullName = @FullName,
            Email = @Email,
            PasswordHash = @PasswordHash,
            Role = N'Admin',
            BrandId = NULL,
            PasswordResetTokenHash = NULL,
            PasswordResetExpiresAtUtc = NULL,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE Id = @UserId;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

IF @WasCreated = 1
    PRINT N'Admin account saksham was created.';
ELSE
    PRINT N'Existing saksham account was updated as Admin.';
GO
