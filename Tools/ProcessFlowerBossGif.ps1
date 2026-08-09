$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$gifPath = $args[0]
if ([string]::IsNullOrWhiteSpace($gifPath)) {
    $gifPath = Get-ChildItem (Join-Path $env:USERPROFILE 'OneDrive') -Recurse -Filter 'openart-video_953b8730_1783768286505.gif' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

$bossRoot = Split-Path $gifPath -Parent
$desktopOut = Join-Path $bossRoot 'FlowerBoss_OpenClose_Sprites_v2'
$unityOut = 'C:\Users\Bensh\Projects\Castlevania2DPrototype\Assets\Art\Sprites\Characters\Enemies\Flower Boss\Idle'

$whiteThreshold = 235
$blackThreshold = 35
$feather = 18
$alphaCutoff = 12
$canvasW = 256
$canvasH = 320
$padding = 8

function Convert-ToBitmap32([System.Drawing.Image]$src) {
    $bmp = New-Object System.Drawing.Bitmap($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
    $g.Dispose()
    return $bmp
}

function Remove-Background([System.Drawing.Bitmap]$src) {
    $result = New-Object System.Drawing.Bitmap($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $src.Height; $y++) {
        for ($x = 0; $x -lt $src.Width; $x++) {
            $pixel = $src.GetPixel($x, $y)
            $r = [int]$pixel.R
            $g = [int]$pixel.G
            $b = [int]$pixel.B
            $brightness = ($r + $g + $b) / 3.0
            $maxVal = [Math]::Max($r, [Math]::Max($g, $b))
            $minVal = [Math]::Min($r, [Math]::Min($g, $b))
            $alpha = 255

            if ($maxVal -le $blackThreshold) {
                $alpha = 0
            }
            elseif ($g -ge 90 -and $g -gt ($r + 25) -and $g -gt ($b + 25)) {
                $alpha = 0
            }
            elseif ($g -ge 70 -and $g -gt ($r * 1.15) -and $g -gt ($b * 1.15) -and $brightness -gt 80) {
                $alpha = 0
            }
            elseif ($minVal -ge $whiteThreshold) {
                $alpha = 0
            }
            elseif ($brightness -ge ($whiteThreshold - $feather)) {
                $t = ($whiteThreshold - $brightness) / [double]$feather
                $alpha = [int]([Math]::Min(255, [Math]::Max(0, (1.0 - $t) * 255)))
            }
            elseif ($maxVal -le ($blackThreshold + $feather)) {
                $t = ($maxVal - $blackThreshold) / [double]$feather
                $alpha = [int]([Math]::Min(255, [Math]::Max(0, $t * 255)))
            }
            elseif ($maxVal - $minVal -lt 18 -and $brightness -gt 190) {
                $alpha = [int]([Math]::Max(0, (255 - $brightness) * 2))
            }

            $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $r, $g, $b))
        }
    }
    return $result
}

function Get-ContentBounds([System.Drawing.Bitmap]$bmp) {
    $minX = $bmp.Width
    $minY = $bmp.Height
    $maxX = 0
    $maxY = 0

    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            if ($bmp.GetPixel($x, $y).A -gt $alphaCutoff) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt $minX -or $maxY -lt $minY) {
        return @{ X = 0; Y = 0; W = $bmp.Width; H = $bmp.Height }
    }

    return @{ X = $minX; Y = $minY; W = ($maxX - $minX + 1); H = ($maxY - $minY + 1) }
}

function Trim-BottomWhiteArtifacts([System.Drawing.Bitmap]$bmp) {
    $maxY = $bmp.Height - 1
    for ($y = $bmp.Height - 1; $y -ge 0; $y--) {
        $opaque = 0
        $whiteish = 0
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $p = $bmp.GetPixel($x, $y)
            if ($p.A -gt $alphaCutoff) {
                $opaque++
                if ($p.R -gt 200 -and $p.G -gt 200 -and $p.B -gt 200) { $whiteish++ }
            }
        }

        if ($opaque -eq 0) { continue }
        if ($whiteish -ge [int]($opaque * 0.75)) { continue }

        $maxY = $y
        break
    }

    if ($maxY -ge ($bmp.Height - 1)) { return $bmp }

    $trimmed = New-Object System.Drawing.Bitmap($bmp.Width, ($maxY + 1), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($trimmed)
    $g.DrawImage($bmp, 0, 0, $bmp.Width, ($maxY + 1))
    $g.Dispose()
    $bmp.Dispose()
    return $trimmed
}

