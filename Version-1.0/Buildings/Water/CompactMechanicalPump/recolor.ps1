<#
.SYNOPSIS
    Decompresses, dynamically recolors, and recompresses a Timberborn .timbermesh file.
.PARAMETER Path
    The path to the source .timbermesh file.
#>
param (
    [Parameter(Mandatory=$true)][string]$Path
)

# --- HARDCODED COLOR CONFIGURATION ---
# Edit these floats (0.0 to 1.0) to change the "PaintedMetal" color
[float]$R = 0.585
[float]$G = 0.455
[float]$B = 0.364
[float]$A = 0.8
# -------------------------------------

$literalPath = Resolve-Path $Path -ErrorAction SilentlyContinue
if (-not $literalPath) { Write-Error "File not found."; return }

$outputFile = Join-Path (Split-Path $literalPath.Path) "Recolored.$([System.IO.Path]::GetFileName($literalPath.Path))"

Write-Host "Reading binary asset..." -ForegroundColor Cyan
$bytes = [System.IO.File]::ReadAllBytes($literalPath.Path)

# 1. Zlib Decompression
$skipBytes = if ($bytes[0] -eq 0x78) { 2 } else { 0 }
$msInput = [System.IO.MemoryStream]::new($bytes, $skipBytes, $bytes.Length - $skipBytes)
$deflate = [System.IO.Compression.DeflateStream]::new($msInput, [System.IO.Compression.CompressionMode]::Decompress)
$msOutput = [System.IO.MemoryStream]::new()
$deflate.CopyTo($msOutput)
$protoBytes = $msOutput.ToArray()

# We need an array of objects to keep each Node's data isolated
$script:nodeMap = [System.Collections.Generic.List[psobject]]::new()
$script:currentNodeTargetIndices = [System.Collections.Generic.List[int]]::new()
$script:currentNodeColorOffset = -1

# 2. In-Place Stream Parser
function Read-Varint($stream) {
    $value = [uint64]0; $shift = 0
    while ($true) {
        $b = $stream.ReadByte()
        if ($b -lt 0) { return $null }
        $value = $value -bor ([uint64]($b -band 0x7F) -shl $shift)
        if (($b -band 0x80) -eq 0) { break }
        $shift += 7
    }
    return $value
}

function Parse-Message($stream, $endPosition, $messageType) {
    $localIndices = [System.Collections.Generic.List[int]]::new()
    $localMaterial = ""
    $vpName = ""
    
    while ($stream.Position -lt $endPosition) {
        $tag = Read-Varint $stream
        if ($null -eq $tag) { break }
        
        $wireType = $tag -band 0x7
        $fieldNum = $tag -shr 3
        
        if ($wireType -eq 0) {
            $null = Read-Varint $stream
        } elseif ($wireType -eq 1) {
            $stream.Position += 8
        } elseif ($wireType -eq 5) {
            $stream.Position += 4
        } elseif ($wireType -eq 2) {
            $len = Read-Varint $stream
            $fieldStart = $stream.Position
            $fieldEnd = $fieldStart + $len
            
            if ($messageType -eq "Model") {
                if ($fieldNum -eq 3) {
                    # Initialize isolation for a new Node
                    $script:currentNodeTargetIndices = [System.Collections.Generic.List[int]]::new()
                    $script:currentNodeColorOffset = -1
                    
                    Parse-Message $stream $fieldEnd "Node"
                    
                    # Store the mapped offsets for this specific Node
                    if ($script:currentNodeColorOffset -ne -1 -and $script:currentNodeTargetIndices.Count -gt 0) {
                        $script:nodeMap.Add([pscustomobject]@{
                            Indices = $script:currentNodeTargetIndices | Select-Object -Unique
                            ColorOffset = $script:currentNodeColorOffset
                        })
                    }
                }
            } elseif ($messageType -eq "Node") {
                if ($fieldNum -eq 7) { Parse-Message $stream $fieldEnd "VertexProperty" }
                elseif ($fieldNum -eq 8) { Parse-Message $stream $fieldEnd "Mesh" }
            } elseif ($messageType -eq "Mesh") {
                if ($fieldNum -eq 1) { # Packed Indices
                    while ($stream.Position -lt $fieldEnd) {
                        $localIndices.Add((Read-Varint $stream))
                    }
                } elseif ($fieldNum -eq 2) {
                    $buf = New-Object byte[] $len
                    [void]$stream.Read($buf, 0, $len)
                    $localMaterial = [System.Text.Encoding]::ASCII.GetString($buf)
                }
            } elseif ($messageType -eq "VertexProperty") {
                if ($fieldNum -eq 1) {
                    $buf = New-Object byte[] $len
                    [void]$stream.Read($buf, 0, $len)
                    $vpName = [System.Text.Encoding]::ASCII.GetString($buf)
                } elseif ($fieldNum -eq 4) {
                    if ($vpName -eq "color") {
                        $script:currentNodeColorOffset = $stream.Position
                    }
                }
            }
            $stream.Position = $fieldEnd
        }
    }
    
    # If this was a Mesh node and it was the painted metal, commit the indices to the current active Node
    if ($messageType -eq "Mesh" -and $localMaterial -match "PaintedMetal") {
        $script:currentNodeTargetIndices.AddRange($localIndices)
    }
}

