using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotamWatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NotamNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IcaoLocation = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    QCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartValidity = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndValidity = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FreeText = table.Column<string>(type: "TEXT", nullable: false),
                    RawText = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Classification = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchedRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RouteKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IcaoCodes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchedRoutes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notams_EndValidity",
                table: "Notams",
                column: "EndValidity");

            migrationBuilder.CreateIndex(
                name: "IX_Notams_IcaoLocation",
                table: "Notams",
                column: "IcaoLocation");

            migrationBuilder.CreateIndex(
                name: "IX_Notams_NotamNumber",
                table: "Notams",
                column: "NotamNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchedRoutes_RouteKey",
                table: "WatchedRoutes",
                column: "RouteKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notams");

            migrationBuilder.DropTable(
                name: "WatchedRoutes");
        }
    }
}
