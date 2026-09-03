# Gera um ícone placeholder simples usando System.Drawing
param([string]$Output = ".\QuickStartup\Assets\app.ico")

Add-Type -AssemblyName System.Drawing

$size = 64
$bmp  = New-Object System.Drawing.Bitmap($size, $size)
$g    = [System.Drawing.Graphics]::FromImage($bmp)

# Fundo azul escuro
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 30, 46))
$g.FillRectangle($bgBrush, 0, 0, $size, $size)

# Letra "Q" em azul claro
$font  = New-Object System.Drawing.Font("Segoe UI", 38, [System.Drawing.FontStyle]::Bold)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(137, 180, 250))
$sf    = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("Q", $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $size, $size), $sf)

$g.Dispose()

# Salva como .ico (formato ICO simples de 64x64)
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$ms.Dispose()
$bmp.Dispose()

# Escreve o header ICO manualmente
$stream = New-Object System.IO.MemoryStream

function Write-UInt16([System.IO.BinaryWriter]$w, [int]$v) { $w.Write([uint16]$v) }
function Write-UInt32([System.IO.BinaryWriter]$w, [int]$v) { $w.Write([uint32]$v) }

$writer = New-Object System.IO.BinaryWriter($stream)
Write-UInt16 $writer 0      # Reserved
Write-UInt16 $writer 1      # Type: 1 = ICO
Write-UInt16 $writer 1      # Count: 1 imagem

# ICONDIRENTRY
$writer.Write([byte]0)          # Width  (0 = 256)
$writer.Write([byte]0)          # Height (0 = 256)
$writer.Write([byte]0)          # ColorCount
$writer.Write([byte]0)          # Reserved
Write-UInt16 $writer 1          # Planes
Write-UInt16 $writer 32         # BitCount
Write-UInt32 $writer $pngBytes.Length   # Size of image data
Write-UInt32 $writer 22         # Offset of image data (6 header + 16 entry)

$writer.Write($pngBytes)
$writer.Flush()

$dir = Split-Path $Output
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

[System.IO.File]::WriteAllBytes($Output, $stream.ToArray())
$stream.Dispose()

Write-Host "Icone gerado: $Output"
