using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreApi.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ProjectBackendFrontendUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited: EF scaffolds drop + add, which would lose every project's URL. Rename keeps the data.
            migrationBuilder.RenameColumn(
                name: "BaseUrl",
                schema: "core",
                table: "Projects",
                newName: "BackendUrl");

            migrationBuilder.AlterColumn<string>(
                name: "BackendUrl",
                schema: "core",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "FrontendUrl",
                schema: "core",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrontendUrl",
                schema: "core",
                table: "Projects");

            // Projects without a backend get an empty string, as the old column was required.
            migrationBuilder.Sql("UPDATE core.\"Projects\" SET \"BackendUrl\" = '' WHERE \"BackendUrl\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "BackendUrl",
                schema: "core",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "BackendUrl",
                schema: "core",
                table: "Projects",
                newName: "BaseUrl");
        }
    }
}
