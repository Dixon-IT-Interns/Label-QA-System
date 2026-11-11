using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHpModelAndFieldSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "FieldDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FieldDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.InsertData(
                table: "Models",
                columns: new[] { "Id", "BrandId", "Code", "CreatedAtUtc", "IsActive", "Name", "UpdatedAtUtc" },
                values: new object[] { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"), "15-fd1652TU", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "15-fd1652TU", null });

            migrationBuilder.InsertData(
                table: "LabelProfiles",
                columns: new[] { "Id", "BrandId", "Code", "CreatedAtUtc", "IsActive", "LabelType", "ModelId", "Name", "UpdatedAtUtc", "Version" },
                values: new object[,]
                {
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d06"), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"), "HP-15-FD1652TU-CO", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "CO", new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"), "HP 15-fd1652TU CO", null, 1 },
                    { new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d07"), new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d01"), "HP-15-FD1652TU-CONFIG", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "CONFIG", new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"), "HP 15-fd1652TU Config", null, 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d06"));

            migrationBuilder.DeleteData(
                table: "LabelProfiles",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d07"));

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: new Guid("73a3f07d-c906-4e49-a3db-4854ab1a9d05"));

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "FieldDefinitions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FieldDefinitions");
        }
    }
}
