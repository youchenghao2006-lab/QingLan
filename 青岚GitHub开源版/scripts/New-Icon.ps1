# Original geometric artwork: an open book, water and sun. No external image/font.
# Regenerates Assets/AppIcon.ico. Run on Windows; output contains PNG-based ICO data.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path -Parent $PSScriptRoot
$iconPath = Join-Path $repoRoot 'src\QingLan\Assets\AppIcon.ico'
$bitmap = New-Object System.Drawing.Bitmap 256,256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$png = New-Object System.IO.MemoryStream
$output = $null
$writer = $null
$paper = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(247,248,234))
$sun = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(225,193,125))
$wave = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(137,196,205)),6
try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(36,89,111))
    $graphics.FillEllipse($sun,176,32,36,36)
    $leftPage = [System.Drawing.Point[]]@([System.Drawing.Point]::new(44,88),[System.Drawing.Point]::new(120,104),[System.Drawing.Point]::new(120,179),[System.Drawing.Point]::new(44,161))
    $rightPage = [System.Drawing.Point[]]@([System.Drawing.Point]::new(136,104),[System.Drawing.Point]::new(212,88),[System.Drawing.Point]::new(212,161),[System.Drawing.Point]::new(136,179))
    $graphics.FillPolygon($paper,$leftPage)
    $graphics.FillPolygon($paper,$rightPage)
    $graphics.DrawBezier($wave,38,204,90,178,162,230,218,204)
    $bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
    $payload = $png.ToArray()
    $output = [System.IO.File]::Open($iconPath,[System.IO.FileMode]::Create)
    $writer = New-Object System.IO.BinaryWriter $output
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$payload.Length); $writer.Write([uint32]22)
    $writer.Write($payload)
    Write-Output 'Generated Assets/AppIcon.ico from original geometric primitives.'
}
finally {
    if ($writer) { $writer.Dispose() }
    if ($output) { $output.Dispose() }
    $png.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    $paper.Dispose(); $sun.Dispose(); $wave.Dispose()
}
