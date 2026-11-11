/*
  Dixon Label Verification database initializer.
  Run using SQL Server Management Studio or sqlcmd with an account allowed to create databases.
  The application creates the first administrator separately via --bootstrap-admin.
*/
USE [master];
GO
IF DB_ID(N'DixonLabelVerificationDB') IS NULL
BEGIN
    CREATE DATABASE [DixonLabelVerificationDB];
END;
GO

USE [DixonLabelVerificationDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Brands', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Brands
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_Brands_Id DEFAULT NEWSEQUENTIALID(),
            Code nvarchar(50) NOT NULL,
            Name nvarchar(150) NOT NULL,
            Description nvarchar(500) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_Brands_IsActive DEFAULT (1),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_Brands_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_Brands PRIMARY KEY (Id)
        );
        CREATE UNIQUE INDEX IX_Brands_Code ON dbo.Brands(Code);
        CREATE UNIQUE INDEX IX_Brands_Name ON dbo.Brands(Name);
    END;

    IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.UserRoles
        (
            Code nvarchar(20) NOT NULL,
            Name nvarchar(50) NOT NULL,
            CONSTRAINT PK_UserRoles PRIMARY KEY (Code)
        );
    END;

    IF OBJECT_ID(N'dbo.ApplicationUsers', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ApplicationUsers
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_ApplicationUsers_Id DEFAULT NEWSEQUENTIALID(),
            FullName nvarchar(150) NOT NULL,
            Username nvarchar(100) NOT NULL,
            Email nvarchar(256) NOT NULL,
            PasswordHash nvarchar(512) NOT NULL,
            Role nvarchar(20) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_ApplicationUsers_IsActive DEFAULT (1),
            LastLoginAtUtc datetimeoffset(7) NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_ApplicationUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_ApplicationUsers PRIMARY KEY (Id),
            CONSTRAINT CK_ApplicationUsers_Role CHECK (Role IN (N'Admin', N'Operator', N'Viewer')),
            CONSTRAINT FK_ApplicationUsers_UserRoles_Role FOREIGN KEY (Role) REFERENCES dbo.UserRoles(Code)
        );
        CREATE UNIQUE INDEX IX_ApplicationUsers_Username ON dbo.ApplicationUsers(Username);
        CREATE UNIQUE INDEX IX_ApplicationUsers_Email ON dbo.ApplicationUsers(Email);
    END;

    IF OBJECT_ID(N'dbo.LaptopModels', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LaptopModels
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_LaptopModels_Id DEFAULT NEWSEQUENTIALID(),
            BrandId uniqueidentifier NOT NULL,
            ModelNumber nvarchar(100) NOT NULL,
            ModelName nvarchar(200) NOT NULL,
            Description nvarchar(500) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_LaptopModels_IsActive DEFAULT (1),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_LaptopModels_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_LaptopModels PRIMARY KEY (Id),
            CONSTRAINT AK_LaptopModels_BrandId_Id UNIQUE (BrandId, Id),
            CONSTRAINT FK_LaptopModels_Brands_BrandId FOREIGN KEY (BrandId) REFERENCES dbo.Brands(Id)
        );
        CREATE UNIQUE INDEX IX_LaptopModels_BrandId_ModelNumber ON dbo.LaptopModels(BrandId, ModelNumber);
    END;

    IF OBJECT_ID(N'dbo.LabelProfiles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LabelProfiles
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_LabelProfiles_Id DEFAULT NEWSEQUENTIALID(),
            BrandId uniqueidentifier NOT NULL,
            ModelId uniqueidentifier NULL,
            LabelType nvarchar(20) NOT NULL,
            ProfileName nvarchar(100) NOT NULL,
            Name nvarchar(200) NOT NULL,
            ProfileVersion int NOT NULL CONSTRAINT DF_LabelProfiles_ProfileVersion DEFAULT (1),
            IsActive bit NOT NULL CONSTRAINT DF_LabelProfiles_IsActive DEFAULT (1),
            EffectiveFromUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_LabelProfiles_EffectiveFromUtc DEFAULT SYSUTCDATETIME(),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_LabelProfiles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_LabelProfiles PRIMARY KEY (Id),
            CONSTRAINT CK_LabelProfiles_LabelType CHECK (LabelType IN (N'CO', N'CONFIG')),
            CONSTRAINT FK_LabelProfiles_Brands_BrandId FOREIGN KEY (BrandId) REFERENCES dbo.Brands(Id),
            CONSTRAINT FK_LabelProfiles_LaptopModels_BrandId_ModelId FOREIGN KEY (BrandId, ModelId) REFERENCES dbo.LaptopModels(BrandId, Id)
        );
        CREATE UNIQUE INDEX IX_LabelProfiles_BrandId_ModelId_LabelType_ProfileName_ProfileVersion
            ON dbo.LabelProfiles(BrandId, ModelId, LabelType, ProfileName, ProfileVersion)
            WHERE ModelId IS NOT NULL;
        CREATE UNIQUE INDEX IX_LabelProfiles_BrandId_LabelType_ProfileName_ProfileVersion
            ON dbo.LabelProfiles(BrandId, LabelType, ProfileName, ProfileVersion)
            WHERE ModelId IS NULL;
        CREATE UNIQUE INDEX IX_LabelProfiles_ModelId_LabelType
            ON dbo.LabelProfiles(ModelId, LabelType)
            WHERE IsActive = 1 AND ModelId IS NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.FieldDefinitions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FieldDefinitions
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_FieldDefinitions_Id DEFAULT NEWSEQUENTIALID(),
            LabelProfileId uniqueidentifier NOT NULL,
            FieldKey nvarchar(100) NOT NULL,
            DisplayName nvarchar(200) NOT NULL,
            DataType nvarchar(50) NOT NULL CONSTRAINT DF_FieldDefinitions_DataType DEFAULT (N'Text'),
            IsRequired bit NOT NULL CONSTRAINT DF_FieldDefinitions_IsRequired DEFAULT (1),
            IsCritical bit NOT NULL CONSTRAINT DF_FieldDefinitions_IsCritical DEFAULT (0),
            OCRConfidenceThreshold float NOT NULL CONSTRAINT DF_FieldDefinitions_OCRConfidenceThreshold DEFAULT (0.9),
            DisplayOrder int NOT NULL CONSTRAINT DF_FieldDefinitions_DisplayOrder DEFAULT (0),
            IsActive bit NOT NULL CONSTRAINT DF_FieldDefinitions_IsActive DEFAULT (1),
            ComparisonStrategy nvarchar(30) NOT NULL CONSTRAINT DF_FieldDefinitions_ComparisonStrategy DEFAULT (N'NormalizedText'),
            NumericAbsoluteTolerance decimal(18,6) NOT NULL CONSTRAINT DF_FieldDefinitions_NumericAbsoluteTolerance DEFAULT (0),
            NumericRelativeTolerance decimal(18,6) NOT NULL CONSTRAINT DF_FieldDefinitions_NumericRelativeTolerance DEFAULT (0),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_FieldDefinitions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_FieldDefinitions PRIMARY KEY (Id),
            CONSTRAINT CK_FieldDefinitions_MinimumConfidence CHECK (OCRConfidenceThreshold >= 0 AND OCRConfidenceThreshold <= 1),
            CONSTRAINT CK_FieldDefinitions_NumericAbsoluteTolerance CHECK (NumericAbsoluteTolerance >= 0),
            CONSTRAINT CK_FieldDefinitions_NumericRelativeTolerance CHECK (NumericRelativeTolerance >= 0 AND NumericRelativeTolerance <= 1),
            CONSTRAINT CK_FieldDefinitions_ComparisonStrategy CHECK (ComparisonStrategy IN (N'Exact', N'NormalizedText', N'CaseInsensitive', N'Numeric')),
            CONSTRAINT FK_FieldDefinitions_LabelProfiles_LabelProfileId FOREIGN KEY (LabelProfileId) REFERENCES dbo.LabelProfiles(Id)
        );
        CREATE UNIQUE INDEX IX_FieldDefinitions_LabelProfileId_FieldKey ON dbo.FieldDefinitions(LabelProfileId, FieldKey);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.FieldDefinitions')
          AND name = N'CK_FieldDefinitions_ComparisonStrategy'
    )
    BEGIN
        ALTER TABLE dbo.FieldDefinitions DROP CONSTRAINT CK_FieldDefinitions_ComparisonStrategy;
    END;
    ALTER TABLE dbo.FieldDefinitions
        ADD CONSTRAINT CK_FieldDefinitions_ComparisonStrategy
        CHECK (ComparisonStrategy IN (
            N'Exact', N'NormalizedText', N'CaseInsensitive', N'Numeric', N'Identifier', N'MacAddress'
        ));

    IF OBJECT_ID(N'dbo.FieldAliases', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FieldAliases
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_FieldAliases_Id DEFAULT NEWSEQUENTIALID(),
            FieldDefinitionId uniqueidentifier NOT NULL,
            AliasText nvarchar(200) NOT NULL,
            Language nvarchar(20) NOT NULL CONSTRAINT DF_FieldAliases_Language DEFAULT (N'en'),
            IsActive bit NOT NULL CONSTRAINT DF_FieldAliases_IsActive DEFAULT (1),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_FieldAliases_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_FieldAliases PRIMARY KEY (Id),
            CONSTRAINT FK_FieldAliases_FieldDefinitions_FieldDefinitionId FOREIGN KEY (FieldDefinitionId) REFERENCES dbo.FieldDefinitions(Id)
        );
        CREATE UNIQUE INDEX IX_FieldAliases_FieldDefinitionId_AliasText_Language
            ON dbo.FieldAliases(FieldDefinitionId, AliasText, Language);
    END;

    IF OBJECT_ID(N'dbo.InspectionSessions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InspectionSessions
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_InspectionSessions_Id DEFAULT NEWSEQUENTIALID(),
            BrandId uniqueidentifier NOT NULL,
            ModelId uniqueidentifier NOT NULL,
            CreatedByUserId uniqueidentifier NOT NULL,
            LabelProfileCOId uniqueidentifier NULL,
            LabelProfileConfigId uniqueidentifier NULL,
            InspectionNumber nvarchar(40) NOT NULL,
            ProductionOrderNumber nvarchar(100) NULL,
            WorkOrderNumber nvarchar(100) NULL,
            Status nvarchar(32) NOT NULL CONSTRAINT DF_InspectionSessions_Status DEFAULT (N'READY'),
            COStatus nvarchar(32) NOT NULL CONSTRAINT DF_InspectionSessions_COStatus DEFAULT (N'READY'),
            ConfigStatus nvarchar(32) NOT NULL CONSTRAINT DF_InspectionSessions_ConfigStatus DEFAULT (N'READY'),
            SerialStatus nvarchar(32) NOT NULL CONSTRAINT DF_InspectionSessions_SerialStatus DEFAULT (N'NOT_APPLICABLE'),
            SerialNumber nvarchar(200) NULL,
            StartedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionSessions_StartedAtUtc DEFAULT SYSUTCDATETIME(),
            CompletedAtUtc datetimeoffset(7) NULL,
            ProcessingDurationMs bigint NULL,
            FinalRemarks nvarchar(2000) NULL,
            RowVersion rowversion NOT NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionSessions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_InspectionSessions PRIMARY KEY (Id),
            CONSTRAINT FK_InspectionSessions_Brands_BrandId FOREIGN KEY (BrandId) REFERENCES dbo.Brands(Id),
            CONSTRAINT FK_InspectionSessions_LaptopModels_BrandId_ModelId FOREIGN KEY (BrandId, ModelId) REFERENCES dbo.LaptopModels(BrandId, Id),
            CONSTRAINT FK_InspectionSessions_ApplicationUsers_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES dbo.ApplicationUsers(Id),
            CONSTRAINT FK_InspectionSessions_LabelProfiles_LabelProfileCOId FOREIGN KEY (LabelProfileCOId) REFERENCES dbo.LabelProfiles(Id),
            CONSTRAINT FK_InspectionSessions_LabelProfiles_LabelProfileConfigId FOREIGN KEY (LabelProfileConfigId) REFERENCES dbo.LabelProfiles(Id)
        );
        CREATE UNIQUE INDEX IX_InspectionSessions_InspectionNumber ON dbo.InspectionSessions(InspectionNumber);
        CREATE INDEX IX_InspectionSessions_BrandId_ModelId ON dbo.InspectionSessions(BrandId, ModelId);
        CREATE INDEX IX_InspectionSessions_CreatedAtUtc ON dbo.InspectionSessions(CreatedAtUtc);
        CREATE INDEX IX_InspectionSessions_Status_CreatedAtUtc ON dbo.InspectionSessions(Status, CreatedAtUtc);
    END;

    IF OBJECT_ID(N'dbo.InspectionImages', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InspectionImages
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_InspectionImages_Id DEFAULT NEWSEQUENTIALID(),
            InspectionSessionId uniqueidentifier NOT NULL,
            ImageType nvarchar(30) NOT NULL,
            LabelType nvarchar(20) NOT NULL,
            Role nvarchar(20) NOT NULL,
            OriginalFileName nvarchar(255) NOT NULL,
            StoredFileName nvarchar(100) NOT NULL,
            StoragePath nvarchar(1024) NOT NULL,
            Sha256Hash nvarchar(64) NOT NULL,
            ContentType nvarchar(100) NOT NULL,
            SizeBytes bigint NOT NULL,
            Width int NOT NULL,
            Height int NOT NULL,
            UploadedByUserId uniqueidentifier NOT NULL,
            UploadedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionImages_UploadedAtUtc DEFAULT SYSUTCDATETIME(),
            ImageQualityStatus nvarchar(30) NOT NULL CONSTRAINT DF_InspectionImages_ImageQualityStatus DEFAULT (N'PENDING'),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionImages_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_InspectionImages PRIMARY KEY (Id),
            CONSTRAINT CK_InspectionImages_SizeBytes CHECK (SizeBytes > 0),
            CONSTRAINT CK_InspectionImages_ImageType CHECK (ImageType IN (N'MASTER_CO', N'ACTUAL_CO', N'MASTER_CONFIG', N'ACTUAL_CONFIG')),
            CONSTRAINT FK_InspectionImages_InspectionSessions_InspectionSessionId FOREIGN KEY (InspectionSessionId) REFERENCES dbo.InspectionSessions(Id),
            CONSTRAINT FK_InspectionImages_ApplicationUsers_UploadedByUserId FOREIGN KEY (UploadedByUserId) REFERENCES dbo.ApplicationUsers(Id)
        );
        CREATE UNIQUE INDEX IX_InspectionImages_InspectionSessionId_ImageType ON dbo.InspectionImages(InspectionSessionId, ImageType);
        CREATE INDEX IX_InspectionImages_InspectionSessionId ON dbo.InspectionImages(InspectionSessionId);
    END;

    IF OBJECT_ID(N'dbo.OCRResults', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.OCRResults
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_OCRResults_Id DEFAULT NEWSEQUENTIALID(),
            InspectionImageId uniqueidentifier NOT NULL,
            InspectionSessionId uniqueidentifier NOT NULL,
            Engine nvarchar(100) NOT NULL,
            EngineVersion nvarchar(100) NOT NULL,
            Status nvarchar(32) NOT NULL CONSTRAINT DF_OCRResults_Status DEFAULT (N'PROCESSING'),
            RawExtractedText nvarchar(max) NULL,
            ProcessedImagePath nvarchar(1024) NULL,
            ProcessingDurationMs bigint NULL,
            OverallConfidence float NULL,
            ExtractedFieldsJson nvarchar(max) NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_OCRResults_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_OCRResults PRIMARY KEY (Id),
            CONSTRAINT FK_OCRResults_InspectionImages_InspectionImageId FOREIGN KEY (InspectionImageId) REFERENCES dbo.InspectionImages(Id),
            CONSTRAINT FK_OCRResults_InspectionSessions_InspectionSessionId FOREIGN KEY (InspectionSessionId) REFERENCES dbo.InspectionSessions(Id)
        );
        CREATE INDEX IX_OCRResults_InspectionSessionId ON dbo.OCRResults(InspectionSessionId);
    END;

    IF OBJECT_ID(N'dbo.ExtractedFields', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ExtractedFields
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_ExtractedFields_Id DEFAULT NEWSEQUENTIALID(),
            OcrResultId uniqueidentifier NOT NULL,
            FieldDefinitionId uniqueidentifier NULL,
            FieldKey nvarchar(100) NOT NULL,
            ExtractedValue nvarchar(2000) NULL,
            NormalizedValue nvarchar(2000) NULL,
            Confidence float NOT NULL,
            BoundingBoxJson nvarchar(2000) NULL,
            IsManuallyCorrected bit NOT NULL CONSTRAINT DF_ExtractedFields_IsManuallyCorrected DEFAULT (0),
            CorrectedByUserId uniqueidentifier NULL,
            CorrectionReason nvarchar(1000) NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_ExtractedFields_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_ExtractedFields PRIMARY KEY (Id),
            CONSTRAINT CK_ExtractedFields_Confidence CHECK (Confidence >= 0 AND Confidence <= 1),
            CONSTRAINT FK_ExtractedFields_OCRResults_OcrResultId FOREIGN KEY (OcrResultId) REFERENCES dbo.OCRResults(Id),
            CONSTRAINT FK_ExtractedFields_FieldDefinitions_FieldDefinitionId FOREIGN KEY (FieldDefinitionId) REFERENCES dbo.FieldDefinitions(Id),
            CONSTRAINT FK_ExtractedFields_ApplicationUsers_CorrectedByUserId FOREIGN KEY (CorrectedByUserId) REFERENCES dbo.ApplicationUsers(Id)
        );
        CREATE INDEX IX_ExtractedFields_OcrResultId_FieldKey ON dbo.ExtractedFields(OcrResultId, FieldKey);
    END;

    ALTER TABLE dbo.OCRResults ALTER COLUMN OverallConfidence float NULL;
    IF OBJECT_ID(N'dbo.CK_ExtractedFields_Confidence', N'C') IS NOT NULL
        ALTER TABLE dbo.ExtractedFields DROP CONSTRAINT CK_ExtractedFields_Confidence;
    ALTER TABLE dbo.ExtractedFields ALTER COLUMN Confidence float NOT NULL;
    ALTER TABLE dbo.ExtractedFields
        ADD CONSTRAINT CK_ExtractedFields_Confidence CHECK (Confidence >= 0 AND Confidence <= 1);

    IF OBJECT_ID(N'dbo.StageResults', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StageResults
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_StageResults_Id DEFAULT NEWSEQUENTIALID(),
            InspectionSessionId uniqueidentifier NOT NULL,
            StageType nvarchar(32) NOT NULL,
            LabelType nvarchar(20) NOT NULL,
            Status nvarchar(32) NOT NULL CONSTRAINT DF_StageResults_Status DEFAULT (N'PROCESSING'),
            TotalFields int NOT NULL CONSTRAINT DF_StageResults_TotalFields DEFAULT (0),
            MatchedFields int NOT NULL CONSTRAINT DF_StageResults_MatchedFields DEFAULT (0),
            MismatchedFields int NOT NULL CONSTRAINT DF_StageResults_MismatchedFields DEFAULT (0),
            UnreadableFields int NOT NULL CONSTRAINT DF_StageResults_UnreadableFields DEFAULT (0),
            StartedAtUtc datetimeoffset(7) NULL,
            CompletedAtUtc datetimeoffset(7) NULL,
            Remarks nvarchar(2000) NULL,
            EvaluatedAtUtc datetimeoffset(7) NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_StageResults_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_StageResults PRIMARY KEY (Id),
            CONSTRAINT FK_StageResults_InspectionSessions_InspectionSessionId FOREIGN KEY (InspectionSessionId) REFERENCES dbo.InspectionSessions(Id)
        );
        CREATE INDEX IX_StageResults_InspectionSessionId_StageType ON dbo.StageResults(InspectionSessionId, StageType);
    END;

    IF OBJECT_ID(N'dbo.FieldComparisons', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FieldComparisons
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_FieldComparisons_Id DEFAULT NEWSEQUENTIALID(),
            StageResultId uniqueidentifier NOT NULL,
            FieldDefinitionId uniqueidentifier NULL,
            FieldKey nvarchar(100) NOT NULL,
            MasterValue nvarchar(2000) NULL,
            ActualValue nvarchar(2000) NULL,
            NormalizedMasterValue nvarchar(2000) NULL,
            NormalizedActualValue nvarchar(2000) NULL,
            ComparisonStrategy nvarchar(30) NOT NULL,
            MasterConfidence float NULL,
            ActualConfidence float NULL,
            MismatchReason nvarchar(1000) NULL,
            Status nvarchar(32) NOT NULL CONSTRAINT DF_FieldComparisons_Status DEFAULT (N'PROCESSING'),
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_FieldComparisons_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_FieldComparisons PRIMARY KEY (Id),
            CONSTRAINT CK_FieldComparisons_MasterConfidence CHECK (MasterConfidence IS NULL OR (MasterConfidence >= 0 AND MasterConfidence <= 1)),
            CONSTRAINT CK_FieldComparisons_ActualConfidence CHECK (ActualConfidence IS NULL OR (ActualConfidence >= 0 AND ActualConfidence <= 1)),
            CONSTRAINT FK_FieldComparisons_StageResults_StageResultId FOREIGN KEY (StageResultId) REFERENCES dbo.StageResults(Id),
            CONSTRAINT FK_FieldComparisons_FieldDefinitions_FieldDefinitionId FOREIGN KEY (FieldDefinitionId) REFERENCES dbo.FieldDefinitions(Id)
        );
        CREATE UNIQUE INDEX IX_FieldComparisons_StageResultId_FieldKey ON dbo.FieldComparisons(StageResultId, FieldKey);
    END;

    IF OBJECT_ID(N'dbo.InspectionLogs', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InspectionLogs
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_InspectionLogs_Id DEFAULT NEWSEQUENTIALID(),
            InspectionSessionId uniqueidentifier NOT NULL,
            UserAccountId uniqueidentifier NULL,
            Action nvarchar(100) NOT NULL,
            Description nvarchar(2000) NOT NULL,
            PreviousValue nvarchar(4000) NULL,
            NewValue nvarchar(4000) NULL,
            IPAddress nvarchar(45) NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionLogs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_InspectionLogs PRIMARY KEY (Id),
            CONSTRAINT FK_InspectionLogs_InspectionSessions_InspectionSessionId FOREIGN KEY (InspectionSessionId) REFERENCES dbo.InspectionSessions(Id),
            CONSTRAINT FK_InspectionLogs_ApplicationUsers_UserAccountId FOREIGN KEY (UserAccountId) REFERENCES dbo.ApplicationUsers(Id)
        );
        CREATE INDEX IX_InspectionLogs_InspectionSessionId_CreatedAtUtc ON dbo.InspectionLogs(InspectionSessionId, CreatedAtUtc);
    END;

    IF OBJECT_ID(N'dbo.InspectionReports', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InspectionReports
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_InspectionReports_Id DEFAULT NEWSEQUENTIALID(),
            InspectionSessionId uniqueidentifier NOT NULL,
            ReportNumber nvarchar(50) NOT NULL,
            ReportFilePath nvarchar(1024) NOT NULL,
            FileSizeBytes bigint NOT NULL,
            Sha256Hash nvarchar(64) NOT NULL,
            ContentType nvarchar(100) NOT NULL CONSTRAINT DF_InspectionReports_ContentType DEFAULT (N'application/pdf'),
            GeneratedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionReports_GeneratedAtUtc DEFAULT SYSUTCDATETIME(),
            GeneratedByUserId uniqueidentifier NOT NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionReports_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_InspectionReports PRIMARY KEY (Id),
            CONSTRAINT FK_InspectionReports_InspectionSessions_InspectionSessionId FOREIGN KEY (InspectionSessionId) REFERENCES dbo.InspectionSessions(Id),
            CONSTRAINT FK_InspectionReports_ApplicationUsers_GeneratedByUserId FOREIGN KEY (GeneratedByUserId) REFERENCES dbo.ApplicationUsers(Id)
        );
        CREATE UNIQUE INDEX IX_InspectionReports_ReportNumber ON dbo.InspectionReports(ReportNumber);
        CREATE INDEX IX_InspectionReports_InspectionSessionId ON dbo.InspectionReports(InspectionSessionId);
    END;

    IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SystemSettings
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_SystemSettings_Id DEFAULT NEWSEQUENTIALID(),
            SettingKey nvarchar(150) NOT NULL,
            SettingValue nvarchar(2000) NULL,
            Description nvarchar(500) NULL,
            UpdatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_SystemSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_SystemSettings PRIMARY KEY (Id)
        );
        CREATE UNIQUE INDEX IX_SystemSettings_SettingKey ON dbo.SystemSettings(SettingKey);
    END;

    IF OBJECT_ID(N'dbo.InspectionProcessingJobs', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InspectionProcessingJobs
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_InspectionProcessingJobs_Id DEFAULT NEWSEQUENTIALID(),
            InspectionSessionId uniqueidentifier NOT NULL,
            Status nvarchar(30) NOT NULL CONSTRAINT DF_InspectionProcessingJobs_Status DEFAULT (N'PENDING'),
            AttemptCount int NOT NULL CONSTRAINT DF_InspectionProcessingJobs_AttemptCount DEFAULT (0),
            LastError nvarchar(2000) NULL,
            StartedAtUtc datetimeoffset(7) NULL,
            CompletedAtUtc datetimeoffset(7) NULL,
            CreatedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_InspectionProcessingJobs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedAtUtc datetimeoffset(7) NULL,
            CONSTRAINT PK_InspectionProcessingJobs PRIMARY KEY (Id),
            CONSTRAINT FK_InspectionProcessingJobs_InspectionSessions_InspectionSessionId FOREIGN KEY (InspectionSessionId) REFERENCES dbo.InspectionSessions(Id)
        );
        CREATE UNIQUE INDEX IX_InspectionProcessingJobs_InspectionSessionId ON dbo.InspectionProcessingJobs(InspectionSessionId);
        CREATE INDEX IX_InspectionProcessingJobs_Status_CreatedAtUtc ON dbo.InspectionProcessingJobs(Status, CreatedAtUtc);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE Code = N'Admin')
        INSERT dbo.UserRoles(Code, Name) VALUES (N'Admin', N'Administrator');
    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE Code = N'Operator')
        INSERT dbo.UserRoles(Code, Name) VALUES (N'Operator', N'Operator');
    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE Code = N'Viewer')
        INSERT dbo.UserRoles(Code, Name) VALUES (N'Viewer', N'Viewer');

    IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Code = N'HP')
        INSERT dbo.Brands(Id, Code, Name, IsActive, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D01', N'HP', N'HP', 1, '2025-01-01T00:00:00+00:00');
    IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Code = N'ASUS')
        INSERT dbo.Brands(Id, Code, Name, IsActive, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D02', N'ASUS', N'ASUS', 1, '2025-01-01T00:00:00+00:00');
    IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Code = N'ACER')
        INSERT dbo.Brands(Id, Code, Name, IsActive, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D03', N'ACER', N'Acer', 1, '2025-01-01T00:00:00+00:00');
    IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Code = N'GIGABYTE')
        INSERT dbo.Brands(Id, Code, Name, IsActive, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D04', N'GIGABYTE', N'Gigabyte', 1, '2025-01-01T00:00:00+00:00');

    IF NOT EXISTS
       (SELECT 1 FROM dbo.LaptopModels
        WHERE Id = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
           OR (BrandId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D01' AND ModelNumber = N'15-fd1652TU'))
       AND EXISTS (SELECT 1 FROM dbo.Brands WHERE Id = '73A3F07D-C906-4E49-A3DB-4854AB1A9D01')
        INSERT dbo.LaptopModels(Id, BrandId, ModelNumber, ModelName, IsActive, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D05', '73A3F07D-C906-4E49-A3DB-4854AB1A9D01',
            N'15-fd1652TU', N'15-fd1652TU', 1, '2025-01-01T00:00:00+00:00');

    IF NOT EXISTS
       (SELECT 1 FROM dbo.LabelProfiles
        WHERE Id = '73A3F07D-C906-4E49-A3DB-4854AB1A9D06'
           OR (BrandId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D01'
               AND ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
               AND LabelType = N'CO' AND ProfileName = N'HP-15-FD1652TU-CO' AND ProfileVersion = 1))
       AND EXISTS (SELECT 1 FROM dbo.LaptopModels WHERE Id = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05')
        INSERT dbo.LabelProfiles(Id, BrandId, ModelId, LabelType, ProfileName, Name, ProfileVersion, IsActive, EffectiveFromUtc, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D06', '73A3F07D-C906-4E49-A3DB-4854AB1A9D01',
            '73A3F07D-C906-4E49-A3DB-4854AB1A9D05', N'CO', N'HP-15-FD1652TU-CO', N'HP 15-fd1652TU CO',
            1, 1, '2025-01-01T00:00:00+00:00', '2025-01-01T00:00:00+00:00');
    IF NOT EXISTS
       (SELECT 1 FROM dbo.LabelProfiles
        WHERE Id = '73A3F07D-C906-4E49-A3DB-4854AB1A9D07'
           OR (BrandId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D01'
               AND ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
               AND LabelType = N'CONFIG' AND ProfileName = N'HP-15-FD1652TU-CONFIG' AND ProfileVersion = 1))
       AND EXISTS (SELECT 1 FROM dbo.LaptopModels WHERE Id = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05')
        INSERT dbo.LabelProfiles(Id, BrandId, ModelId, LabelType, ProfileName, Name, ProfileVersion, IsActive, EffectiveFromUtc, CreatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D07', '73A3F07D-C906-4E49-A3DB-4854AB1A9D01',
            '73A3F07D-C906-4E49-A3DB-4854AB1A9D05', N'CONFIG', N'HP-15-FD1652TU-CONFIG', N'HP 15-fd1652TU Config',
            1, 1, '2025-01-01T00:00:00+00:00', '2025-01-01T00:00:00+00:00');

    DECLARE @HpFields TABLE
    (
        FieldKey nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(200) NOT NULL,
        DisplayOrder int NOT NULL
    );
    INSERT @HpFields(FieldKey, DisplayName, DisplayOrder)
    VALUES
        (N'MODEL', N'Model', 1),
        (N'PROCESSOR', N'Processor', 2),
        (N'STORAGE', N'Storage', 3),
        (N'RAM', N'RAM', 4),
        (N'OPERATING_SYSTEM', N'Operating System', 5),
        (N'DISPLAY', N'Display', 6),
        (N'BATTERY', N'Battery', 7);

    INSERT dbo.FieldDefinitions
        (Id, LabelProfileId, FieldKey, DisplayName, DataType, IsRequired, IsCritical,
         OCRConfidenceThreshold, DisplayOrder, IsActive, ComparisonStrategy,
         NumericAbsoluteTolerance, NumericRelativeTolerance, CreatedAtUtc)
    SELECT NEWID(), profile.Id, field.FieldKey, field.DisplayName, N'Text', 1, 1,
           CASE WHEN field.FieldKey = N'BATTERY' THEN 0.50 ELSE 0.55 END,
           field.DisplayOrder, 1, N'NormalizedText', 0, 0, SYSUTCDATETIME()
    FROM dbo.LabelProfiles AS profile
    CROSS JOIN @HpFields AS field
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CO'
      AND NOT EXISTS
        (SELECT 1 FROM dbo.FieldDefinitions AS existing
         WHERE existing.LabelProfileId = profile.Id AND existing.FieldKey = field.FieldKey);

    UPDATE modelField
       SET modelField.IsActive = 0
    FROM dbo.FieldDefinitions AS modelField
    JOIN dbo.LabelProfiles AS profile ON profile.Id = modelField.LabelProfileId
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND modelField.FieldKey = N'MODEL'
      AND EXISTS
        (SELECT 1 FROM dbo.FieldDefinitions AS existing
         WHERE existing.LabelProfileId = profile.Id AND existing.FieldKey = N'MODEL_NO');

    UPDATE modelField
       SET modelField.FieldKey = N'MODEL_NO',
           modelField.DisplayName = N'Model No.',
           modelField.DisplayOrder = 1,
           modelField.IsActive = 1
    FROM dbo.FieldDefinitions AS modelField
    JOIN dbo.LabelProfiles AS profile ON profile.Id = modelField.LabelProfileId
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND modelField.FieldKey = N'MODEL'
      AND NOT EXISTS
        (SELECT 1 FROM dbo.FieldDefinitions AS existing
         WHERE existing.LabelProfileId = profile.Id AND existing.FieldKey = N'MODEL_NO');

    INSERT dbo.FieldDefinitions
        (Id, LabelProfileId, FieldKey, DisplayName, DataType, IsRequired, IsCritical,
         OCRConfidenceThreshold, DisplayOrder, IsActive, ComparisonStrategy,
         NumericAbsoluteTolerance, NumericRelativeTolerance, CreatedAtUtc)
    SELECT NEWID(), profile.Id, N'MODEL_NO', N'Model No.', N'Text', 1, 1,
           0.55, 1, 1, N'CaseInsensitive', 0, 0, SYSUTCDATETIME()
    FROM dbo.LabelProfiles AS profile
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND NOT EXISTS
        (SELECT 1 FROM dbo.FieldDefinitions AS existing
         WHERE existing.LabelProfileId = profile.Id AND existing.FieldKey = N'MODEL_NO');

    UPDATE modelField
       SET modelField.DisplayName = N'Model No.',
           modelField.DisplayOrder = 1,
           modelField.IsRequired = 1,
           modelField.IsCritical = 1,
           modelField.IsActive = 1,
           modelField.ComparisonStrategy = N'CaseInsensitive'
    FROM dbo.FieldDefinitions AS modelField
    JOIN dbo.LabelProfiles AS profile ON profile.Id = modelField.LabelProfileId
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND modelField.FieldKey = N'MODEL_NO';

    UPDATE definition
       SET definition.IsActive = 0
    FROM dbo.FieldDefinitions AS definition
    JOIN dbo.LabelProfiles AS profile ON profile.Id = definition.LabelProfileId
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND definition.FieldKey NOT IN (N'MODEL_NO', N'QUEUE_NO');

    INSERT dbo.FieldDefinitions
        (Id, LabelProfileId, FieldKey, DisplayName, DataType, IsRequired, IsCritical,
         OCRConfidenceThreshold, DisplayOrder, IsActive, ComparisonStrategy,
         NumericAbsoluteTolerance, NumericRelativeTolerance, CreatedAtUtc)
    SELECT NEWID(), profile.Id, N'QUEUE_NO', N'Queue No.', N'Text', 1, 1,
           0.55, 2, 1, N'CaseInsensitive', 0, 0, SYSUTCDATETIME()
    FROM dbo.LabelProfiles AS profile
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND NOT EXISTS
        (SELECT 1 FROM dbo.FieldDefinitions AS existing
         WHERE existing.LabelProfileId = profile.Id AND existing.FieldKey = N'QUEUE_NO');

    UPDATE queueField
       SET queueField.DisplayName = N'Queue No.',
           queueField.DisplayOrder = 2,
           queueField.IsRequired = 1,
           queueField.IsCritical = 1,
           queueField.OCRConfidenceThreshold = 0.55,
           queueField.IsActive = 1,
           queueField.ComparisonStrategy = N'CaseInsensitive'
    FROM dbo.FieldDefinitions AS queueField
    JOIN dbo.LabelProfiles AS profile ON profile.Id = queueField.LabelProfileId
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND queueField.FieldKey = N'QUEUE_NO';

    DECLARE @HpAliases TABLE
    (
        FieldKey nvarchar(100) NOT NULL,
        AliasText nvarchar(200) NOT NULL,
        PRIMARY KEY (FieldKey, AliasText)
    );
    INSERT @HpAliases(FieldKey, AliasText)
    VALUES
        (N'MODEL', N'Model Number'),
        (N'MODEL', N'Product Model'),
        (N'PROCESSOR', N'CPU'),
        (N'STORAGE', N'SSD'),
        (N'RAM', N'Memory'),
        (N'OPERATING_SYSTEM', N'OS'),
        (N'DISPLAY', N'Screen'),
        (N'BATTERY', N'Battery Capacity');

    INSERT dbo.FieldAliases
        (Id, FieldDefinitionId, AliasText, Language, IsActive, CreatedAtUtc)
    SELECT NEWID(), definition.Id, alias.AliasText, N'en', 1, SYSUTCDATETIME()
    FROM dbo.FieldDefinitions AS definition
    JOIN dbo.LabelProfiles AS profile ON profile.Id = definition.LabelProfileId
    JOIN @HpAliases AS alias ON alias.FieldKey = definition.FieldKey
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CO'
      AND NOT EXISTS
        (SELECT 1 FROM dbo.FieldAliases AS existing
         WHERE existing.FieldDefinitionId = definition.Id
           AND existing.AliasText = alias.AliasText AND existing.Language = N'en');

    INSERT dbo.FieldAliases
        (Id, FieldDefinitionId, AliasText, Language, IsActive, CreatedAtUtc)
    SELECT NEWID(), definition.Id, alias.AliasText, N'en', 1, SYSUTCDATETIME()
    FROM dbo.FieldDefinitions AS definition
    JOIN dbo.LabelProfiles AS profile ON profile.Id = definition.LabelProfileId
    JOIN (VALUES
        (N'MODEL_NO', N'Model Number'),
        (N'MODEL_NO', N'Product Model'),
        (N'QUEUE_NO', N'Queue Number')
    ) AS alias(FieldKey, AliasText) ON alias.FieldKey = definition.FieldKey
    WHERE profile.ModelId = '73A3F07D-C906-4E49-A3DB-4854AB1A9D05'
      AND profile.ProfileVersion = 1
      AND profile.ProfileName = N'HP-15-FD1652TU-CONFIG'
      AND NOT EXISTS
        (SELECT 1 FROM dbo.FieldAliases AS existing
         WHERE existing.FieldDefinitionId = definition.Id
           AND existing.AliasText = alias.AliasText AND existing.Language = N'en');

    IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = N'OcrMinimumConfidence')
        INSERT dbo.SystemSettings(Id, SettingKey, SettingValue, Description, UpdatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D21', N'OcrMinimumConfidence', N'0.85',
            N'Minimum OCR confidence required before configured field values can be compared.', '2025-01-01T00:00:00+00:00');
    IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = N'InspectionStatuses')
        INSERT dbo.SystemSettings(Id, SettingKey, SettingValue, Description, UpdatedAtUtc)
        VALUES ('73A3F07D-C906-4E49-A3DB-4854AB1A9D22', N'InspectionStatuses',
            N'["READY","PROCESSING","PASS","FAIL","REVIEW_REQUIRED","BLOCKED","CANCELLED","ERROR","NOT_APPLICABLE"]',
            N'Status values supported by inspection and stage results.', '2025-01-01T00:00:00+00:00');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
