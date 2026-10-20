using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuurWizard.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initieel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bedrijfsprofiel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bedrijfsnaam = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Straat = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Huisnummer = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Postcode = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Plaats = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Land = table.Column<string>(type: "TEXT", nullable: false),
                    KvkNummer = table.Column<string>(type: "TEXT", nullable: false),
                    BtwNummer = table.Column<string>(type: "TEXT", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", nullable: false),
                    Bic = table.Column<string>(type: "TEXT", nullable: false),
                    TenaamstellingRekening = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Telefoon = table.Column<string>(type: "TEXT", nullable: false),
                    Website = table.Column<string>(type: "TEXT", nullable: false),
                    StandaardBetalingstermijnDagen = table.Column<int>(type: "INTEGER", nullable: false),
                    NummeringEersteJaar = table.Column<int>(type: "INTEGER", nullable: false),
                    NummeringStartnummer = table.Column<int>(type: "INTEGER", nullable: false),
                    KorIngangsdatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    KorEinddatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Taal = table.Column<string>(type: "TEXT", nullable: false),
                    Valuta = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bedrijfsprofiel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Briefpapier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    IsStandaard = table.Column<bool>(type: "INTEGER", nullable: false),
                    Logo = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LogoBreedteMm = table.Column<double>(type: "REAL", nullable: false),
                    AchtergrondSoort = table.Column<int>(type: "INTEGER", nullable: false),
                    Achtergrond = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Lettertype = table.Column<string>(type: "TEXT", nullable: false),
                    Accentkleur = table.Column<string>(type: "TEXT", nullable: false),
                    Tekstkleur = table.Column<string>(type: "TEXT", nullable: false),
                    ToonBedrijfsgegevens = table.Column<bool>(type: "INTEGER", nullable: false),
                    BedrijfsgegevensRechts = table.Column<bool>(type: "INTEGER", nullable: false),
                    MargeBovenMm = table.Column<double>(type: "REAL", nullable: false),
                    MargeOnderMm = table.Column<double>(type: "REAL", nullable: false),
                    MargeLinksMm = table.Column<double>(type: "REAL", nullable: false),
                    MargeRechtsMm = table.Column<double>(type: "REAL", nullable: false),
                    Voettekst = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Briefpapier", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BtwGroep",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BtwGroep", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Klant",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Klantnummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", nullable: false),
                    Contactpersoon = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Straat = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Huisnummer = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Postcode = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Plaats = table.Column<string>(type: "TEXT", nullable: false),
                    Adres_Land = table.Column<string>(type: "TEXT", nullable: false),
                    Factuuradres_Straat = table.Column<string>(type: "TEXT", nullable: true),
                    Factuuradres_Huisnummer = table.Column<string>(type: "TEXT", nullable: true),
                    Factuuradres_Postcode = table.Column<string>(type: "TEXT", nullable: true),
                    Factuuradres_Plaats = table.Column<string>(type: "TEXT", nullable: true),
                    Factuuradres_Land = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Telefoon = table.Column<string>(type: "TEXT", nullable: false),
                    KvkNummer = table.Column<string>(type: "TEXT", nullable: false),
                    BtwNummer = table.Column<string>(type: "TEXT", nullable: false),
                    BetalingstermijnDagen = table.Column<int>(type: "INTEGER", nullable: true),
                    Notities = table.Column<string>(type: "TEXT", nullable: false),
                    Gearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Klant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nummerreeks",
                columns: table => new
                {
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    LaatsteVolgnummer = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nummerreeks", x => x.Jaar);
                });

            migrationBuilder.CreateTable(
                name: "Tekstsjabloon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Taal = table.Column<string>(type: "TEXT", nullable: false),
                    Tekst = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tekstsjabloon", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Artikel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Artikelnummer = table.Column<string>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: false),
                    Eenheid = table.Column<string>(type: "TEXT", nullable: false),
                    PrijsExclBtw = table.Column<decimal>(type: "TEXT", nullable: false),
                    BtwGroepId = table.Column<int>(type: "INTEGER", nullable: false),
                    Gearchiveerd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artikel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artikel_BtwGroep_BtwGroepId",
                        column: x => x.BtwGroepId,
                        principalTable: "BtwGroep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BtwTarief",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BtwGroepId = table.Column<int>(type: "INTEGER", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    Ingangsdatum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Einddatum = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BtwTarief", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BtwTarief_BtwGroep_BtwGroepId",
                        column: x => x.BtwGroepId,
                        principalTable: "BtwGroep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Factuur",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Factuurnummer = table.Column<string>(type: "TEXT", nullable: true),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: true),
                    Volgnummer = table.Column<int>(type: "INTEGER", nullable: true),
                    KlantId = table.Column<int>(type: "INTEGER", nullable: false),
                    KlantGegevens_Naam = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Contactpersoon = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Adres_Straat = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Adres_Huisnummer = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Adres_Postcode = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Adres_Plaats = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Adres_Land = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_KvkNummer = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_BtwNummer = table.Column<string>(type: "TEXT", nullable: false),
                    KlantGegevens_Email = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Naam = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Contactpersoon = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Adres_Straat = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Adres_Huisnummer = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Adres_Postcode = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Adres_Plaats = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Adres_Land = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_KvkNummer = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_BtwNummer = table.Column<string>(type: "TEXT", nullable: false),
                    LeverancierGegevens_Email = table.Column<string>(type: "TEXT", nullable: false),
                    Factuurdatum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Leverdatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LeverperiodeEind = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    BetalingstermijnDagen = table.Column<int>(type: "INTEGER", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", nullable: false),
                    KorToegepast = table.Column<bool>(type: "INTEGER", nullable: false),
                    Taal = table.Column<string>(type: "TEXT", nullable: false),
                    Valuta = table.Column<string>(type: "TEXT", nullable: false),
                    Opmerking = table.Column<string>(type: "TEXT", nullable: false),
                    BriefpapierId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrigineleFactuurId = table.Column<int>(type: "INTEGER", nullable: true),
                    BetaaldOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DefinitiefOp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PdfBestand = table.Column<string>(type: "TEXT", nullable: true),
                    TotaalExclBtw = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotaalBtw = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotaalInclBtw = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factuur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factuur_Briefpapier_BriefpapierId",
                        column: x => x.BriefpapierId,
                        principalTable: "Briefpapier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Factuur_Factuur_OrigineleFactuurId",
                        column: x => x.OrigineleFactuurId,
                        principalTable: "Factuur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factuur_Klant_KlantId",
                        column: x => x.KlantId,
                        principalTable: "Klant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Factuurregel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FactuurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtikelId = table.Column<int>(type: "INTEGER", nullable: true),
                    Artikelnummer = table.Column<string>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", nullable: false),
                    Aantal = table.Column<decimal>(type: "TEXT", nullable: false),
                    Eenheid = table.Column<string>(type: "TEXT", nullable: false),
                    PrijsExclBtw = table.Column<decimal>(type: "TEXT", nullable: false),
                    BtwGroepId = table.Column<int>(type: "INTEGER", nullable: false),
                    BtwPercentage = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factuurregel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factuurregel_Artikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "Artikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Factuurregel_BtwGroep_BtwGroepId",
                        column: x => x.BtwGroepId,
                        principalTable: "BtwGroep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factuurregel_Factuur_FactuurId",
                        column: x => x.FactuurId,
                        principalTable: "Factuur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Herinnering",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FactuurId = table.Column<int>(type: "INTEGER", nullable: false),
                    Niveau = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BetalingstermijnDagen = table.Column<int>(type: "INTEGER", nullable: false),
                    PdfBestand = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Herinnering", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Herinnering_Factuur_FactuurId",
                        column: x => x.FactuurId,
                        principalTable: "Factuur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artikel_Artikelnummer",
                table: "Artikel",
                column: "Artikelnummer");

            migrationBuilder.CreateIndex(
                name: "IX_Artikel_BtwGroepId",
                table: "Artikel",
                column: "BtwGroepId");

            migrationBuilder.CreateIndex(
                name: "IX_BtwTarief_BtwGroepId",
                table: "BtwTarief",
                column: "BtwGroepId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuur_BriefpapierId",
                table: "Factuur",
                column: "BriefpapierId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuur_Factuurdatum",
                table: "Factuur",
                column: "Factuurdatum");

            migrationBuilder.CreateIndex(
                name: "IX_Factuur_Factuurnummer",
                table: "Factuur",
                column: "Factuurnummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factuur_KlantId",
                table: "Factuur",
                column: "KlantId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuur_OrigineleFactuurId",
                table: "Factuur",
                column: "OrigineleFactuurId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuurregel_ArtikelId",
                table: "Factuurregel",
                column: "ArtikelId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuurregel_BtwGroepId",
                table: "Factuurregel",
                column: "BtwGroepId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuurregel_FactuurId",
                table: "Factuurregel",
                column: "FactuurId");

            migrationBuilder.CreateIndex(
                name: "IX_Herinnering_FactuurId",
                table: "Herinnering",
                column: "FactuurId");

            migrationBuilder.CreateIndex(
                name: "IX_Klant_Klantnummer",
                table: "Klant",
                column: "Klantnummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Klant_Naam",
                table: "Klant",
                column: "Naam");

            migrationBuilder.CreateIndex(
                name: "IX_Tekstsjabloon_Soort_Taal",
                table: "Tekstsjabloon",
                columns: new[] { "Soort", "Taal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bedrijfsprofiel");

            migrationBuilder.DropTable(
                name: "BtwTarief");

            migrationBuilder.DropTable(
                name: "Factuurregel");

            migrationBuilder.DropTable(
                name: "Herinnering");

            migrationBuilder.DropTable(
                name: "Nummerreeks");

            migrationBuilder.DropTable(
                name: "Tekstsjabloon");

            migrationBuilder.DropTable(
                name: "Artikel");

            migrationBuilder.DropTable(
                name: "Factuur");

            migrationBuilder.DropTable(
                name: "BtwGroep");

            migrationBuilder.DropTable(
                name: "Briefpapier");

            migrationBuilder.DropTable(
                name: "Klant");
        }
    }
}