function Crop-Bitmap([System.Drawing.Bitmap]$src, $bounds) {
    $crop = New-Object System.Drawing.Bitmap($bounds.W, $bounds.H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.DrawImage(
        $src,
        (New-Object System.Drawing.Rectangle(0, 0, $bounds.W, $bounds.H)),
        (New-Object System.Drawing.Rectangle($bounds.X, $bounds.Y, $bounds.W, $bounds.H)),
        [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return $crop
}

function Save-CanvasFrame([System.Drawing.Bitmap]$crop, [int]$targetContentH, [string]$path) {
    $scale = $targetContentH / [double]$crop.Height
    $targetW = [int][Math]::Round($crop.Width * $scale)
    $targetH = $targetContentH

    $canvas = New-Object System.Drawing.Bitmap($canvasW, $canvasH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $destX = [int](($canvasW - $targetW) / 2)
    $destY = $canvasH - $padding - $targetH
    $g.DrawImage($crop, $destX, $destY, $targetW, $targetH)
    $g.Dispose()
    $canvas.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}

if (-not (Test-Path $gifPath)) {
    throw "GIF not found: $gifPath"
}

if (Test-Path $desktopOut) {
    Remove-Item $desktopOut -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $desktopOut | Out-Null
New-Item -ItemType Directory -Force -Path $unityOut | Out-Null

$gif = [System.Drawing.Image]::FromFile($gifPath)
$dimension = New-Object System.Drawing.Imaging.FrameDimension($gif.FrameDimensionsList[0])
$totalFrames = $gif.GetFrameCount($dimension)
Write-Output "GIF frames: $totalFrames ($($gif.Width)x$($gif.Height))"

$rawCrops = New-Object System.Collections.Generic.List[object]
for ($frameIndex = 0; $frameIndex -lt $totalFrames; $frameIndex++) {
    $gif.SelectActiveFrame($dimension, $frameIndex) | Out-Null
    $frame32 = Convert-ToBitmap32 $gif
    $noBg = Remove-Background $frame32
    $trimmed = Trim-BottomWhiteArtifacts $noBg
    $bounds = Get-ContentBounds $trimmed
    $crop = Crop-Bitmap $trimmed $bounds
    $rawCrops.Add([PSCustomObject]@{ Index = $frameIndex; Crop = $crop; H = $bounds.H })
    $frame32.Dispose()
    if ($trimmed -ne $noBg) { $noBg.Dispose() } else { $noBg.Dispose() }

    if (($frameIndex + 1) % 10 -eq 0) {
        Write-Output "Processed $($frameIndex + 1)/$totalFrames source frames"
    }
}
$gif.Dispose()

$targetContentH = ($rawCrops | ForEach-Object { $_.H } | Measure-Object -Maximum).Maximum
$targetContentH = [int][Math]::Min($targetContentH, $canvasH - ($padding * 2))
Write-Output "Unified content height: $targetContentH"

$sequence = New-Object System.Collections.Generic.List[int]
for ($i = 0; $i -lt $totalFrames; $i++) { [void]$sequence.Add($i) }
for ($i = $totalFrames - 2; $i -ge 0; $i--) { [void]$sequence.Add($i) }

$exportIndex = 1
$mapLines = @("GIF: $gifPath", "Source frames: $totalFrames", "Export frames: $($sequence.Count)", "Sequence: forward 0..$($totalFrames - 1), reverse $($totalFrames - 2)..0", "")

foreach ($srcIndex in $sequence) {
    $item = $rawCrops[$srcIndex]
    $name = 'FlowerBoss_Idle_{0:D3}.png' -f $exportIndex
    $desktopPath = Join-Path $desktopOut $name
    Save-CanvasFrame $item.Crop $targetContentH $desktopPath
    $mapLines += "$name <= GIF frame $srcIndex"
    $exportIndex++
}

foreach ($item in $rawCrops) { $item.Crop.Dispose() }
$mapLines | Set-Content (Join-Path $desktopOut 'frame_map.txt') -Encoding UTF8

Write-Output "Desktop export complete: $($sequence.Count) frames -> $desktopOut"

# Replace Unity sprites
Get-ChildItem $unityOut -Filter '*.png' -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $unityOut -Filter '*.png.meta' -ErrorAction SilentlyContinue | Remove-Item -Force
if (Test-Path (Join-Path $unityOut 'guid_map.csv')) { Remove-Item (Join-Path $unityOut 'guid_map.csv') -Force }

$metaTemplate = @'
fileFormatVersion: 2
guid: {0}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 21300000
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'@

$guidRows = @()
$exported = Get-ChildItem $desktopOut -Filter 'FlowerBoss_Idle_*.png' | Sort-Object Name
foreach ($file in $exported) {
    $guid = [guid]::NewGuid().ToString('N')
    Copy-Item $file.FullName (Join-Path $unityOut $file.Name) -Force
    ($metaTemplate -f $guid) | Set-Content (Join-Path $unityOut ($file.Name + '.meta')) -Encoding UTF8
    $guidRows += [PSCustomObject]@{ Name = $file.Name; Guid = $guid }
}

$guidRows | Export-Csv (Join-Path $unityOut 'guid_map.csv') -NoTypeInformation -Encoding UTF8
Write-Output "Unity sprites replaced: $($guidRows.Count) files in $unityOut"
