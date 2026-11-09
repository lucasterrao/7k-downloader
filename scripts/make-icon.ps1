$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) 'assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$bitmap = New-Object Drawing.Bitmap 128,128
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.Color]::FromArgb(54,171,86))
$font = New-Object Drawing.Font 'Segoe UI',40,([Drawing.FontStyle]::Bold)
$white = [Drawing.Brushes]::White
$graphics.DrawString('7K',$font,$white,8,10)
$pen = New-Object Drawing.Pen ([Drawing.Color]::White),7
$graphics.DrawLine($pen,64,77,64,112)
$graphics.DrawLine($pen,48,96,64,112)
$graphics.DrawLine($pen,80,96,64,112)
$image = [Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [IO.File]::Create((Join-Path $assetDirectory 'app.ico'))
$image.Save($stream)
$stream.Dispose()
$graphics.Dispose()
$font.Dispose()
$pen.Dispose()
$image.Dispose()
$bitmap.Dispose()
