param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe',
    [switch]$SceneOnly,
    [switch]$ChecksOnly
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$unityRoot = Split-Path -Parent $project
$evidence = Join-Path $project 'Builds\Evidence'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
if (-not $env:ALLUSERSPROFILE) { $env:ALLUSERSPROFILE = $env:ProgramData }
$env:ANDROID_HOME = Join-Path $unityRoot 'toolchain\android-sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$method = if ($ChecksOnly) { 'SwanAtelier.Editor.SwanAtelierChecks.Run' } elseif ($SceneOnly) { 'SwanAtelier.Editor.SwanAtelierBuild.Generate' } else { 'SwanAtelier.Editor.SwanAtelierBuild.BuildAndroid' }
$log = Join-Path $evidence $(if ($ChecksOnly) { 'unity-checks.log' } else { 'unity-build.log' })
$quit = if ($ChecksOnly) { '' } else { '-quit' }
$arguments = "-batchmode $quit -projectPath `"$project`" -buildTarget Android -executeMethod $method -logFile `"$log`""
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($process.Id) log=$log"
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    Select-String -LiteralPath $log -Pattern 'error CS\d+|Exception:|ATELIER_SWAN_|SWAN_ATELIER_|BuildFailedException|License|Aborting batchmode' | Select-Object -Last 20 | ForEach-Object Line
    throw "Unity failed with exit code $($process.ExitCode). See $log"
}
Select-String -LiteralPath $log -Pattern 'ATELIER_SWAN_|SWAN_ATELIER_|Build completed' | Select-Object -Last 12 | ForEach-Object Line
