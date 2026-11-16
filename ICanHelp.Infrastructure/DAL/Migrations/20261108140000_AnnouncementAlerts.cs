using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <summary>
    /// Krok 14: alerty o nowych ogłoszeniach (pisana ręcznie – odpowiada temu, co wygenerowałby dotnet ef, bez linii o GeoPoint,
    /// plus SQL, którego EF nie modeluje: trigger liczący strefę i indeks przestrzenny).
    /// Shape = punkt (obszar) albo LINESTRING (trasa) – zapisuje EF. Zone = Shape uproszczony (Reduce, 25 m) i zbuforowany
    /// o RadiusKm w metrach – liczy trigger przy każdej zmianie Shape albo RadiusKm, w tej samej transakcji.
    /// </summary>
    public partial class AnnouncementAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnouncementAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveDays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActiveFrom = table.Column<TimeOnly>(type: "time", nullable: true),
                    ActiveTo = table.Column<TimeOnly>(type: "time", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Points = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RadiusKm = table.Column<double>(type: "float", nullable: false),
                    Shape = table.Column<Geometry>(type: "geography", nullable: false),
                    SubcategoryIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Zone = table.Column<Geometry>(type: "geography", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAlerts_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnouncementAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAlerts_CategoryId",
                table: "AnnouncementAlerts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAlerts_UserId",
                table: "AnnouncementAlerts",
                column: "UserId");

            // Bufor w metrach liczy SQL Server (NTS buforowałby w stopniach). MakeValid – na wypadek trasy, która sama
            // siebie przecina; Reduce(25) – uproszczenie polilinii (Douglas-Peucker, tolerancja 25 m << min. promień 500 m).
            // Kolumna obliczana z metodą geography nie może być PERSISTED (nie miałaby indeksu) – dlatego trigger.
            // RECURSIVE_TRIGGERS jest domyślnie wyłączone, a UPDATE samej kolumny Zone i tak nie spełnia warunku.
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_AnnouncementAlerts_Zone] ON [AnnouncementAlerts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF UPDATE([Shape]) OR UPDATE([RadiusKm])
                    BEGIN
                        UPDATE a
                        SET [Zone] = a.[Shape].MakeValid().Reduce(25).STBuffer(a.[RadiusKm] * 1000)
                        FROM [AnnouncementAlerts] a
                        INNER JOIN inserted i ON a.[Id] = i.[Id];
                    END
                END
                """);

            // EF nie modeluje indeksów przestrzennych (nie ma go w snapshocie) – jak IX_Announcements_GeoPoint.
            migrationBuilder.Sql(
                "CREATE SPATIAL INDEX [IX_AnnouncementAlerts_Zone] ON [AnnouncementAlerts] ([Zone]) USING GEOGRAPHY_AUTO_GRID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP TABLE usuwa też trigger i indeks przestrzenny.
            migrationBuilder.DropTable(name: "AnnouncementAlerts");
        }
    }
}
