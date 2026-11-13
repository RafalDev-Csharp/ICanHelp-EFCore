using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class JobExitResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "JobExits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Response",
                table: "JobExits",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "JobExits");

            migrationBuilder.DropColumn(
                name: "Response",
                table: "JobExits");
        }
    }
}
