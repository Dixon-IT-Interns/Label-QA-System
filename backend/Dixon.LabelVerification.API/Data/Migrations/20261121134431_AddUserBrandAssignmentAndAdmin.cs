using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBrandAssignmentAndAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExtractedFields_ApplicationUsers_CorrectedByUserId",
                table: "ExtractedFields");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionImages_ApplicationUsers_UploadedByUserId",
                table: "InspectionImages");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionLogs_ApplicationUsers_UserAccountId",
                table: "InspectionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReports_ApplicationUsers_GeneratedByUserId",
                table: "InspectionReports");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionSessions_ApplicationUsers_CreatedByUserId",
                table: "InspectionSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_MasterLabels_ApplicationUsers_UploadedByUserId",
                table: "MasterLabels");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedByUserId",
                table: "MasterLabels",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "InspectionSessions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "GeneratedByUserId",
                table: "InspectionReports",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedByUserId",
                table: "InspectionImages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.Sql(
                "UPDATE [InspectionSessions] SET [CreatedByUserId] = NULL " +
                "WHERE [CreatedByUserId] IN (SELECT [Id] FROM [ApplicationUsers] WHERE [IsActive] = 0); " +
                "UPDATE [InspectionReports] SET [GeneratedByUserId] = NULL " +
                "WHERE [GeneratedByUserId] IN (SELECT [Id] FROM [ApplicationUsers] WHERE [IsActive] = 0); " +
                "UPDATE [InspectionLogs] SET [UserAccountId] = NULL " +
                "WHERE [UserAccountId] IN (SELECT [Id] FROM [ApplicationUsers] WHERE [IsActive] = 0); " +
                "UPDATE [ExtractedFields] SET [CorrectedByUserId] = NULL " +
                "WHERE [CorrectedByUserId] IN (SELECT [Id] FROM [ApplicationUsers] WHERE [IsActive] = 0); " +
                "UPDATE [InspectionImages] SET [UploadedByUserId] = NULL " +
                "WHERE [UploadedByUserId] IN (SELECT [Id] FROM [ApplicationUsers] WHERE [IsActive] = 0); " +
                "UPDATE [MasterLabels] SET [UploadedByUserId] = NULL " +
                "WHERE [UploadedByUserId] IN (SELECT [Id] FROM [ApplicationUsers] WHERE [IsActive] = 0); " +
                "DELETE FROM [ApplicationUsers] WHERE [IsActive] = 0;");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ApplicationUsers");

            migrationBuilder.AddColumn<Guid>(
                name: "BrandId",
                table: "ApplicationUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [u] SET [BrandId] = (" +
                "SELECT CONVERT(uniqueidentifier, MIN(CONVERT(char(36), [i].[BrandId]))) " +
                "FROM [InspectionSessions] AS [i] WHERE [i].[CreatedByUserId] = [u].[Id] " +
                "HAVING COUNT(DISTINCT [i].[BrandId]) = 1) " +
                "FROM [ApplicationUsers] AS [u] " +
                "WHERE [u].[Role] IN (N'Supervisor', N'Operator') AND EXISTS (" +
                "SELECT 1 FROM [InspectionSessions] AS [i] WHERE [i].[CreatedByUserId] = [u].[Id]);");

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Code", "Name" },
                values: new object[] { "Admin", "Admin" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_BrandId",
                table: "ApplicationUsers",
                column: "BrandId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers",
                sql: "[Role] IN ('Admin', 'Supervisor', 'Operator')");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationUsers_Brands_BrandId",
                table: "ApplicationUsers",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExtractedFields_ApplicationUsers_CorrectedByUserId",
                table: "ExtractedFields",
                column: "CorrectedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionImages_ApplicationUsers_UploadedByUserId",
                table: "InspectionImages",
                column: "UploadedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionLogs_ApplicationUsers_UserAccountId",
                table: "InspectionLogs",
                column: "UserAccountId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReports_ApplicationUsers_GeneratedByUserId",
                table: "InspectionReports",
                column: "GeneratedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionSessions_ApplicationUsers_CreatedByUserId",
                table: "InspectionSessions",
                column: "CreatedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MasterLabels_ApplicationUsers_UploadedByUserId",
                table: "MasterLabels",
                column: "UploadedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "This migration permanently removes inactive user accounts and cannot be reversed without a database backup.");
    }
}
