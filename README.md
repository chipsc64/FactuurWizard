# FactuurWizard

Facturen maken voor kleine zelfstandigen in Nederland, als vervanging van facturen in Word.

- Facturen volgens de Nederlandse regels, als PDF op uw eigen briefpapier
- Klanten, producten en diensten bijhouden
- Creditfacturen, betaalstatus en betalingsherinneringen
- Btw-overzicht per kwartaal (aangifterubrieken) met export naar Excel voor de boekhouder
- Adres opzoeken op postcode en huisnummer
- Backup in één zip-bestand; automatische updates

**Downloaden:** [FactuurWizard-win-Setup.exe](https://github.com/chipsc64/FactuurWizard/releases/latest) (Windows 10/11).
Hoe het werkt staat in de [handleiding](Docs/Handleiding.md).

## Ontwikkelen

Vereist: .NET 10 SDK (zie `global.json`).

```powershell
dotnet tool restore
dotnet test
dotnet run --project src/FactuurWizard.App
```

| Project | Inhoud |
|---|---|
| `src/FactuurWizard.Core` | Domeinmodel en rekenregels (btw, nummering, controles, btw-rapportage) |
| `src/FactuurWizard.Data` | Database (EF Core + SQLite), opslag, backup, adres opzoeken |
| `src/FactuurWizard.Pdf` | Opmaak van facturen en herinneringen (QuestPDF) |
| `src/FactuurWizard.Export` | Excel-export van het btw-overzicht (ClosedXML) |
| `src/FactuurWizard.App` | WPF-programma |
| `tests/FactuurWizard.Tests` | Unit tests |

Met `FACTUURWIZARD_DATAMAP` gebruikt het programma een andere datamap, bijvoorbeeld om te testen zonder uw eigen administratie te raken.

### Database wijzigen

```powershell
dotnet ef migrations add <Naam> --project src/FactuurWizard.Data --output-dir Migrations
```

Migraties worden bij het opstarten automatisch uitgevoerd.

### Release maken

Een nieuwe versie publiceren gaat via een versietag; GitHub Actions bouwt, test en publiceert dan automatisch (zie `.github/workflows/release.yml`):

```powershell
git tag v1.0.1
git push origin v1.0.1
```

Lokaal een installatieprogramma maken, zonder te publiceren: `./build/release.ps1 -Versie 1.0.1` (uitvoer in `Releases/`). Release notes kunnen in `Docs/releases/<versie>.md`.

## Licentie

FactuurWizard is vrije software onder de [GNU General Public License versie 3](LICENSE) (of later), met een aanvullende toestemming voor het gebruik van QuestPDF. Zie [NOTICE.md](NOTICE.md).

## Gebruikte onderdelen

| Onderdeel | Licentie |
|---|---|
| [QuestPDF](https://www.questpdf.com) | Community-licentie (gratis voor organisaties met minder dan $1M omzet) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) | MIT |
| [Velopack](https://velopack.io) | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT |
| Entity Framework Core, SQLite | MIT / public domain |
| Adresgegevens: [PDOK](https://www.pdok.nl) (BAG, Kadaster) | Open data |
