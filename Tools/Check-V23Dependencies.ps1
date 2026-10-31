param()
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path -Parent $PSScriptRoot
$taskPackages = Join-Path $taskProject 'Packages'
$taskDependencies = Get-Content (Join-Path $taskProject 'Docs/Local-Dependencies.json') -Raw | ConvertFrom-Json
$taskProblems = @()
foreach ($taskDependency in $taskDependencies) {
    $taskRelative = $taskDependency.manifestPath -replace '^file:', ''
    $taskPackage = [IO.Path]::GetFullPath((Join-Path $taskPackages $taskRelative))
    $taskManifest = Join-Path $taskPackage 'package.json'
    if (-not (Test-Path -LiteralPath $taskManifest)) {
        $taskProblems += "Missing $($taskDependency.name): $taskManifest"
        continue
    }
    $taskActual = Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
    $taskHash = (Get-FileHash -LiteralPath $taskManifest -Algorithm SHA256).Hash
    if ($taskActual.version -ne $taskDependency.version -or $taskActual.name -ne $taskDependency.name -or $taskHash -ne $taskDependency.packageJsonSha256) {
        $taskProblems += "Package metadata differs: $($taskDependency.name), expected $($taskDependency.version), found $($taskActual.version)."
    } else { Write-Output "OK $($taskDependency.name) $($taskActual.version)" }
}
$taskLfs = & git -C $taskProject lfs ls-files 2>&1
if ($LASTEXITCODE -ne 0) { $taskProblems += 'Git LFS unavailable. Install Git LFS and run git lfs pull.' }
else {
    foreach ($taskLine in $taskLfs) {
        if ($taskLine -match '^[a-f0-9]+ - ') { $taskProblems += "LFS content not downloaded: $taskLine" }
    }
}
if ($taskProblems.Count) {
    $taskProblems | ForEach-Object { Write-Warning $_ }
    throw 'Dependency checks failed. Restore the exact local packages / LFS files before opening Unity.'
}
Write-Output 'Package metadata and LFS checkout checks passed. Unity import/build and Android toolchain are separate checks.'
