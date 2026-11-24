using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixon.LabelVerification.API.Data.Migrations;

public partial class AddActivityLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ActivityLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                BrandName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                InspectionSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                table.ForeignKey("FK_ActivityLogs_ApplicationUsers_UserAccountId", x => x.UserAccountId, "ApplicationUsers", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ActivityLogs_Brands_BrandId", x => x.BrandId, "Brands", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ActivityLogs_InspectionSessions_InspectionSessionId", x => x.InspectionSessionId, "InspectionSessions", "Id", onDelete: ReferentialAction.SetNull);
            });
        migrationBuilder.CreateIndex(name: "IX_ActivityLogs_BrandId_CreatedAtUtc", table: "ActivityLogs", columns: new[] { "BrandId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_ActivityLogs_UserAccountId_CreatedAtUtc", table: "ActivityLogs", columns: new[] { "UserAccountId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "ActivityLogs");
}
