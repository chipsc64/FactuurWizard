# Maakt src/FactuurWizard.App/Assets/FactuurWizard.ico: een afgerond blauw vlak met een witte "F" en een euroteken.
# Het ICO-bestand bevat PNG-afbeeldingen in meerdere formaten (ondersteund vanaf Windows Vista).
param([string]$Uitvoer = (Join-Path $PSScriptRoot '..\src\FactuurWizard.App\Assets\FactuurWizard.ico'))

Add-Type -AssemblyName System.Drawing

function Teken([int]$grootte) {
    $bmp = New-Object System.Drawing.Bitmap $grootte, $grootte
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [Math]::Max(2, $grootte * 0.2)
    $pad = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $grootte - 1
    $pad.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $pad.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $pad.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $pad.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $pad.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#1F4E79'))), $pad)

    $wit = [System.Drawing.Brushes]::White
    $formaat = New-Object System.Drawing.StringFormat
    $formaat.Alignment = 'Center'; $formaat.LineAlignment = 'Center'
    $letter = New-Object System.Drawing.Font 'Segoe UI', ($grootte * 0.62), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('F', $letter, $wit, (New-Object System.Drawing.RectangleF (-$grootte * 0.06), ($grootte * 0.02), $grootte, $grootte), $formaat)
    if ($grootte -ge 32) {
        $euro = New-Object System.Drawing.Font 'Segoe UI', ($grootte * 0.3), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $g.DrawString([string][char]0x20AC, $euro, (New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#9CC3E6'))),
            (New-Object System.Drawing.RectangleF ($grootte * 0.42), ($grootte * 0.38), ($grootte * 0.6), ($grootte * 0.6)), $formaat)
    }
    $g.Dispose()
    $stroom = New-Object System.IO.MemoryStream
    $bmp.Save($stroom, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $stroom.ToArray()
}

$groottes = 16, 24, 32, 48, 64, 128, 256
$afbeeldingen = $groottes | ForEach-Object { , (Teken $_) }

New-Item -ItemType Directory -Force (Split-Path $Uitvoer) | Out-Null
$uit = New-Object System.IO.BinaryWriter ([System.IO.File]::Create($Uitvoer))
$uit.Write([UInt16]0); $uit.Write([UInt16]1); $uit.Write([UInt16]$groottes.Count)
$positie = 6 + 16 * $groottes.Count
for ($i = 0; $i -lt $groottes.Count; $i++) {
    $z = $groottes[$i]
    $uit.Write([byte]($z % 256)); $uit.Write([byte]($z % 256)); $uit.Write([byte]0); $uit.Write([byte]0)
    $uit.Write([UInt16]1); $uit.Write([UInt16]32)
    $uit.Write([UInt32]$afbeeldingen[$i].Length); $uit.Write([UInt32]$positie)
    $positie += $afbeeldingen[$i].Length
}
foreach ($a in $afbeeldingen) { $uit.Write($a) }
$uit.Close()
"Pictogram gemaakt: $Uitvoer"
