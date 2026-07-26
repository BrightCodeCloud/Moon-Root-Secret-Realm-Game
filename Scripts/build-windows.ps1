param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Installer
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$godot = Join-Path $projectRoot 'tools\godot\editor\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'
$exportDirectory = Join-Path $projectRoot 'builds\windows'
$exportTempFile = Join-Path $exportDirectory 'Moonroot.tmp'

if (-not (Test-Path -LiteralPath $godot)) {
    throw "Godot .NET was not found at $godot"
}

New-Item -ItemType Directory -Path $exportDirectory -Force | Out-Null
if (Test-Path -LiteralPath $exportTempFile) {
    Remove-Item -LiteralPath $exportTempFile -Force
}

Push-Location $projectRoot
try {
    dotnet build 'Moonroot.sln' -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }

    & $godot --headless --path $projectRoot --export-release 'Windows Desktop' (Join-Path $exportDirectory 'Moonroot.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Godot export failed.' }
    if (Test-Path -LiteralPath $exportTempFile) {
        Remove-Item -LiteralPath $exportTempFile -Force
    }

    if ($Installer) {
        $isccCandidates = @(
            (Join-Path $projectRoot 'tools\inno\compiler\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
            'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
            'C:\Program Files\Inno Setup 7\ISCC.exe'
        )
        $iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $iscc) { throw 'Inno Setup 7 was not found. Install it before requesting -Installer.' }
        & $iscc (Join-Path $projectRoot 'installer\Moonroot.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    }
}
finally {
    Pop-Location
}
