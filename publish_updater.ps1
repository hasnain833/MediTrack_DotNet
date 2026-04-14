#Requires -Version 5.1
<#
.SYNOPSIS
    Builds DChemistUpdater as a self-contained, single-file Windows executable
    and copies it to the D.Chemist app root as 'updater.exe'.

.NOTES
    Run from the D.Chemist solution root (where this script lives).
    Requires .NET 8 SDK installed on the BUILD machine (not the target machine).
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Paths ──────────────────────────────────────────────────────────────────────
$ScriptDir    = $PSScriptRoot
$UpdaterProj  = Join-Path $ScriptDir "DChemistUpdater\DChemistUpdater\DChemistUpdater.csproj"
$PublishOut   = Join-Path $ScriptDir "DChemistUpdater\DChemistUpdater\bin\Publish"
$DestExe      = Join-Path $ScriptDir "updater.exe"
$DestPdb      = Join-Path $ScriptDir "updater.pdb"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  D.Chemist Updater — Self-Contained Publish Script" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ── Verify project exists ──────────────────────────────────────────────────────
if (-not (Test-Path $UpdaterProj)) {
    Write-Host "[ERROR] Project not found: $UpdaterProj" -ForegroundColor Red
    exit 1
}

# ── Clean previous publish output ──────────────────────────────────────────────
if (Test-Path $PublishOut) {
    Write-Host "[1/4] Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item $PublishOut -Recurse -Force
}

# ── Publish (self-contained, single-file, win-x64, Release) ───────────────────
Write-Host "[2/4] Publishing updater (self-contained, single-file, win-x64)..." -ForegroundColor Yellow
Write-Host ""

dotnet publish $UpdaterProj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishTrimmed=true `
    /p:TrimMode=partial `
    --output $PublishOut

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] dotnet publish failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "[OK] Publish succeeded." -ForegroundColor Green

# ── Locate the built exe ───────────────────────────────────────────────────────
$BuiltExe = Join-Path $PublishOut "updater.exe"
$BuiltPdb = Join-Path $PublishOut "updater.pdb"

if (-not (Test-Path $BuiltExe)) {
    Write-Host "[ERROR] Expected output not found: $BuiltExe" -ForegroundColor Red
    exit 1
}

# ── Copy to app root ───────────────────────────────────────────────────────────
Write-Host "[3/4] Copying updater.exe to app root..." -ForegroundColor Yellow

Copy-Item $BuiltExe -Destination $DestExe -Force
Write-Host "  -> $DestExe  ($([math]::Round((Get-Item $DestExe).Length / 1MB, 1)) MB)" -ForegroundColor Gray

if (Test-Path $BuiltPdb) {
    Copy-Item $BuiltPdb -Destination $DestPdb -Force
    Write-Host "  -> $DestPdb" -ForegroundColor Gray
}

# ── Done ───────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[4/4] Done." -ForegroundColor Green
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  updater.exe is ready in the app root." -ForegroundColor Cyan
Write-Host "  File: $DestExe" -ForegroundColor Cyan
Write-Host "  Size: $([math]::Round((Get-Item $DestExe).Length / 1MB, 1)) MB" -ForegroundColor Cyan
Write-Host "  Self-contained: NO .NET runtime required on target machine." -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
