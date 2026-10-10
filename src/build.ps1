$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $PSScriptRoot

$appName = -join ([char[]](0x4E0B, 0x73ED, 0x4E86))
$publishDir = Join-Path $root "dist"
$buildDir = Join-Path $PSScriptRoot "bin\Release\net48"
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $isccCandidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    $iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $iscc) { throw "Install Inno Setup 6, then run build.cmd again." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Install the .NET SDK, then run build.cmd again."
}

dotnet build ".\WorkTime.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
$exeName = $appName + ".exe"
foreach ($fileName in @($exeName, ($exeName + ".config"))) {
    Copy-Item -LiteralPath (Join-Path $buildDir $fileName) -Destination (Join-Path $root $fileName) -Force
}
& $iscc (Join-Path $PSScriptRoot "installer.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer creation failed with exit code $LASTEXITCODE" }

$exe = Join-Path $root $exeName
$setup = Join-Path $publishDir ($appName + "-Setup.exe")
if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath $setup)) {
    throw "Build completed without producing the app and installer."
}
Write-Host "Build completed successfully."
Write-Host "App size: $((Get-Item -LiteralPath $exe).Length) bytes"
Write-Host "Installer size: $((Get-Item -LiteralPath $setup).Length) bytes"
