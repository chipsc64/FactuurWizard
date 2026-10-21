# Handleiding FactuurWizard

FactuurWizard is een programma voor zelfstandigen om eenvoudig facturen te maken. De facturen voldoen aan de Nederlandse regels, worden als PDF bewaard en u houdt bij wie er al betaald heeft. Voor uw boekhouder maakt het programma een btw-overzicht per kwartaal.

---

## 1. Installeren

1. Download **FactuurWizard-win-Setup.exe** van de downloadpagina: <https://github.com/chipsc64/FactuurWizard/releases/latest>
2. Start het bestand. Het kan zijn dat Windows een blauw venster toont met *"Windows heeft uw pc beschermd"*. Dat komt doordat het programma (nog) niet digitaal ondertekend is. Klik op **Meer informatie** en daarna op **Toch uitvoeren**.
3. FactuurWizard wordt geïnstalleerd en gestart. U vindt het daarna in het Startmenu en op het bureaublad.

U hebt geen beheerdersrechten nodig, en u hoeft niets anders te installeren.

### Bijwerken

Bij het starten kijkt FactuurWizard of er een nieuwe versie is. Is dat zo, dan ziet u bovenaan een blauwe balk. Klik op **Nu bijwerken** wanneer het u uitkomt: het programma downloadt de nieuwe versie, sluit af en start opnieuw. Uw gegevens blijven gewoon bewaard.

---

## 2. De eerste keer: inrichting

Loop deze onderdelen onder **Inrichting** (links in het menu) één keer door voordat u de eerste factuur maakt.

### Bedrijfsgegevens

