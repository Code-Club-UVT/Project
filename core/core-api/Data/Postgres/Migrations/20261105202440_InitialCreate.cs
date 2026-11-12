using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreApi.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => new { x.UserId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_ProjectMembers_UserProfiles_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalSchema: "core",
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalSchema: "core",
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRoles",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRoles", x => x.Id);
                    table.UniqueConstraint("AK_ProjectRoles_ProjectId_Id", x => new { x.ProjectId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HealthUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TokenMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Exchange"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.CheckConstraint("CK_Projects_PassThrough_System", "\"TokenMode\" <> 'PassThrough' OR \"IsSystem\"");
                    table.CheckConstraint("CK_Projects_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "FK_Projects_ProjectRoles_DefaultRoleId",
                        column: x => x.DefaultRoleId,
                        principalSchema: "core",
                        principalTable: "ProjectRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_GrantedByUserId",
                schema: "core",
                table: "ProjectMembers",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId",
                schema: "core",
                table: "ProjectMembers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_ProjectRoleId",
                schema: "core",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "ProjectRoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectRoleId",
                schema: "core",
                table: "ProjectMembers",
                column: "ProjectRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRoles_ProjectId_Name",
                schema: "core",
                table: "ProjectRoles",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DefaultRoleId",
                schema: "core",
                table: "Projects",
                column: "DefaultRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Slug",
                schema: "core",
                table: "Projects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_Username",
                schema: "core",
                table: "UserProfiles",
                column: "Username");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMembers_ProjectRoles_ProjectId_ProjectRoleId",
                schema: "core",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "ProjectRoleId" },
                principalSchema: "core",
                principalTable: "ProjectRoles",
                principalColumns: new[] { "ProjectId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMembers_Projects_ProjectId",
                schema: "core",
                table: "ProjectMembers",
                column: "ProjectId",
                principalSchema: "core",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectRoles_Projects_ProjectId",
                schema: "core",
                table: "ProjectRoles",
                column: "ProjectId",
                principalSchema: "core",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectRoles_DefaultRoleId",
                schema: "core",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectMembers",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserProfiles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ProjectRoles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "core");
        }
    }
}
