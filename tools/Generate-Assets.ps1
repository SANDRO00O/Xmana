$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path $PSScriptRoot '..\src\Xmana\Assets'
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

function New-RoundedPath([System.Drawing.RectangleF]$rect, [single]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-XmanaAsset([int]$width, [int]$height, [string]$fileName, [bool]$wide) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::FromArgb(11, 13, 18))

    if ($wide) {
        $logoSize = [Math]::Min($height * 0.62, 96)
        $left = [single]($width * 0.075)
        $top = [single](($height - $logoSize) / 2)
        $path = New-RoundedPath ([System.Drawing.RectangleF]::new($left, $top, $logoSize, $logoSize)) ($logoSize * 0.22)
        $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(86, 214, 194)), $path)
        $font = [System.Drawing.Font]::new('Segoe UI', [single]($logoSize * 0.66), [System.Drawing.FontStyle]::Bold)
        $graphics.DrawString('X', $font, [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(8, 19, 24)), $left + ($logoSize * 0.19), $top + ($logoSize * 0.02))
        $titleFont = [System.Drawing.Font]::new('Segoe UI', [single]($height * 0.22), [System.Drawing.FontStyle]::Bold)
        $graphics.DrawString('XMANA', $titleFont, [System.Drawing.SolidBrush]::White, $left + $logoSize + ($width * 0.035), [single]($height * 0.25))
        $subFont = [System.Drawing.Font]::new('Segoe UI', [single]($height * 0.08), [System.Drawing.FontStyle]::Regular)
        $graphics.DrawString('CINEMATIC LIBRARY', $subFont, [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(157, 172, 190)), $left + $logoSize + ($width * 0.04), [single]($height * 0.59))
    }
    else {
        $size = [single]([Math]::Min($width, $height) * 0.78)
        $left = [single](($width - $size) / 2)
        $top = [single](($height - $size) / 2)
        $path = New-RoundedPath ([System.Drawing.RectangleF]::new($left, $top, $size, $size)) ($size * 0.2)
        $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(86, 214, 194)), $path)
        $font = [System.Drawing.Font]::new('Segoe UI', [single]($size * 0.67), [System.Drawing.FontStyle]::Bold)
        $graphics.DrawString('X', $font, [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(8, 19, 24)), $left + ($size * 0.18), $top + ($size * 0.015))
    }

    $output = Join-Path $assetDir $fileName
    $bitmap.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    if ($path) { $path.Dispose() }
    Write-Host "Generated $output"
}

New-XmanaAsset 44 44 'Square44x44Logo.png' $false
New-XmanaAsset 150 150 'Square150x150Logo.png' $false
New-XmanaAsset 310 150 'Wide310x150Logo.png' $true
New-XmanaAsset 620 300 'SplashScreen.png' $true
