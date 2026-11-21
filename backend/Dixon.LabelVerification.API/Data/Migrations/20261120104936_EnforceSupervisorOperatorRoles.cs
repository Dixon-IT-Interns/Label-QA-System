using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dixon.LabelVerification.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSupervisorOperatorRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers");

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Code", "Name" },
                values: new object[] { "Supervisor", "Supervisor" });

            migrationBuilder.Sql(
                "UPDATE [ApplicationUsers] SET [Role] = N'Supervisor' WHERE [Role] = N'Admin'; " +
                "UPDATE [ApplicationUsers] SET [Role] = N'Operator' WHERE [Role] = N'Viewer';");

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Code",
                keyValue: "Admin");

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Code",
                keyValue: "Viewer");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers",
                sql: "[Role] IN ('Supervisor', 'Operator')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers");

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Code", "Name" },
                values: new object[] { "Admin", "Administrator" });

            migrationBuilder.Sql(
                "UPDATE [ApplicationUsers] SET [Role] = N'Admin' WHERE [Role] = N'Supervisor';");

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Code",
                keyValue: "Supervisor");

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Code", "Name" },
                values: new object[] { "Viewer", "Viewer" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationUsers_Role",
                table: "ApplicationUsers",
                sql: "[Role] IN ('Admin', 'Operator', 'Viewer')");
        }
    }
}
