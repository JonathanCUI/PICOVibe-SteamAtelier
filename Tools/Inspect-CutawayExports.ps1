param(
    [string]$Apk = 'Builds/SwanAtelier/SteamAtelier-Swan.apk',
    [string]$Output = 'Builds/Evidence/v043-packaged-cutaway.json'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Apk))
try {
    $entry = $archive.GetEntry('assets/SpatialAdapterExport/Assets/SwanAtelier/Scenes/SteamAtelierSwan.unity.glb')
    if (-not $entry) { throw 'Missing exported Swan scene' }
    $reader = [IO.BinaryReader]::new($entry.Open())
    try {
        if ($reader.ReadUInt32() -ne 0x46546C67 -or $reader.ReadUInt32() -ne 2) { throw 'Invalid scene GLB' }
        $null = $reader.ReadUInt32(); $length = $reader.ReadUInt32()
        if ($reader.ReadUInt32() -ne 0x4E4F534A) { throw 'Missing GLB JSON' }
        $gltf = [Text.Encoding]::UTF8.GetString($reader.ReadBytes($length)) | ConvertFrom-Json
    } finally { $reader.Dispose() }
    # Exported material names use asset filenames. Resolve materials through
    # the actual semantic scene nodes instead of relying on Unity display names.
    $nodes=@($gltf.nodes | Where-Object { $_.name -match '^(后端进汽示意|前端进汽示意|后端排汽示意|前端排汽示意|锅炉上部蒸汽空间|火箱顶部蒸汽空间|烟箱汇集烟气|锅炉有深度的水体|锅炉波纹水面)$' })
    if ($nodes.Count -ne 13) { throw "Expected 13 steam/water nodes, found $($nodes.Count)" }
    $items=@(foreach ($node in $nodes) {
        if ($null -eq $node.mesh) { throw "Cutaway node has no mesh: $($node.name)" }
        foreach ($primitive in $gltf.meshes[[int]$node.mesh].primitives) {
            if ($null -eq $primitive.material) { throw "Cutaway node has no material: $($node.name)" }
            $mat=$gltf.materials[[int]$primitive.material]
            $alpha=$mat.pbrMetallicRoughness.baseColorFactor[3]
            if ($mat.alphaMode -ne 'BLEND' -or $alpha -le 0 -or $alpha -gt .25) {
                throw "Invalid transparency: $($node.name), mode=$($mat.alphaMode), alpha=$alpha"
            }
            $count=$gltf.accessors[[int]$primitive.attributes.POSITION].count
            if ($node.name -ne '锅炉波纹水面' -and $count -le 100) {
                throw "Volume still has box-like geometry: $($node.name), positions=$count"
            }
            [pscustomobject]@{node=$node.name;material=$mat.name;alphaMode=$mat.alphaMode;alpha=$alpha;positions=$count}
        }
    })
    [pscustomobject]@{passed=$true;scene=$entry.FullName;materials=$items} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Output -Encoding UTF8
    "Verified $($items.Count) cutaway materials: transparent water/steam with rounded chamber geometry."
} finally { $archive.Dispose() }
