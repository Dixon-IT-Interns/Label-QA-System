using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiBrandConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Models_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionSessions_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionSessions_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionSessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabelProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LabelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelProfiles_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabelProfiles_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionImages", x => x.Id);
                    table.CheckConstraint("CK_InspectionImages_SizeBytes", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_InspectionImages_InspectionSessions_InspectionSessionId",
                        column: x => x.InspectionSessionId,
                        principalTable: "InspectionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionLogs_InspectionSessions_InspectionSessionId",
                        column: x => x.InspectionSessionId,
                        principalTable: "InspectionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectionLogs_Users_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StageResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageResults_InspectionSessions_InspectionSessionId",
                        column: x => x.InspectionSessionId,
                        principalTable: "InspectionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    MinimumConfidence = table.Column<double>(type: "float", nullable: false),
                    ComparisonStrategy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumericAbsoluteTolerance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    NumericRelativeTolerance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDefinitions", x => x.Id);
                    table.CheckConstraint("CK_FieldDefinitions_MinimumConfidence", "[MinimumConfidence] >= 0 AND [MinimumConfidence] <= 1");
                    table.CheckConstraint("CK_FieldDefinitions_NumericAbsoluteTolerance", "[NumericAbsoluteTolerance] >= 0");
                    table.CheckConstraint("CK_FieldDefinitions_NumericRelativeTolerance", "[NumericRelativeTolerance] >= 0 AND [NumericRelativeTolerance] <= 1");
                    table.ForeignKey(
                        name: "FK_FieldDefinitions_LabelProfiles_LabelProfileId",
                        column: x => x.LabelProfileId,
                        principalTable: "LabelProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OCRResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Engine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExtractedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OCRResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OCRResults_InspectionImages_InspectionImageId",
                        column: x => x.InspectionImageId,
                        principalTable: "InspectionImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldAliases_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldComparisons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MasterValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActualValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MasterConfidence = table.Column<double>(type: "float", nullable: true),
                    ActualConfidence = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldComparisons", x => x.Id);
                    table.CheckConstraint("CK_FieldComparisons_ActualConfidence", "[ActualConfidence] IS NULL OR ([ActualConfidence] >= 0 AND [ActualConfidence] <= 1)");
                    table.CheckConstraint("CK_FieldComparisons_MasterConfidence", "[MasterConfidence] IS NULL OR ([MasterConfidence] >= 0 AND [MasterConfidence] <= 1)");
                    table.ForeignKey(
                        name: "FK_FieldComparisons_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldComparisons_StageResults_StageResultId",
                        column: x => x.StageResultId,
                        principalTable: "StageResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"), "HP", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "HP", null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d02"), "ASUS", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "ASUS", null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d03"), "ACER", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Acer", null },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d04"), "GIGABYTE", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Gigabyte", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Code",
                table: "Brands",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Name",
                table: "Brands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldAliases_FieldDefinitionId_Value",
                table: "FieldAliases",
                columns: new[] { "FieldDefinitionId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldComparisons_FieldDefinitionId",
                table: "FieldComparisons",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldComparisons_StageResultId_FieldKey",
                table: "FieldComparisons",
                columns: new[] { "StageResultId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinitions_LabelProfileId_Key",
                table: "FieldDefinitions",
                columns: new[] { "LabelProfileId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_InspectionSessionId_LabelType_Role",
                table: "InspectionImages",
                columns: new[] { "InspectionSessionId", "LabelType", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_Sha256Hash",
                table: "InspectionImages",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLogs_InspectionSessionId_CreatedAtUtc",
                table: "InspectionLogs",
                columns: new[] { "InspectionSessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLogs_UserAccountId",
                table: "InspectionLogs",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_BrandId",
                table: "InspectionSessions",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_CreatedByUserId",
                table: "InspectionSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_ModelId",
                table: "InspectionSessions",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSessions_Status_CreatedAtUtc",
                table: "InspectionSessions",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabelProfiles_BrandId_ModelId_LabelType_Code_Version",
                table: "LabelProfiles",
                columns: new[] { "BrandId", "ModelId", "LabelType", "Code", "Version" },
                unique: true,
                filter: "[ModelId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabelProfiles_BrandId_LabelType_Code_Version",
                table: "LabelProfiles",
                columns: new[] { "BrandId", "LabelType", "Code", "Version" },
                unique: true,
                filter: "[ModelId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabelProfiles_ModelId",
                table: "LabelProfiles",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Models_BrandId_Code",
                table: "Models",
                columns: new[] { "BrandId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OCRResults_InspectionImageId_CreatedAtUtc",
                table: "OCRResults",
                columns: new[] { "InspectionImageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StageResults_InspectionSessionId_LabelType",
                table: "StageResults",
                columns: new[] { "InspectionSessionId", "LabelType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldAliases");

            migrationBuilder.DropTable(
                name: "FieldComparisons");

            migrationBuilder.DropTable(
                name: "InspectionLogs");

            migrationBuilder.DropTable(
                name: "OCRResults");

            migrationBuilder.DropTable(
                name: "FieldDefinitions");

            migrationBuilder.DropTable(
                name: "StageResults");

            migrationBuilder.DropTable(
                name: "InspectionImages");

            migrationBuilder.DropTable(
                name: "LabelProfiles");

            migrationBuilder.DropTable(
                name: "InspectionSessions");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
