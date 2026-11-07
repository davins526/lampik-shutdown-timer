$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) {
    throw 'This test requires a disposable GitHub Actions runner.'
}

$installDir = Join-Path $env:RUNNER_TEMP 'Lampik install smoke'
$oldSetup = Join-Path $env:RUNNER_TEMP 'Lampik-beta2.exe'
$settings = Join-Path $env:LOCALAPPDATA 'Lampik/settings.json'
if (Test-Path (Split-Path $settings)) { throw 'Lampik profile already exists on the runner.' }

function Invoke-Installer([string] $File, [string[]] $Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(90000)) { $process.Kill(); throw "Installer timed out: $File" }
    if ($process.ExitCode -ne 0) { throw "Installer exited with $($process.ExitCode): $File" }
}

$flags = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=`"$installDir`"")
Invoke-WebRequest -Uri 'https://github.com/davins526/lampik-shutdown-timer/releases/download/v0.7.0-beta.2/Lampik-Setup-0.7.0-beta.2-win-x64.exe' -OutFile $oldSetup
Invoke-Installer $oldSetup $flags
$installedExe = Join-Path $installDir 'Lampik.exe'
if (-not (Test-Path $installedExe)) { throw 'Baseline installation failed.' }

New-Item -ItemType Directory -Path (Split-Path $settings) | Out-Null
'{"Language":"en","DarkTheme":false,"PetScale":0.6,"StartWithWindows":false}' | Set-Content $settings
$before = Get-Content $settings -Raw
$setup = Get-Item 'release-installer/Lampik-Setup-1.0.0-rc.1-win-x64.exe'
Invoke-Installer $setup.FullName $flags
if ((Get-Item $installedExe).VersionInfo.FileMajorPart -ne 1) { throw 'Upgrade did not install version 1.' }
if ((Get-Content $settings -Raw) -ne $before) { throw 'Upgrade modified the profile.' }

$first = Start-Process -FilePath $installedExe -WindowStyle Hidden -PassThru
try {
    if ($first.WaitForExit(4000)) { throw 'Installed app exited at startup.' }
    $portable = (Resolve-Path 'release/Lampik.exe').Path
    $second = Start-Process -FilePath $portable -WindowStyle Hidden -PassThru
    if (-not $second.WaitForExit(15000)) { $second.Kill(); throw 'A second instance stayed running.' }
    if ($second.ExitCode -ne 0 -or $first.HasExited) { throw 'Repeat launch failed.' }
} finally {
    if (-not $first.HasExited) { $first.Kill(); $first.WaitForExit() }
}

$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ((Test-Path $startupKey) -and ((Get-Item $startupKey).GetValueNames() -contains 'Lampik')) {
    throw 'Unexpected existing Lampik startup entry on runner.'
}
if (-not (Test-Path $startupKey)) { New-Item -Path $startupKey | Out-Null }
Set-ItemProperty -Path $startupKey -Name Lampik -Value ('"{0}" --tray' -f $installedExe)
$uninstaller = @(Get-ChildItem -LiteralPath $installDir -Filter 'unins*.exe')
if ($uninstaller.Count -ne 1) { throw 'Expected one uninstaller after upgrade.' }
Invoke-Installer $uninstaller[0].FullName @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
$deadline = (Get-Date).AddSeconds(15)
while ((Test-Path $installedExe) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
if (Test-Path $installedExe) { throw 'Uninstall left the application executable.' }
if ((Test-Path $startupKey) -and ((Get-Item $startupKey).GetValueNames() -contains 'Lampik')) {
    throw 'Uninstall left the installed app startup entry.'
}
if ((Get-Content $settings -Raw) -ne $before) { throw 'Uninstall modified the profile.' }
Write-Host 'Upgrade, shared single instance and uninstall passed.'
