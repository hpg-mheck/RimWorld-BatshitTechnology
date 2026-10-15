# Tiles a run's frames into contact sheets, so a whole tour can be looked
# over in a few images instead of fifty. The frames are the PNGs a verify
# run saves with -Shots (Tools\smoke-test.ps1, NN-<scene>-<cue>.png in
# <data folder>\shots); any folder of PNGs will do. Each tile is the
# frame scaled to -TileWidth, under a band labelled with its file name.
# Uses .NET's System.Drawing (Windows), so it needs no Python imaging
# library; runs under PowerShell 7 and Windows PowerShell 5.1.
#
# Usage (PowerShell):
#   .\Tools\contact-sheet.ps1 -Path <data folder or shots folder>
#                                   # one sheet per scene, in frame order:
#                                   # sheets\NN-<scene>.png beside the
#                                   # shots folder; READY's frame opens
#                                   # the first scene's sheet
#   .\Tools\contact-sheet.ps1 -Path <folder> -PerSheet 12
#                                   # a fixed number of frames per sheet
#                                   # instead: sheets\sheet-NN.png
#   -Columns 3 -TileWidth 480       # the layout (the defaults)
#   -OutDir <folder>                # where the sheets go
# Prints each sheet's path and the frames on it. Exits 1 if the folder
# holds no PNG.
param(
  [Parameter(Mandatory = $true)][string]$Path,
  [int]$PerSheet = 0,
  [int]$Columns = 3,
  [int]$TileWidth = 480,
  [string]$OutDir = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Absolute paths from the current folder (.NET resolves relative paths
# against the process's own folder, which PowerShell does not move).
$fullPath = {
  param($p)
  if ($p) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p) }
  else { $p }
}
$Path = & $fullPath $Path
if (-not (Test-Path $Path -PathType Container)) { throw "No folder at '$Path'." }
if ($PerSheet -lt 0) { throw '-PerSheet must be 0 (one sheet per scene) or more.' }
if ($Columns -lt 1) { throw '-Columns must be at least 1.' }
if ($TileWidth -lt 32) { throw '-TileWidth must be at least 32 pixels.' }

# A data folder holds its frames in shots\.
$shots = $Path
$nested = Join-Path $Path 'shots'
if (-not (Get-ChildItem $Path -Filter '*.png' -File) -and (Test-Path $nested -PathType Container)) {
  $shots = $nested
}
$frames = @(Get-ChildItem $shots -Filter '*.png' -File | Sort-Object Name)
if ($frames.Count -eq 0) {
  Write-Error "No PNG frames in '$shots'."
  exit 1
}
if (-not $OutDir) { $OutDir = Join-Path (Split-Path $shots -Parent) 'sheets' }
$OutDir = & $fullPath $OutDir
New-Item -ItemType Directory -Force $OutDir | Out-Null

# Gives a frame's scene from its name, NN-<scene>-<cue>: the cue is
# "scene", "ready", or "beat-..."/"end-..."; a scene key may hold hyphens.
# Any other name is its own "frames" group.
function Get-FrameScene([string]$name) {
  if ($name -match '^\d+-(.+?)-(scene|ready|beat-.+|end-.+)$') { return $Matches[1] }
  return 'frames'
}

# Groups the frames into sheets: by scene (READY's "tour" frame joins the
# next scene), or a fixed count each.
$sheets = New-Object System.Collections.Generic.List[object]
if ($PerSheet -gt 0) {
  for ($i = 0; $i -lt $frames.Count; $i += $PerSheet) {
    $last = [Math]::Min($i + $PerSheet, $frames.Count) - 1
    $sheets.Add([pscustomobject]@{
      Name = 'sheet-{0:00}' -f ($sheets.Count + 1)
      Frames = @($frames[$i..$last])
    })
  }
} else {
  $carry = @()
  $current = $null
  foreach ($frame in $frames) {
    $scene = Get-FrameScene $frame.BaseName
    if ($scene -eq 'tour') { $carry += $frame; continue }
    if (-not $current -or $current.Scene -ne $scene) {
      $current = [pscustomobject]@{
        Name = '{0:00}-{1}' -f ($sheets.Count + 1), $scene
        Scene = $scene
        Frames = @()
      }
      $sheets.Add($current)
    }
    $current.Frames += $carry
    $carry = @()
    $current.Frames += $frame
  }
  if ($carry.Count -gt 0) {
    $sheets.Add([pscustomobject]@{
      Name = '{0:00}-tour' -f ($sheets.Count + 1); Scene = 'tour'; Frames = $carry
    })
  }
}

# Draws one sheet: tiles left to right, top to bottom, each frame scaled
# to the tile width under its label band.
function Write-Sheet($sheet, [string]$file) {
  $gap = 8
  $band = 24
  $images = @($sheet.Frames | % { [System.Drawing.Image]::FromFile($_.FullName) })
  try {
    $cols = [Math]::Min($Columns, $images.Count)
    $rows = [int][Math]::Ceiling($images.Count / $cols)
    $tileHeight = 0
    foreach ($img in $images) {
      $h = [int][Math]::Round($img.Height * $TileWidth / [double]$img.Width)
      $tileHeight = [Math]::Max($tileHeight, $h)
    }
    $width = $cols * $TileWidth + ($cols + 1) * $gap
    $height = $rows * ($tileHeight + $band) + ($rows + 1) * $gap
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $font = New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold,
      [System.Drawing.GraphicsUnit]::Pixel)
    try {
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
      $g.Clear([System.Drawing.Color]::FromArgb(24, 24, 24))
      for ($i = 0; $i -lt $images.Count; $i++) {
        $img = $images[$i]
        $x = $gap + ($i % $cols) * ($TileWidth + $gap)
        $y = $gap + [Math]::Floor($i / $cols) * ($tileHeight + $band + $gap)
        $h = [int][Math]::Round($img.Height * $TileWidth / [double]$img.Width)
        $g.FillRectangle([System.Drawing.Brushes]::Black, $x, $y, $TileWidth, $band)
        # Sums first: in an argument list, a comma binds before a plus.
        $lx = [float]($x + 4)
        $ly = [float]($y + 3)
        $lw = [float]($TileWidth - 8)
        $lh = [float]($band - 4)
        $label = New-Object System.Drawing.RectangleF($lx, $ly, $lw, $lh)
        $g.DrawString($sheet.Frames[$i].BaseName, $font, [System.Drawing.Brushes]::White, $label)
        $g.DrawImage($img, [int]$x, [int]($y + $band), [int]$TileWidth, [int]$h)
      }
      $bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
      $font.Dispose()
      $g.Dispose()
      $bitmap.Dispose()
    }
  } finally {
    $images | % { $_.Dispose() }
  }
}

"Frames: $($frames.Count) in $shots"
foreach ($sheet in $sheets) {
  $file = Join-Path $OutDir "$($sheet.Name).png"
  Write-Sheet $sheet $file
  "Sheet: $file ($(@($sheet.Frames).Count) frames)"
  $sheet.Frames | % { "  $($_.BaseName)" }
}
exit 0