Write-Host "Mapping protobuf byte offsets..." -ForegroundColor Yellow
$parseStream = [System.IO.MemoryStream]::new($protoBytes)
Parse-Message $parseStream $protoBytes.Length "Model"

if ($script:nodeMap.Count -eq 0) {
    Write-Error "Could not map color data or find PaintedMetal indices."
    return
}

# 3. Inject New Color Bytes (Safely, per-node)
$rBytes = [System.BitConverter]::GetBytes([float]$R)
$gBytes = [System.BitConverter]::GetBytes([float]$G)
$bBytes = [System.BitConverter]::GetBytes([float]$B)
$aBytes = [System.BitConverter]::GetBytes([float]$A)

$totalModifications = 0
foreach ($nodeData in $script:nodeMap) {
    foreach ($index in $nodeData.Indices) {
        # 4 floats (RGBA) * 4 bytes per float = 16 bytes per vertex
        $byteOffset = $nodeData.ColorOffset + ($index * 16) 
        
        [System.Array]::Copy($rBytes, 0, $protoBytes, $byteOffset, 4)
        [System.Array]::Copy($gBytes, 0, $protoBytes, $byteOffset + 4, 4)
        [System.Array]::Copy($bBytes, 0, $protoBytes, $byteOffset + 8, 4)
        [System.Array]::Copy($aBytes, 0, $protoBytes, $byteOffset + 12, 4)
        $totalModifications++
    }
}
Write-Host "Injected new color floats into $totalModifications unique vertices across $($script:nodeMap.Count) nodes." -ForegroundColor Green


# 4. Zlib Adler32 Checksum Algorithm (Safe from 32-bit sign overflow)
function Get-Adler32($data) {
    [long]$a = 1
    [long]$b = 0
    foreach ($byte in $data) {
        $a = ($a + $byte) % 65521
        $b = ($b + $a) % 65521
    }
    
    [uint32]$checksum = ([uint32]$b * 65536) + [uint32]$a
    $checksumBytes = [System.BitConverter]::GetBytes($checksum)
    
    if ([System.BitConverter]::IsLittleEndian) { [System.Array]::Reverse($checksumBytes) }
    return $checksumBytes
}

# 5. Recompress to standard Zlib Stream
Write-Host "Re-compressing to Zlib binary..." -ForegroundColor Cyan
$msCompress = [System.IO.MemoryStream]::new()
$msCompress.WriteByte(0x78) # Zlib Magic Header
$msCompress.WriteByte(0x9C) # Default Compression

$deflateOut = [System.IO.Compression.DeflateStream]::new($msCompress, [System.IO.Compression.CompressionMode]::Compress, $true)
$deflateOut.Write($protoBytes, 0, $protoBytes.Length)
$deflateOut.Close()

$checksum = Get-Adler32 $protoBytes
$msCompress.Write($checksum, 0, 4)

[System.IO.File]::WriteAllBytes($outputFile, $msCompress.ToArray())
Write-Host "Done! Saved to: $outputFile" -ForegroundColor Green