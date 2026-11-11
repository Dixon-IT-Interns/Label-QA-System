using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteInspectionDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldAliases_FieldDefinitions_FieldDefinitionId",
                table: "FieldAliases");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldComparisons_StageResults_StageResultId",
                table: "FieldComparisons");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldDefinitions_LabelProfiles_LabelProfileId",
                table: "FieldDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionImages_InspectionSessions_InspectionSessionId",
                table: "InspectionImages");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionLogs_InspectionSessions_InspectionSessionId",
                table: "InspectionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionLogs_Users_UserAccountId",
                table: "InspectionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_Models_ModelId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_Users_CreatedByUserId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_LabelProfiles_Models_ModelId",
                table: "LabelProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Models_Brands_BrandId",
                table: "Models");

            migrationBuilder.DropForeignKey(
                name: "FK_OCRResults_InspectionImages_InspectionImageId",
                table: "OCRResults");

            migrationBuilder.DropForeignKey(
                name: "FK_StageResults_InspectionSessions_InspectionSessionId",
                table: "StageResults");

            migrationBuilder.DropIndex(
                name: "IX_StageResults_InspectionSessionId_LabelType",
                table: "StageResults");

            migrationBuilder.DropIndex(
                name: "IX_OCRResults_InspectionImageId_CreatedAtUtc",
                table: "OCRResults");

            migrationBuilder.DropIndex(
                name: "IX_LabelProfiles_ModelId",
                table: "LabelProfiles");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_BrandId",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_ModelId",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_InspectionSessionId_LabelType_Role",
                table: "InspectionImages");

            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_Sha256Hash",
                table: "InspectionImages");

            migrationBuilder.DropIndex(
                name: "IX_FieldAliases_FieldDefinitionId_Value",
                table: "FieldAliases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Models",
                table: "Models");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "ApplicationUsers");

            migrationBuilder.RenameTable(
                name: "Models",
                newName: "LaptopModels");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "LabelProfiles",
                newName: "ProfileVersion");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "LabelProfiles",
                newName: "ProfileName");

            migrationBuilder.RenameIndex(
                name: "IX_LabelProfiles_BrandId_ModelId_LabelType_Code_Version",
                table: "LabelProfiles",
                newName: "IX_LabelProfiles_BrandId_ModelId_LabelType_ProfileName_ProfileVersion");

            migrationBuilder.RenameIndex(
                name: "IX_LabelProfiles_BrandId_LabelType_Code_Version",
                table: "LabelProfiles",
                newName: "IX_LabelProfiles_BrandId_LabelType_ProfileName_ProfileVersion");

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "InspectionLogs",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "Details",
                table: "InspectionLogs",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "MinimumConfidence",
                table: "FieldDefinitions",
                newName: "OCRConfidenceThreshold");

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "FieldDefinitions",
                newName: "FieldKey");

            migrationBuilder.RenameIndex(
                name: "IX_FieldDefinitions_LabelProfileId_Key",
                table: "FieldDefinitions",
                newName: "IX_FieldDefinitions_LabelProfileId_FieldKey");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "FieldAliases",
                newName: "AliasText");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "ApplicationUsers",
                newName: "FullName");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "ApplicationUsers",
                newName: "IX_ApplicationUsers_Email");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "LaptopModels",
                newName: "ModelName");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "LaptopModels",
                newName: "ModelNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Models_BrandId_Code",
                table: "LaptopModels",
                newName: "IX_LaptopModels_BrandId_ModelNumber");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "StageResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchedFields",
                table: "StageResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MismatchedFields",
                table: "StageResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "StageResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageType",
                table: "StageResults",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "StageResults",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalFields",
                table: "StageResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnreadableFields",
                table: "StageResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "InspectionSessionId",
                table: "OCRResults",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<double>(
                name: "OverallConfidence",
                table: "OCRResults",
                type: "float(5)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedImagePath",
                table: "OCRResults",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProcessingDurationMs",
                table: "OCRResults",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawExtractedText",
                table: "OCRResults",
                type: "nvarchar(max)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFromUtc",
                table: "LabelProfiles",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "COStatus",
                table: "InspectionSessions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConfigStatus",
                table: "InspectionSessions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FinalRemarks",
                table: "InspectionSessions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionNumber",
                table: "InspectionSessions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "LabelProfileCOId",
                table: "InspectionSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LabelProfileConfigId",
                table: "InspectionSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionOrderNumber",
                table: "InspectionSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InspectionSessions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SerialStatus",
                table: "InspectionSessions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "InspectionSessions",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderNumber",
                table: "InspectionSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreviousValue",
                table: "InspectionLogs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewValue",
                table: "InspectionLogs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IPAddress",
                table: "InspectionLogs",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "InspectionImages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageQualityStatus",
                table: "InspectionImages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImageType",
                table: "InspectionImages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "InspectionImages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "InspectionImages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UploadedAtUtc",
                table: "InspectionImages",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                table: "InspectionImages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "InspectionImages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "FieldDefinitions",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "FieldDefinitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.AlterColumn<string>(
                name: "MasterValue",
                table: "FieldComparisons",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActualValue",
                table: "FieldComparisons",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonStrategy",
                table: "FieldComparisons",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MismatchReason",
                table: "FieldComparisons",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedActualValue",
                table: "FieldComparisons",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedMasterValue",
                table: "FieldComparisons",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FieldAliases",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "FieldAliases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Brands",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "ApplicationUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAtUtc",
                table: "ApplicationUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "ApplicationUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "LaptopModels",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ApplicationUsers WHERE Role NOT IN (N'Admin', N'Operator', N'Viewer') OR LEN(Role) > 20)
                    THROW 51000, 'Existing user roles must be mapped to Admin, Operator, or Viewer before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1 FROM LabelProfiles
                    WHERE ModelId IS NOT NULL
                    GROUP BY ModelId, LabelType
                    HAVING SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) > 1)
                    THROW 51001, 'Resolve duplicate active label profiles per model and label type before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1 FROM LabelProfiles p
                    LEFT JOIN LaptopModels m ON m.Id = p.ModelId AND m.BrandId = p.BrandId
                    WHERE p.ModelId IS NOT NULL AND m.Id IS NULL)
                    THROW 51002, 'Resolve label profiles whose model and brand do not match before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1 FROM InspectionImages
                    WHERE LabelType NOT IN (N'CO', N'CONFIG') OR Role NOT IN (N'MASTER', N'ACTUAL'))
                    THROW 51003, 'Resolve legacy image label types and roles before applying this migration.', 1;

                IF EXISTS (SELECT 1 FROM StageResults WHERE LabelType NOT IN (N'CO', N'CONFIG'))
                    THROW 51004, 'Resolve legacy stage label types before applying this migration.', 1;

                UPDATE ApplicationUsers
                SET Username = LEFT(COALESCE(NULLIF(LTRIM(RTRIM(Email)), N''), N'legacy'), 60)
                    + N'-' + REPLACE(CONVERT(nvarchar(36), Id), N'-', N'')
                WHERE NULLIF(LTRIM(RTRIM(Username)), N'') IS NULL;

                UPDATE InspectionSessions
                SET InspectionNumber = N'LEGACY-' + REPLACE(CONVERT(nvarchar(36), Id), N'-', N''),
                    StartedAtUtc = CreatedAtUtc
                WHERE NULLIF(LTRIM(RTRIM(InspectionNumber)), N'') IS NULL;

                UPDATE InspectionSessions
                SET StartedAtUtc = CreatedAtUtc
                WHERE StartedAtUtc > CreatedAtUtc;

                UPDATE LabelProfiles
                SET EffectiveFromUtc = CreatedAtUtc
                WHERE EffectiveFromUtc > CreatedAtUtc;

                UPDATE InspectionImages
                SET ImageType = CASE
                        WHEN LabelType = N'CO' AND Role = N'MASTER' THEN N'MASTER_CO'
                        WHEN LabelType = N'CO' AND Role = N'ACTUAL' THEN N'ACTUAL_CO'
                        WHEN LabelType = N'CONFIG' AND Role = N'MASTER' THEN N'MASTER_CONFIG'
                        WHEN LabelType = N'CONFIG' AND Role = N'ACTUAL' THEN N'ACTUAL_CONFIG'
                    END,
                    OriginalFileName = N'LEGACY-' + REPLACE(CONVERT(nvarchar(36), Id), N'-', N'') + N'.unknown',
                    StoredFileName = N'LEGACY-' + REPLACE(CONVERT(nvarchar(36), Id), N'-', N'') + N'.unknown',
                    UploadedByUserId = (
                        SELECT i.CreatedByUserId FROM InspectionSessions i
                        WHERE i.Id = InspectionImages.InspectionSessionId),
                    UploadedAtUtc = CreatedAtUtc,
                    ImageQualityStatus = N'LEGACY_REVIEW_REQUIRED';

                UPDATE StageResults
                SET StageType = CASE
                    WHEN LabelType = N'CO' THEN N'CO_COMPARISON'
                    WHEN LabelType = N'CONFIG' THEN N'CONFIG_COMPARISON'
                    END,
                    StartedAtUtc = CreatedAtUtc,
                    CompletedAtUtc = CASE
                        WHEN Status IN (N'PASS', N'FAIL', N'REVIEW_REQUIRED', N'NOT_APPLICABLE', N'BLOCKED')
                            THEN COALESCE(EvaluatedAtUtc, CreatedAtUtc)
                        ELSE NULL
                    END;

                UPDATE i
                SET COStatus = COALESCE((
                        SELECT TOP (1) s.Status FROM StageResults s
                        WHERE s.InspectionSessionId = i.Id AND s.LabelType = N'CO'
                        ORDER BY COALESCE(s.EvaluatedAtUtc, s.CreatedAtUtc) DESC),
                    CASE WHEN i.Status IN (N'PASS', N'FAIL', N'REVIEW_REQUIRED')
                        THEN N'REVIEW_REQUIRED' ELSE N'READY' END),
                    ConfigStatus = COALESCE((
                        SELECT TOP (1) s.Status FROM StageResults s
                        WHERE s.InspectionSessionId = i.Id AND s.LabelType = N'CONFIG'
                        ORDER BY COALESCE(s.EvaluatedAtUtc, s.CreatedAtUtc) DESC),
                    CASE WHEN i.Status IN (N'PASS', N'FAIL', N'REVIEW_REQUIRED')
                        THEN N'REVIEW_REQUIRED' ELSE N'READY' END),
                    SerialStatus = CASE
                        WHEN i.Status IN (N'PASS', N'FAIL', N'REVIEW_REQUIRED') THEN N'REVIEW_REQUIRED'
                        ELSE N'NOT_APPLICABLE'
                    END,
                    FinalRemarks = CASE
                        WHEN i.LabelProfileCOId IS NULL OR i.LabelProfileConfigId IS NULL
                            THEN COALESCE(i.FinalRemarks, N'Legacy record: exact profile versions were not stored; serial evidence requires review.')
                        ELSE i.FinalRemarks
                    END
                FROM InspectionSessions i;

                UPDATE o
                SET InspectionSessionId = i.InspectionSessionId
                FROM OCRResults o
                INNER JOIN InspectionImages i ON i.Id = o.InspectionImageId;

                UPDATE c
                SET ComparisonStrategy = COALESCE(d.ComparisonStrategy, N'NormalizedText')
                FROM FieldComparisons c
                LEFT JOIN FieldDefinitions d ON d.Id = c.FieldDefinitionId;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicationUsers",
                table: "ApplicationUsers",
                column: "Id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LaptopModels_BrandId_Id",
                table: "LaptopModels",
                columns: new[] { "BrandId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LaptopModels",
                table: "LaptopModels",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ExtractedFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OcrResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExtractedValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NormalizedValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Confidence = table.Column<double>(type: "float(5)", precision: 5, scale: 4, nullable: false),
                    BoundingBoxJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsManuallyCorrected = table.Column<bool>(type: "bit", nullable: false),
                    CorrectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractedFields_ApplicationUsers_CorrectedByUserId",
                        column: x => x.CorrectedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtractedFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtractedFields_OCRResults_OcrResultId",
                        column: x => x.OcrResultId,
                        principalTable: "OCRResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionProcessingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionProcessingJobs_InspectionSessions_InspectionSessionId",
                        column: x => x.InspectionSessionId,
                        principalTable: "InspectionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionReports_ApplicationUsers_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionReports_InspectionSessions_InspectionSessionId",
                        column: x => x.InspectionSessionId,
                        principalTable: "InspectionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettingKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"),
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d02"),
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d03"),
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d04"),
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d06"),
                column: "EffectiveFromUtc",
                value: new DateTimeOffset(new DateTime(2026, 11, 4, 15, 10, 54, 378, DateTimeKind.Unspecified).AddTicks(6034), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d07"),
                column: "EffectiveFromUtc",
                value: new DateTimeOffset(new DateTime(2026, 11, 4, 15, 10, 54, 378, DateTimeKind.Unspecified).AddTicks(7412), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "LaptopModels",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"),
                column: "Description",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_InspectionSessionId_StageType",
                table: "StageResults",
                columns: new[] { "InspectionSessionId", "StageType" });

            migrationBuilder.CreateIndex(
                name: "IX_OCRResults_InspectionImageId",
                table: "OCRResults",
                column: "InspectionImageId");

            migrationBuilder.CreateIndex(
                name: "IX_OCRResults_InspectionSessionId",
                table: "OCRResults",
                column: "InspectionSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelProfiles_ModelId_LabelType",
                table: "LabelProfiles",
                columns: new[] { "ModelId", "LabelType" },
                unique: true,
                filter: "[IsActive] = 1 AND [ModelId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_BrandId_ModelId",
                table: "InspectionSessions",
                columns: new[] { "BrandId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_CreatedAtUtc",
                table: "InspectionSessions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_InspectionNumber",
                table: "InspectionSessions",
                column: "InspectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_LabelProfileCOId",
                table: "InspectionSessions",
                column: "LabelProfileCOId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_LabelProfileConfigId",
                table: "InspectionSessions",
                column: "LabelProfileConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_InspectionSessionId",
                table: "InspectionImages",
                column: "InspectionSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_InspectionSessionId_ImageType",
                table: "InspectionImages",
                columns: new[] { "InspectionSessionId", "ImageType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_UploadedByUserId",
                table: "InspectionImages",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAliases_FieldDefinitionId_AliasText_Language",
                table: "FieldAliases",
                columns: new[] { "FieldDefinitionId", "AliasText", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Username",
                table: "ApplicationUsers",
                column: "Username",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers",
                sql: "[Role] IN ('Admin', 'Operator', 'Viewer')");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedFields_CorrectedByUserId",
                table: "ExtractedFields",
                column: "CorrectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedFields_FieldDefinitionId",
                table: "ExtractedFields",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedFields_OcrResultId_FieldKey",
                table: "ExtractedFields",
                columns: new[] { "OcrResultId", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProcessingJobs_InspectionSessionId",
                table: "InspectionProcessingJobs",
                column: "InspectionSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionProcessingJobs_Status_CreatedAtUtc",
                table: "InspectionProcessingJobs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReports_GeneratedByUserId",
                table: "InspectionReports",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReports_InspectionSessionId",
                table: "InspectionReports",
                column: "InspectionSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReports_ReportNumber",
                table: "InspectionReports",
                column: "ReportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_SettingKey",
                table: "SystemSettings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAliases_FieldDefinitions_FieldDefinitionId",
                table: "FieldAliases",
                column: "FieldDefinitionId",
                principalTable: "FieldDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldComparisons_StageResults_StageResultId",
                table: "FieldComparisons",
                column: "StageResultId",
                principalTable: "StageResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldDefinitions_LabelProfiles_LabelProfileId",
                table: "FieldDefinitions",
                column: "LabelProfileId",
                principalTable: "LabelProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionImages_ApplicationUsers_UploadedByUserId",
                table: "InspectionImages",
                column: "UploadedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionImages_InspectionSessions_InspectionSessionId",
                table: "InspectionImages",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionLogs_ApplicationUsers_UserAccountId",
                table: "InspectionLogs",
                column: "UserAccountId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionLogs_InspectionSessions_InspectionSessionId",
                table: "InspectionLogs",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_ApplicationUsers_CreatedByUserId",
                table: "InspectionSessions",
                column: "CreatedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_LabelProfiles_LabelProfileCOId",
                table: "InspectionSessions",
                column: "LabelProfileCOId",
                principalTable: "LabelProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_LabelProfiles_LabelProfileConfigId",
                table: "InspectionSessions",
                column: "LabelProfileConfigId",
                principalTable: "LabelProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_LaptopModels_BrandId_ModelId",
                table: "InspectionSessions",
                columns: new[] { "BrandId", "ModelId" },
                principalTable: "LaptopModels",
                principalColumns: new[] { "BrandId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabelProfiles_LaptopModels_BrandId_ModelId",
                table: "LabelProfiles",
                columns: new[] { "BrandId", "ModelId" },
                principalTable: "LaptopModels",
                principalColumns: new[] { "BrandId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LaptopModels_Brands_BrandId",
                table: "LaptopModels",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OCRResults_InspectionImages_InspectionImageId",
                table: "OCRResults",
                column: "InspectionImageId",
                principalTable: "InspectionImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OCRResults_InspectionSessions_InspectionSessionId",
                table: "OCRResults",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StageResults_InspectionSessions_InspectionSessionId",
                table: "StageResults",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldAliases_FieldDefinitions_FieldDefinitionId",
                table: "FieldAliases");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldComparisons_StageResults_StageResultId",
                table: "FieldComparisons");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldDefinitions_LabelProfiles_LabelProfileId",
                table: "FieldDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionImages_ApplicationUsers_UploadedByUserId",
                table: "InspectionImages");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionImages_InspectionSessions_InspectionSessionId",
                table: "InspectionImages");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionLogs_ApplicationUsers_UserAccountId",
                table: "InspectionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionLogs_InspectionSessions_InspectionSessionId",
                table: "InspectionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_ApplicationUsers_CreatedByUserId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_LabelProfiles_LabelProfileCOId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_LabelProfiles_LabelProfileConfigId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_LaptopModels_BrandId_ModelId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_LabelProfiles_LaptopModels_BrandId_ModelId",
                table: "LabelProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_LaptopModels_Brands_BrandId",
                table: "LaptopModels");

            migrationBuilder.DropForeignKey(
                name: "FK_OCRResults_InspectionImages_InspectionImageId",
                table: "OCRResults");

            migrationBuilder.DropForeignKey(
                name: "FK_OCRResults_InspectionSessions_InspectionSessionId",
                table: "OCRResults");

            migrationBuilder.DropForeignKey(
                name: "FK_StageResults_InspectionSessions_InspectionSessionId",
                table: "StageResults");

            migrationBuilder.DropTable(
                name: "ExtractedFields");

            migrationBuilder.DropTable(
                name: "InspectionProcessingJobs");

            migrationBuilder.DropTable(
                name: "InspectionReports");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_StageResults_InspectionSessionId_StageType",
                table: "StageResults");

            migrationBuilder.DropIndex(
                name: "IX_OCRResults_InspectionImageId",
                table: "OCRResults");

            migrationBuilder.DropIndex(
                name: "IX_OCRResults_InspectionSessionId",
                table: "OCRResults");

            migrationBuilder.DropIndex(
                name: "IX_LabelProfiles_ModelId_LabelType",
                table: "LabelProfiles");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_BrandId_ModelId",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_CreatedAtUtc",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_InspectionNumber",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_LabelProfileCOId",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionSessions_LabelProfileConfigId",
                table: "InspectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_InspectionSessionId",
                table: "InspectionImages");

            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_InspectionSessionId_ImageType",
                table: "InspectionImages");

            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_UploadedByUserId",
                table: "InspectionImages");

            migrationBuilder.DropIndex(
                name: "IX_FieldAliases_FieldDefinitionId_AliasText_Language",
                table: "FieldAliases");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LaptopModels_BrandId_Id",
                table: "LaptopModels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LaptopModels",
                table: "LaptopModels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicationUsers",
                table: "ApplicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_Username",
                table: "ApplicationUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "MatchedFields",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "MismatchedFields",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "StageType",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "TotalFields",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "UnreadableFields",
                table: "StageResults");

            migrationBuilder.DropColumn(
                name: "InspectionSessionId",
                table: "OCRResults");

            migrationBuilder.DropColumn(
                name: "OverallConfidence",
                table: "OCRResults");

            migrationBuilder.DropColumn(
                name: "ProcessedImagePath",
                table: "OCRResults");

            migrationBuilder.DropColumn(
                name: "ProcessingDurationMs",
                table: "OCRResults");

            migrationBuilder.DropColumn(
                name: "RawExtractedText",
                table: "OCRResults");

            migrationBuilder.DropColumn(
                name: "EffectiveFromUtc",
                table: "LabelProfiles");

            migrationBuilder.DropColumn(
                name: "COStatus",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "ConfigStatus",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "FinalRemarks",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "InspectionNumber",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "LabelProfileCOId",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "LabelProfileConfigId",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "ProductionOrderNumber",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "SerialStatus",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "WorkOrderNumber",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "IPAddress",
                table: "InspectionLogs");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "ImageQualityStatus",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "ImageType",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "UploadedAtUtc",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "DataType",
                table: "FieldDefinitions");

            migrationBuilder.DropColumn(
                name: "ComparisonStrategy",
                table: "FieldComparisons");

            migrationBuilder.DropColumn(
                name: "MismatchReason",
                table: "FieldComparisons");

            migrationBuilder.DropColumn(
                name: "NormalizedActualValue",
                table: "FieldComparisons");

            migrationBuilder.DropColumn(
                name: "NormalizedMasterValue",
                table: "FieldComparisons");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FieldAliases");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "FieldAliases");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "LaptopModels");

            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "ApplicationUsers");

            migrationBuilder.RenameTable(
                name: "LaptopModels",
                newName: "Models");

            migrationBuilder.RenameTable(
                name: "ApplicationUsers",
                newName: "Users");

            migrationBuilder.RenameColumn(
                name: "ProfileVersion",
                table: "LabelProfiles",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "ProfileName",
                table: "LabelProfiles",
                newName: "Code");

            migrationBuilder.RenameIndex(
                name: "IX_LabelProfiles_BrandId_ModelId_LabelType_ProfileName_ProfileVersion",
                table: "LabelProfiles",
                newName: "IX_LabelProfiles_BrandId_ModelId_LabelType_Code_Version");

            migrationBuilder.RenameIndex(
                name: "IX_LabelProfiles_BrandId_LabelType_ProfileName_ProfileVersion",
                table: "LabelProfiles",
                newName: "IX_LabelProfiles_BrandId_LabelType_Code_Version");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "InspectionLogs",
                newName: "Details");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "InspectionLogs",
                newName: "EventType");

            migrationBuilder.RenameColumn(
                name: "OCRConfidenceThreshold",
                table: "FieldDefinitions",
                newName: "MinimumConfidence");

            migrationBuilder.RenameColumn(
                name: "FieldKey",
                table: "FieldDefinitions",
                newName: "Key");

            migrationBuilder.RenameIndex(
                name: "IX_FieldDefinitions_LabelProfileId_FieldKey",
                table: "FieldDefinitions",
                newName: "IX_FieldDefinitions_LabelProfileId_Key");

            migrationBuilder.RenameColumn(
                name: "AliasText",
                table: "FieldAliases",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "ModelNumber",
                table: "Models",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "ModelName",
                table: "Models",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_LaptopModels_BrandId_ModelNumber",
                table: "Models",
                newName: "IX_Models_BrandId_Code");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "Users",
                newName: "DisplayName");

            migrationBuilder.RenameIndex(
                name: "IX_ApplicationUsers_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousValue",
                table: "InspectionLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewValue",
                table: "InspectionLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "FieldDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "MasterValue",
                table: "FieldComparisons",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActualValue",
                table: "FieldComparisons",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Models",
                table: "Models",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_InspectionSessionId_LabelType",
                table: "StageResults",
                columns: new[] { "InspectionSessionId", "LabelType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OCRResults_InspectionImageId_CreatedAtUtc",
                table: "OCRResults",
                columns: new[] { "InspectionImageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabelProfiles_ModelId",
                table: "LabelProfiles",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_BrandId",
                table: "InspectionSessions",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_ModelId",
                table: "InspectionSessions",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_InspectionSessionId_LabelType_Role",
                table: "InspectionImages",
                columns: new[] { "InspectionSessionId", "LabelType", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_Sha256Hash",
                table: "InspectionImages",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_FieldAliases_FieldDefinitionId_Value",
                table: "FieldAliases",
                columns: new[] { "FieldDefinitionId", "Value" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldAliases_FieldDefinitions_FieldDefinitionId",
                table: "FieldAliases",
                column: "FieldDefinitionId",
                principalTable: "FieldDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldComparisons_StageResults_StageResultId",
                table: "FieldComparisons",
                column: "StageResultId",
                principalTable: "StageResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldDefinitions_LabelProfiles_LabelProfileId",
                table: "FieldDefinitions",
                column: "LabelProfileId",
                principalTable: "LabelProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionImages_InspectionSessions_InspectionSessionId",
                table: "InspectionImages",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionLogs_InspectionSessions_InspectionSessionId",
                table: "InspectionLogs",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionLogs_Users_UserAccountId",
                table: "InspectionLogs",
                column: "UserAccountId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_Models_ModelId",
                table: "InspectionSessions",
                column: "ModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_Users_CreatedByUserId",
                table: "InspectionSessions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabelProfiles_Models_ModelId",
                table: "LabelProfiles",
                column: "ModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Models_Brands_BrandId",
                table: "Models",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OCRResults_InspectionImages_InspectionImageId",
                table: "OCRResults",
                column: "InspectionImageId",
                principalTable: "InspectionImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StageResults_InspectionSessions_InspectionSessionId",
                table: "StageResults",
                column: "InspectionSessionId",
                principalTable: "InspectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
