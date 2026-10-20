using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuurWizard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdresToevoeging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Adres_Toevoeging",
                table: "Klant",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Factuuradres_Toevoeging",
                table: "Klant",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KlantGegevens_Adres_Toevoeging",
                table: "Factuur",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LeverancierGegevens_Adres_Toevoeging",
                table: "Factuur",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Adres_Toevoeging",
                table: "Bedrijfsprofiel",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Adres_Toevoeging",
                table: "Klant");

            migrationBuilder.DropColumn(
                name: "Factuuradres_Toevoeging",
                table: "Klant");

            migrationBuilder.DropColumn(
                name: "KlantGegevens_Adres_Toevoeging",
                table: "Factuur");

            migrationBuilder.DropColumn(
                name: "LeverancierGegevens_Adres_Toevoeging",
                table: "Factuur");

            migrationBuilder.DropColumn(
                name: "Adres_Toevoeging",
                table: "Bedrijfsprofiel");
        }
    }
}
