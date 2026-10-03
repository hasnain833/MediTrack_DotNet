# One-command release: bump version -> publish -> zip -> GitHub release -> push version.json.
# Usage:  .\release.ps1 1.9.0.0 "What changed in this release"
# Needs:  GitHub CLI (winget install GitHub.cli, then: gh auth login), a clean git tree.
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Notes
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not [version]::TryParse($Version, [ref]$null)) { throw "Version must look like 1.9.0.0" }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "GitHub CLI not found. Run: winget install GitHub.cli ; gh auth login" }
if (git status --porcelain) { throw "Commit or stash your changes first - a release must match what is in git." }
if (-not (Test-Path updater.exe)) { & .\publish_updater.ps1 }

$tag = "v$Version"
$zipName = "DChemist_${tag}_Release.zip"
$outDir = Join-Path $env:TEMP "dchemist_release"
$stage = Join-Path $outDir $tag
$zip = Join-Path $outDir $zipName

Write-Host "[1/5] Bumping version to $Version" -ForegroundColor Yellow
$csproj = Get-Content DChemist.csproj -Raw
$csproj = $csproj -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>" `
                  -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$Version</FileVersion>"
[IO.File]::WriteAllText("$PWD\DChemist.csproj", $csproj)  # UTF-8 without BOM

Write-Host "[2/5] Publishing" -ForegroundColor Yellow
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
dotnet publish DChemist.csproj -c Release -r win-x64 --self-contained -p:Platform=x64 -o $stage
if ($LASTEXITCODE -ne 0) { git checkout -- DChemist.csproj; throw "Publish failed." }

Write-Host "[3/5] Zipping" -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, 'Optimal', $true)
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash

Write-Host "[4/5] Uploading GitHub release $tag" -ForegroundColor Yellow
gh release create $tag $zip --title $tag --notes $Notes
if ($LASTEXITCODE -ne 0) { git checkout -- DChemist.csproj; throw "gh release failed." }

# version.json is pushed LAST: shop PCs only see the update once the zip is downloadable.
Write-Host "[5/5] Publishing version.json" -ForegroundColor Yellow
[ordered]@{
    LatestVersion = $Version
    DownloadUrl   = "https://github.com/hasnain833/MediTrack_DotNet/releases/download/$tag/$zipName"
    ReleaseNotes  = $Notes
    PackageSha256 = $sha
} | ConvertTo-Json | ForEach-Object { [IO.File]::WriteAllText("$PWD\version.json", $_) }

git add DChemist.csproj version.json
git commit -m "Release $tag"
git push origin HEAD
if ($LASTEXITCODE -ne 0) { throw "Push failed - release is on GitHub but shops won't see it until version.json is pushed." }

Write-Host "`nReleased $tag. Shop PCs will pick it up on next app start." -ForegroundColor Green
