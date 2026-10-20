namespace FactuurWizard.Core.Model;

public enum KlantSoort
{
    Particulier = 0,
    Bedrijf = 1,
}

/// <summary>
/// Soort btw-groep. Bepaalt in welke rubriek van de btw-aangifte de omzet valt.
/// </summary>
public enum BtwSoort
{
    /// <summary>Rubriek 1a – hoog tarief.</summary>
    Hoog = 0,
    /// <summary>Rubriek 1b – laag tarief.</summary>
    Laag = 1,
    /// <summary>Rubriek 1e – 0% of niet bij u belast.</summary>
    Nul = 2,
}

public enum ArtikelSoort
{
    Product = 0,
    Dienst = 1,
}

public enum RegelSoort
{
    Vrij = 0,
    Product = 1,
    Dienst = 2,
}

public enum FactuurSoort
{
    Factuur = 0,
    Creditfactuur = 1,
}

public enum FactuurStatus
{
    Concept = 0,
    Definitief = 1,
}

/// <summary>
/// Afgeleide betaalstatus; wordt niet opgeslagen maar berekend uit BetaaldOp en Vervaldatum.
/// </summary>
public enum BetaalStatus
{
    NietVanToepassing = 0,
    Openstaand = 1,
    Vervallen = 2,
    Betaald = 3,
    /// <summary>Volledig verrekend met een of meer creditfacturen.</summary>
    Gecrediteerd = 4,
}

public enum TekstSoort
{
    Betalingsvoorwaarden = 0,
    AlgemeneVoorwaarden = 1,
    Afsluittekst = 2,
    EmailOnderwerp = 3,
    EmailTekst = 4,
    Herinnering1Tekst = 5,
    Herinnering2Tekst = 6,
    HerinneringEmailOnderwerp = 7,
    HerinneringEmailTekst = 8,
}

public enum AchtergrondSoort
{
    Geen = 0,
    Afbeelding = 1,
    Pdf = 2,
}
