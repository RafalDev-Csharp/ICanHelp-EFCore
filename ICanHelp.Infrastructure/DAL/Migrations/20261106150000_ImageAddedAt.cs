using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ICanHelp.Infrastructure.DAL.Migrations
{
    /// <summary>
    /// Krok 10: kiedy dodano zdjęcie (pisana ręcznie – odpowiada temu, co wygenerowałby dotnet ef).
    /// Najstarsze zdjęcie to miniatura ogłoszenia. Wcześniej nie dało się wgrywać zdjęć, więc domyślna wartość nie ma znaczenia.
    /// </summary>
    public partial class ImageAddedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddedAt",
                table: "Images",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AddedAt", table: "Images");
        }
    }
}
