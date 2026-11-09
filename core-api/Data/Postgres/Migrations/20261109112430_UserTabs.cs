using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreApi.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class UserTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTabs",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    LastPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTabs", x => new { x.UserId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_UserTabs_ProjectMembers_UserId_ProjectId",
                        columns: x => new { x.UserId, x.ProjectId },
                        principalSchema: "core",
                        principalTable: "ProjectMembers",
                        principalColumns: new[] { "UserId", "ProjectId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTabs_UserId_Position",
                schema: "core",
                table: "UserTabs",
                columns: new[] { "UserId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTabs",
                schema: "core");
        }
    }
}
