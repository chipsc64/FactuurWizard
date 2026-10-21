<#
.SYNOPSIS
    Bouwt een release van FactuurWizard: tests, publiceren, installatieprogramma maken en (optioneel) uploaden.

.EXAMPLE
    ./build/release.ps1 -Versie 1.0.0
    Maakt Releases/FactuurWizard-win-Setup.exe en de updatebestanden, zonder te uploaden.

.EXAMPLE
    ./build/release.ps1 -Versie 1.0.1 -Upload -Token <GitHub-token>
    Maakt de release en publiceert hem op GitHub Releases. Geïnstalleerde versies zien de update dan vanzelf.
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Versie,
    [switch]$Upload,
    [string]$Token = $env:GITHUB_TOKEN
)

$ErrorActionPreference = 'Stop'
$repo = 'https://github.com/chipsc64/FactuurWizard'
$root = Split-Path $PSScriptRoot
$publish = Join-Path $root 'publish'
$releases = Join-Path $root 'Releases'
Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE) { throw 'dotnet tool restore mislukt' }

    Write-Host "== Tests" -ForegroundColor Cyan
    dotnet test tests/FactuurWizard.Tests -c Release
    if ($LASTEXITCODE) { throw 'Tests mislukt' }

    Write-Host "== Publiceren $Versie" -ForegroundColor Cyan
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish src/FactuurWizard.App -c Release -r win-x64 --self-contained true -p:Version=$Versie -o $publish
    if ($LASTEXITCODE) { throw 'Publiceren mislukt' }

    if ($Upload) {
        # Vorige release ophalen, zodat Velopack een kleine 'delta'-update kan maken.
        Write-Host "== Vorige release ophalen" -ForegroundColor Cyan
        dotnet vpk download github --repoUrl $repo --outputDir $releases --token $Token
    }

    Write-Host "== Installatieprogramma maken" -ForegroundColor Cyan
    $notities = Join-Path $root "Docs/releases/$Versie.md"
    $opties = @(
        'pack', '--packId', 'FactuurWizard', '--packVersion', $Versie, '--packDir', $publish,
        '--mainExe', 'FactuurWizard.exe', '--packTitle', 'FactuurWizard', '--packAuthors', 'FactuurWizard',
        '--icon', (Join-Path $root 'src/FactuurWizard.App/Assets/FactuurWizard.ico'), '--outputDir', $releases
    )
    if (Test-Path $notities) { $opties += @('--releaseNotes', $notities) }
    dotnet vpk @opties
    if ($LASTEXITCODE) { throw 'Verpakken mislukt' }

    if ($Upload) {
        if (-not $Token) { throw 'Geef een GitHub-token mee met -Token of $env:GITHUB_TOKEN.' }
        Write-Host "== Uploaden naar GitHub Releases" -ForegroundColor Cyan
        dotnet vpk upload github --repoUrl $repo --outputDir $releases --publish `
            --releaseName "FactuurWizard $Versie" --tag "v$Versie" --token $Token
        if ($LASTEXITCODE) { throw 'Uploaden mislukt' }
    }

    Write-Host "Klaar. Installatieprogramma: $(Join-Path $releases 'FactuurWizard-win-Setup.exe')" -ForegroundColor Green
}
finally {
    Pop-Location
}
