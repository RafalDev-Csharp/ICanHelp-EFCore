using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AlertCountryAndAnyCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Geometry>(
                name: "Shape",
                table: "AnnouncementAlerts",
                type: "geography",
                nullable: true,
                oldClrType: typeof(Geometry),
                oldType: "geography");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "AnnouncementAlerts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Geometry>(
                name: "Shape",
                table: "AnnouncementAlerts",
                type: "geography",
                nullable: false,
                oldClrType: typeof(Geometry),
                oldType: "geography",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "AnnouncementAlerts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
