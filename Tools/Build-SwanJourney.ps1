param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe',
    [switch]$SceneOnly,
    [switch]$InspectOnly,
    [switch]$ChecksOnly,
    [switch]$FlowChecksOnly,
    [switch]$UiChecksOnly,
    [switch]$DeferredChecksOnly,
    [switch]$HiddenChecksOnly,
    [switch]$RenderPathChecksOnly,
    [switch]$DevelopmentBuild
)
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path -Parent $PSScriptRoot
$taskEvidence = Join-Path $taskProject 'Builds\Evidence'
New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
if (-not $env:ALLUSERSPROFILE) { $env:ALLUSERSPROFILE = $env:ProgramData }
$env:ANDROID_HOME = Join-Path (Split-Path -Parent $taskProject) 'toolchain\android-sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$taskMethod = if ($RenderPathChecksOnly) { 'SwanJourney.Editor.SwanJourneyRenderPathChecks.Run' } elseif ($HiddenChecksOnly) { 'SwanJourney.Editor.SwanJourneyHiddenUpdateChecks.Run' } elseif ($DeferredChecksOnly) { 'SwanJourney.Editor.SwanJourneyDeferredChecks.Run' } elseif ($UiChecksOnly) { 'SwanJourney.Editor.SwanJourneyUiChecks.Run' } elseif ($FlowChecksOnly) { 'SwanJourney.Editor.SwanJourneyFlowChecks.Run' } elseif ($ChecksOnly) { 'SwanJourney.Editor.SwanJourneyChecks.Run' } elseif ($InspectOnly) { 'SwanJourney.Editor.SwanJourneyProbe.Inspect' } elseif ($SceneOnly) { 'SwanJourney.Editor.SwanJourneyBuild.Generate' } else { 'SwanJourney.Editor.SwanJourneyBuild.BuildAndroid' }
$taskLog = Join-Path $taskEvidence $(if ($RenderPathChecksOnly) { 'unity-v23-render-path-checks.log' } elseif ($HiddenChecksOnly) { 'unity-v23-hidden-checks.log' } elseif ($DeferredChecksOnly) { 'unity-v23-deferred-checks.log' } elseif ($UiChecksOnly) { 'unity-v23-ui-checks.log' } elseif ($FlowChecksOnly) { 'unity-v23-flow-checks.log' } elseif ($ChecksOnly) { 'unity-v23-checks.log' } elseif ($InspectOnly) { 'unity-v23-inspect.log' } else { 'unity-v23-build.log' })
if ($DevelopmentBuild -and $taskMethod -eq 'SwanJourney.Editor.SwanJourneyBuild.BuildAndroid') { $taskMethod = 'SwanJourney.Editor.SwanJourneyBuild.BuildAndroidDevelopment' }
$taskQuit = if ($ChecksOnly) { '' } else { '-quit' }
$taskArguments = "-batchmode $taskQuit -projectPath `"$taskProject`" -buildTarget Android -executeMethod $taskMethod -logFile `"$taskLog`""
$taskProcess = Start-Process -FilePath $EditorPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID=$($taskProcess.Id) log=$taskLog"
$taskProcess.WaitForExit()
Select-String -LiteralPath $taskLog -Pattern 'error CS\d+|Exception:|SWAN_JOURNEY_|BuildFailedException|Aborting batchmode|No valid Unity Editor license' | Select-Object -Last 25 | ForEach-Object Line
if ($taskProcess.ExitCode -ne 0) { throw "Unity failed with exit code $($taskProcess.ExitCode). See $taskLog" }
