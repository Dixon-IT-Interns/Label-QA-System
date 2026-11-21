using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FieldDefinitions_MinimumConfidence",
                table: "FieldDefinitions");

            migrationBuilder.AddColumn<long>(
                name: "ProcessingDurationMs",
                table: "InspectionSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "InspectionReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "InspectionReports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Sha256Hash",
                table: "InspectionReports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetExpiresAtUtc",
                table: "ApplicationUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "ApplicationUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Code);
                });

            migrationBuilder.UpdateData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d06"),
                columns: new[] { "EffectiveFromUtc", "IsActive" },
                values: new object[] { new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false });

            migrationBuilder.UpdateData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d07"),
                columns: new[] { "EffectiveFromUtc", "IsActive" },
                values: new object[] { new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false });

            migrationBuilder.InsertData(
                table: "LabelProfiles",
                columns: new[] { "Id", "BrandId", "ProfileName", "CreatedAtUtc", "EffectiveFromUtc", "IsActive", "LabelType", "ModelId", "Name", "UpdatedAtUtc", "ProfileVersion" },
                values: new object[,]
                {
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"), "HP-15-FD1652TU-CO", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "CO", new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"), "HP 15-fd1652TU CO", null, 2 },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d09"), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"), "HP-15-FD1652TU-CONFIG", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "CONFIG", new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"), "HP 15-fd1652TU Config", null, 2 }
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "Description", "SettingKey", "SettingValue", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"), "Minimum OCR confidence required before configured field values can be compared.", "OcrMinimumConfidence", "0.85", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d22"), "Status values supported by inspection and stage results.", "InspectionStatuses", "[\"READY\",\"PROCESSING\",\"PASS\",\"FAIL\",\"REVIEW_REQUIRED\",\"BLOCKED\",\"CANCELLED\",\"ERROR\",\"NOT_APPLICABLE\"]", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Code", "Name" },
                values: new object[,]
                {
                    { "Admin", "Administrator" },
                    { "Operator", "Operator" },
                    { "Viewer", "Viewer" }
                });

            migrationBuilder.InsertData(
                table: "FieldDefinitions",
                columns: new[] { "Id", "ComparisonStrategy", "CreatedAtUtc", "IsCritical", "DataType", "DisplayName", "DisplayOrder", "FieldKey", "IsActive", "LabelProfileId", "NumericAbsoluteTolerance", "NumericRelativeTolerance", "OCRConfidenceThreshold", "IsRequired", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d10"), "Identifier", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Model", 1, "MODEL", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d11"), "NormalizedText", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Processor", 2, "PROCESSOR", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d12"), "NormalizedText", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Storage", 3, "STORAGE", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d13"), "NormalizedText", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Memory", 4, "MEMORY", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d14"), "NormalizedText", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Display", 5, "DISPLAY", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d15"), "NormalizedText", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Battery", 6, "BATTERY", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d20"), "Identifier", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Part Number", 1, "PART_NUMBER", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d09"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"), "Identifier", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Production Order", 2, "PRODUCTION_ORDER", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d09"), 0m, 0m, 0.5, true, null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d22"), "Identifier", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Text", "Work Order", 3, "WORK_ORDER", true, new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d09"), 0m, 0m, 0.5, true, null }
                });

            migrationBuilder.InsertData(
                table: "FieldAliases",
                columns: new[] { "Id", "CreatedAtUtc", "FieldDefinitionId", "IsActive", "Language", "UpdatedAtUtc", "AliasText" },
                values: new object[,]
                {
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d30"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d10"), true, "en", null, "Product Model" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d31"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d10"), true, "en", null, "Model Number" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d32"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d11"), true, "en", null, "CPU" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d33"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d12"), true, "en", null, "SSD" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d34"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d13"), true, "en", null, "RAM" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d35"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d14"), true, "en", null, "Screen" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d36"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d15"), true, "en", null, "Battery Capacity" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d37"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d20"), true, "en", null, "P/N" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d38"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d20"), true, "en", null, "Product Number" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d39"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"), true, "en", null, "PRDN Order" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d3a"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"), true, "en", null, "PRON Order" },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d3b"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"), true, "en", null, "Production Order" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FieldDefinitions_MinimumConfidence",
                table: "FieldDefinitions",
                sql: "[OCRConfidenceThreshold] >= 0 AND [OCRConfidenceThreshold] <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Role",
                table: "ApplicationUsers",
                column: "Role");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationUsers_UserRoles_Role",
                table: "ApplicationUsers",
                column: "Role",
                principalTable: "UserRoles",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationUsers_UserRoles_Role",
                table: "ApplicationUsers");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FieldDefinitions_MinimumConfidence",
                table: "FieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_Role",
                table: "ApplicationUsers");

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d30"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d31"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d32"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d33"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d34"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d35"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d36"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d37"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d38"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d39"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d3a"));

            migrationBuilder.DeleteData(
                table: "FieldAliases",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d3b"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d22"));

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"));

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d22"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d10"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d11"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d12"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d13"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d14"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d15"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d20"));

            migrationBuilder.DeleteData(
                table: "FieldDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d21"));

            migrationBuilder.DeleteData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d08"));

            migrationBuilder.DeleteData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d09"));

            migrationBuilder.DropColumn(
                name: "ProcessingDurationMs",
                table: "InspectionSessions");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "InspectionReports");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "InspectionReports");

            migrationBuilder.DropColumn(
                name: "Sha256Hash",
                table: "InspectionReports");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpiresAtUtc",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenHash",
                table: "ApplicationUsers");

            migrationBuilder.UpdateData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d06"),
                columns: new[] { "EffectiveFromUtc", "IsActive" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 11, 4, 15, 10, 54, 378, DateTimeKind.Unspecified).AddTicks(6034), new TimeSpan(0, 0, 0, 0, 0)), true });

            migrationBuilder.UpdateData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d07"),
                columns: new[] { "EffectiveFromUtc", "IsActive" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 11, 4, 15, 10, 54, 378, DateTimeKind.Unspecified).AddTicks(7412), new TimeSpan(0, 0, 0, 0, 0)), true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FieldDefinitions_MinimumConfidence",
                table: "FieldDefinitions",
                sql: "[MinimumConfidence] >= 0 AND [MinimumConfidence] <= 1");
        }
    }
}
