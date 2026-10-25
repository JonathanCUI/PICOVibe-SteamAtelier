param(
    [string]$Apk = 'Builds/SwanAtelier/SteamAtelier-Swan.apk',
    [double]$ExpectedAlpha = 0.34,
    [string]$Output = 'Builds/Evidence/v043-packaged-ghosts.json'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Apk))
try {
    $entries = @($archive.Entries | Where-Object { $_.FullName -match '/Ghost_\d\d\.prefab\.glb$' })
    if ($entries.Count -ne 45) { throw "Expected 45 packaged placement ghost GLBs, found $($entries.Count)" }
    $results = foreach ($entry in $entries) {
        $stream = $entry.Open()
        $reader = [System.IO.BinaryReader]::new($stream)
        try {
            if ($reader.ReadUInt32() -ne 0x46546C67 -or $reader.ReadUInt32() -ne 2) { throw "Invalid GLB: $($entry.FullName)" }
            $null = $reader.ReadUInt32()
            $length = $reader.ReadUInt32()
            if ($reader.ReadUInt32() -ne 0x4E4F534A) { throw 'First GLB chunk is not JSON' }
            $gltf = [System.Text.Encoding]::UTF8.GetString($reader.ReadBytes($length)) | ConvertFrom-Json
            $materials = @($gltf.materials)
            if ($materials.Count -eq 0) { throw "Ghost has no material: $($entry.FullName)" }
            foreach ($mat in $materials) {
                $alpha = $mat.pbrMetallicRoughness.baseColorFactor[3]
                if ($mat.alphaMode -ne 'BLEND' -or [math]::Abs($alpha - $ExpectedAlpha) -gt 0.001) {
                    throw "Opaque/incorrect ghost material in $($entry.FullName): mode=$($mat.alphaMode) alpha=$alpha"
                }
                if (@($mat.pbrMetallicRoughness.baseColorFactor[0..2] | Where-Object { [math]::Abs($_ - 1) -gt 0.001 }).Count -ne 0) {
                    throw "Ghost material must be neutral white in $($entry.FullName)"
                }
            }
            [pscustomobject]@{
                path = $entry.FullName; bytes = $entry.Length; meshes = @($gltf.meshes).Count
                materials = @($materials | ForEach-Object { [pscustomobject]@{ name=$_.name; alphaMode=$_.alphaMode; baseColorFactor=$_.pbrMetallicRoughness.baseColorFactor } })
            }
        } finally { $reader.Dispose(); $stream.Dispose() }
    }
    [pscustomobject]@{ passed=$true; ghostCount=$entries.Count; glbs=$results } |
        ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $Output -Encoding UTF8
    "Verified $($entries.Count) APK ghost GLBs: alphaMode=BLEND, alpha=$ExpectedAlpha."
} finally { $archive.Dispose() }
