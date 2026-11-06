using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <summary>
    /// Krok 9: rewizje ogłoszenia (pisana ręcznie – odpowiada temu, co wygenerowałby dotnet ef).
    /// Istniejące ogłoszenia i oferty dostają rewizję 0 (= wersja opublikowana), więc nic nie staje się nieaktualne.
    /// </summary>
    public partial class AnnouncementRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnouncementRevision",
                table: "Offers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Announcements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnnouncementChanges",
                columns: table => new
                {
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedFields = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementChanges", x => new { x.AnnouncementId, x.Revision });
                    table.ForeignKey(
                        name: "FK_AnnouncementChanges_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementFieldChanges",
                columns: table => new
                {
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementFieldChanges", x => new { x.AnnouncementId, x.Revision, x.Field });
                    table.ForeignKey(
                        name: "FK_AnnouncementFieldChanges_AnnouncementChanges_AnnouncementId_Revision",
                        columns: x => new { x.AnnouncementId, x.Revision },
                        principalTable: "AnnouncementChanges",
                        principalColumns: new[] { "AnnouncementId", "Revision" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AnnouncementFieldChanges");

            migrationBuilder.DropTable(name: "AnnouncementChanges");

            migrationBuilder.DropColumn(name: "AnnouncementRevision", table: "Offers");

            migrationBuilder.DropColumn(name: "Revision", table: "Announcements");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Announcements");
        }
    }
}
