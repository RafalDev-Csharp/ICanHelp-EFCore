using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementGeoPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Krok 7: punkt geography liczony przez SQL Server z Latitude/Longitude (PERSISTED - pod indeks przestrzenny).
            migrationBuilder.AddColumn<Point>(
                name: "GeoPoint",
                table: "Announcements",
                type: "geography",
                nullable: true,
                computedColumnSql: "geography::Point([Latitude], [Longitude], 4326)",
                stored: true);

            // EF nie modeluje indeksów przestrzennych - dlatego SQL ręcznie (nie ma go w snapshocie).
            migrationBuilder.Sql(
                "CREATE SPATIAL INDEX [IX_Announcements_GeoPoint] ON [Announcements] ([GeoPoint]) USING GEOGRAPHY_AUTO_GRID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX [IX_Announcements_GeoPoint] ON [Announcements];");

            migrationBuilder.DropColumn(
                name: "GeoPoint",
                table: "Announcements");
        }
    }
}