- **Bedrijfsnaam, KvK-nummer en btw-nummer** zijn verplicht op een factuur. Het btw-nummer heeft de vorm `NL123456789B01`.
- **Adres:** vul eerst postcode en huisnummer in. FactuurWizard zoekt straat en plaats dan zelf op (zie [Privacy](#9-uw-gegevens-en-privacy)). Heeft het adres een toevoeging (bijv. 12A of 12-2), kies die dan in de lijst bij *Toevoeging*.
- **IBAN** en de **standaard betalingstermijn** (bijvoorbeeld 14 of 30 dagen).
- **Factuurnummering:** facturen krijgen een nummer als `2026-0001`. Factureerde u eerder al in Word? Vul dan bij *Startnummer* het volgende nummer in, bijvoorbeeld 38 als uw laatste factuur 2026-0037 was. Elk volgend jaar begint weer bij 0001. Na de eerste factuur kunt u dit niet meer wijzigen.
- **Kleineondernemersregeling (KOR):** doet u mee aan de KOR, zet dan het vinkje aan en vul de ingangsdatum in. Uw facturen komen dan zonder btw en met de verplichte vermelding.

### Briefpapier

- **Bestaand briefpapier gebruiken:** hebt u al briefpapier in Word? Sla het in Word op als PDF en kies het hier. De factuur wordt erop gedrukt. Staan uw bedrijfsgegevens en logo al op het briefpapier, dan zet u *Bedrijfsgegevens en logo op de factuur afdrukken* uit.
- **Zelf opmaken:** kies een logo, lettertype en kleuren. Met de **marges** zorgt u dat de tekst niet over een voorbedrukt deel van uw briefpapier valt.
- Klik op **Voorbeeld bekijken** om direct te zien hoe uw factuur eruitziet.

### Vaste teksten

Hier staan de teksten voor de factuur (betalingsvoorwaarden, afsluiting), de e-mail en de herinneringen. Woorden tussen accolades, zoals `{Factuurnummer}` en `{Vervaldatum}`, worden automatisch ingevuld. Rechts op het scherm staat welke woorden u kunt gebruiken.

### Btw-tarieven

De Nederlandse tarieven (21%, 9% en 0%) staan er al in. Verandert de overheid een tarief, voeg dan het nieuwe percentage toe met de ingangsdatum. Een factuur gebruikt altijd het tarief dat geldt op de factuurdatum.

### Producten en diensten

Leg vast wat u vaak verkoopt, met prijs (exclusief btw), eenheid en btw-tarief. Op een factuur kiest u ze dan uit een lijst. De prijs kunt u op de factuur nog aanpassen.

---

## 3. Klanten

Onder **Klanten** legt u uw klanten vast: particulier of bedrijf, naam, adres en e-mailadres. Bij een bedrijf kunt u ook de contactpersoon, het KvK-nummer en het btw-nummer invullen. Elke klant krijgt automatisch een klantnummer.

- Heeft een klant een ander adres voor facturen, zet dan *Afwijkend factuuradres* aan.
- Wilt u een klant langere of kortere tijd geven om te betalen, vul dan een eigen betalingstermijn in.
- Een klant met facturen kunt u niet verwijderen, wel **archiveren**: dan verschijnt hij niet meer in de keuzelijst.

U kunt een klant ook direct bij het maken van een factuur toevoegen.

---

## 4. Een factuur maken

1. Ga naar **Facturen** en klik op **Nieuwe factuur**.
2. Kies de **klant**, of klik op *Nieuwe klant…*.
3. Controleer de **factuurdatum**. Vul eventueel de **leverdatum** in: dat is verplicht als de levering op een andere datum was dan de factuurdatum.
4. Voeg **regels** toe:
   - **+ Vrije regel** voor een losse omschrijving met bedrag;
   - kies een **product of dienst** uit de lijst en klik op **+ Product/dienst**.

   Klik in de tabel op een vakje om de omschrijving, het aantal of de prijs aan te passen. Rechtsonder ziet u direct het totaal met de btw per tarief.
5. Klik op **Opslaan** om later verder te gaan, of op **Voorbeeld (PDF)** om de factuur te bekijken (met de tekst CONCEPT erover).
6. Klik op **Definitief maken** als alles klopt. FactuurWizard controleert eerst of alle verplichte gegevens er zijn. Daarna krijgt de factuur een nummer en wordt de PDF bewaard in de map `Facturen\<jaar>`.

> **Let op:** een definitieve factuur kunt u niet meer wijzigen. Dat is wettelijk zo bedoeld. Klopt er toch iets niet, maak dan een creditfactuur (zie hieronder) en daarna een nieuwe factuur.

### PDF openen en e-mailen

- **PDF openen** toont de bewaarde factuur.
- **E-mailen** opent een nieuw bericht in uw eigen mailprogramma, met de PDF als bijlage en het onderwerp en de tekst uit *Vaste teksten*. U verstuurt het bericht zelf.
  Dit werkt met bijvoorbeeld het klassieke Outlook en Thunderbird. Lukt het automatisch toevoegen van de bijlage niet (bijvoorbeeld met het nieuwe Outlook of webmail), dan opent FactuurWizard een leeg bericht en toont de PDF in Verkenner. Sleep het bestand dan zelf in het bericht.

### Creditfactuur

Bij een definitieve factuur klikt u op **Creditfactuur maken**. Er verschijnt een concept met dezelfde regels, maar met negatieve aantallen. U kunt regels weghalen of aanpassen als u maar een deel crediteert. Maak de creditfactuur daarna definitief.

### Kopiëren

Factureert u regelmatig hetzelfde? Open een eerdere factuur en klik op **Kopiëren als nieuwe factuur**. U krijgt een nieuw concept met dezelfde klant en regels en de datum van vandaag.

---

## 5. Betalingen en herinneringen

In het overzicht **Facturen** ziet u van elke factuur de status:

| Status | Betekenis |
|---|---|
| Concept | Nog niet definitief |
| Openstaand | Nog niet betaald, de vervaldatum is nog niet verstreken |
| Vervallen (rood) | Nog niet betaald en de vervaldatum is verstreken |
| Betaald | U hebt de betaling vastgelegd |
| Gecrediteerd | Volledig verrekend met een creditfactuur |

Bovenaan staat hoeveel er in totaal openstaat. Met de keuzelijsten kunt u bijvoorbeeld alleen de vervallen facturen van dit jaar tonen.

**Betaling vastleggen:** open de factuur, kies de datum bij *Betaling* en klik op **Markeer als betaald**.

**Herinnering sturen:** open een vervallen factuur, kies eventueel een andere termijn en klik op **Herinnering maken**. De eerste keer wordt dat een betalingsherinnering, de keer daarna een tweede herinnering. De brief wordt als PDF bewaard. Met **E-mailen** stuurt u de herinnering samen met de factuur.

---

## 6. Btw-overzicht voor de boekhouder

Onder **Btw-overzicht** kiest u het jaar en het kwartaal. U ziet de bedragen voor de btw-aangifte:

- **1a** omzet en btw hoog tarief;
- **1b** omzet en btw laag tarief;
- **1e** omzet tegen 0%;
- **5a** de btw die u verschuldigd bent.

Facturen tellen mee in het kwartaal van hun factuurdatum. Creditfacturen worden verrekend. Concepten tellen niet mee.

Klik op **Exporteren naar Excel** voor een bestand met een overzicht en een specificatie per factuur dat u aan uw boekhouder kunt geven.

> **Let op:** de btw die u zelf betaald hebt op uw inkopen (voorbelasting, rubriek 5b) staat hier niet in. Die komt uit uw inkoopfacturen.

---

## 7. Backup maken

Onder **Inrichting → Backup** klikt u op **Backup maken…**. Al uw gegevens (klanten, facturen, instellingen en PDF's) komen in één zip-bestand.

- Bewaar backups ook **buiten uw computer**, bijvoorbeeld op een USB-stick of in uw OneDrive-map.
- Maak bijvoorbeeld elke maand een backup, en in elk geval na het maken van facturen voor de btw-aangifte.

**Backup terugzetten**, bijvoorbeeld op een nieuwe computer: installeer FactuurWizard, ga naar *Backup* en klik op **Backup terugzetten…**. Van de bestaande gegevens maakt FactuurWizard eerst automatisch een backup.

Bewaar uw facturen ten minste **7 jaar**: dat is de wettelijke bewaarplicht.

---

## 8. Waar staan mijn gegevens?

Standaard in de map **Documenten\FactuurWizard**:

- `factuurwizard.db`: de database met al uw gegevens;
- `Facturen\<jaar>\`: de PDF's van uw facturen, met de herinneringen in de map `Herinneringen`;
- `Btw-overzichten\`: geëxporteerde Excel-bestanden;
- `Backups\`: backups die u daar hebt opgeslagen.

Een andere map kiest u onder *Bedrijfsgegevens → Opslaglocatie*. Uw gegevens worden dan gekopieerd en de oude map blijft als reservekopie staan.

Als u FactuurWizard verwijdert, blijven uw gegevens in deze map gewoon staan.

---

## 9. Uw gegevens en privacy

Al uw gegevens blijven op uw eigen computer. FactuurWizard maakt alleen in deze gevallen verbinding met internet:

- **Adres opzoeken:** de ingevulde postcode en het huisnummer gaan naar PDOK, de gratis adresservice van de Nederlandse overheid (Kadaster). Andere gegevens, zoals de naam, gaan niet mee.
- **Controleren op updates:** bij het starten kijkt FactuurWizard op GitHub of er een nieuwe versie is. Daarbij worden geen gegevens van u verstuurd.
- **E-mailen:** dat gaat via uw eigen mailprogramma. U verstuurt het bericht zelf.

---

## 10. Problemen oplossen

| Probleem | Oplossing |
|---|---|
| Windows waarschuwt bij het installeren | Klik op **Meer informatie** en daarna op **Toch uitvoeren** (zie [Installeren](#1-installeren)). |
| *"De factuur kan nog niet definitief gemaakt worden"* | De melding zegt welk verplicht gegeven ontbreekt, bijvoorbeeld uw KvK-nummer of het adres van de klant. Vul het aan en probeer opnieuw. |
| Het adres wordt niet gevonden | Controleer postcode en huisnummer. Zonder internet, of als de adresservice tijdelijk niet werkt, vult u het adres gewoon zelf in. |
| De PDF komt niet als bijlage in de e-mail | Zie *PDF openen en e-mailen* in hoofdstuk 4. Sleep de PDF uit Verkenner in het bericht. |
| Er gaat iets anders mis | Fouten worden bijgehouden in `%AppData%\FactuurWizard\fouten.log`. Stuur dat bestand mee als u om hulp vraagt. |
