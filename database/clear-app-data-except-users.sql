-- Deletes application data while preserving ApplicationUsers and UserRoles.
-- Set @ConfirmDatabase to the exact database name before running.
SET NOCOUNT ON;

DECLARE @ExpectedDatabase sysname = N'DixonLabelVerificationDB';
DECLARE @ConfirmDatabase sysname = N'REPLACE_WITH_DATABASE_NAME';

IF DB_NAME() <> @ExpectedDatabase
    THROW 50000, 'Cleanup stopped: connected to an unexpected database.', 1;

IF @ConfirmDatabase <> @ExpectedDatabase
    THROW 50001, 'Cleanup stopped: set @ConfirmDatabase to the expected database name.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE FROM dbo.FieldComparisons;
    DELETE FROM dbo.ExtractedFields;
    DELETE FROM dbo.MasterLabelFields;
    DELETE FROM dbo.InspectionProcessingJobs;
    DELETE FROM dbo.InspectionReports;
    DELETE FROM dbo.InspectionLogs;
    DELETE FROM dbo.StageResults;
    DELETE FROM dbo.OCRResults;
    DELETE FROM dbo.InspectionImages;
    DELETE FROM dbo.InspectionSessions;
    DELETE FROM dbo.MasterLabels;
    DELETE FROM dbo.FieldAliases;
    DELETE FROM dbo.FieldDefinitions;
    DELETE FROM dbo.LabelProfiles;
    DELETE FROM dbo.LaptopModels;
    DELETE FROM dbo.Brands;
    DELETE FROM dbo.SystemSettings;

    COMMIT TRANSACTION;
    PRINT 'Application data cleared. ApplicationUsers and UserRoles were preserved.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
