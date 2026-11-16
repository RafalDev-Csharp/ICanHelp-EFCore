using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementJobTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Announcements");
        }
    }
}
