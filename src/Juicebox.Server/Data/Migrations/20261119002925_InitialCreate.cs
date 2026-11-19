using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juicebox.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HostSecretKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    JoinPassword = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPing = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Destroyed = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "INTEGER", nullable: false),
                    MemberName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                    MemberSecretKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPing = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Destroyed = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => new { x.SessionId, x.MemberName });
                    table.ForeignKey(
                        name: "FK_Members_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_Destroyed",
                table: "Members",
                column: "Destroyed");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Destroyed",
                table: "Sessions",
                column: "Destroyed");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_JoinPassword",
                table: "Sessions",
                column: "JoinPassword",
                unique: true,
                filter: "\"Destroyed\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
