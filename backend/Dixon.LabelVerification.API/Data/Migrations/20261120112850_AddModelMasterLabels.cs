using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModelMasterLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MasterLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Engine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OcrStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RawExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OcrDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallConfidence = table.Column<double>(type: "float", nullable: true),
                    ProcessingDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ImageQualityStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterLabels", x => x.Id);
                    table.CheckConstraint("CK_MasterLabels_LabelType", "[LabelType] IN ('CO', 'CONFIG')");
                    table.CheckConstraint("CK_MasterLabels_SizeBytes", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_MasterLabels_ApplicationUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MasterLabels_LabelProfiles_LabelProfileId",
                        column: x => x.LabelProfileId,
                        principalTable: "LabelProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MasterLabels_LaptopModels_BrandId_ModelId",
                        columns: x => new { x.BrandId, x.ModelId },
                        principalTable: "LaptopModels",
                        principalColumns: new[] { "BrandId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MasterLabelFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MasterLabelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExtractedValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NormalizedValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    BoundingBoxJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterLabelFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterLabelFields_MasterLabels_MasterLabelId",
                        column: x => x.MasterLabelId,
                        principalTable: "MasterLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterLabelFields_MasterLabelId_FieldKey",
                table: "MasterLabelFields",
                columns: new[] { "MasterLabelId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterLabels_BrandId_ModelId",
                table: "MasterLabels",
                columns: new[] { "BrandId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterLabels_LabelProfileId",
                table: "MasterLabels",
                column: "LabelProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterLabels_ModelId_LabelType",
                table: "MasterLabels",
                columns: new[] { "ModelId", "LabelType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterLabels_UploadedByUserId",
                table: "MasterLabels",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterLabelFields");

            migrationBuilder.DropTable(
                name: "MasterLabels");
        }
    }
}
