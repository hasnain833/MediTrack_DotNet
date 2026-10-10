# Release from committed source; publish the updater manifest after the ZIP is available.
# Usage: .\release.ps1 1.9.6.0 "Release notes"
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Notes
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$parsedVersion = $null
if (-not [version]::TryParse($Version, [ref]$parsedVersion) -or $parsedVersion.Revision -lt 0) {
    throw 'Version must have four components, for example 1.9.6.0.'
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI is required.' }
$pendingChanges = git status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Cannot read git status.' }
if ($pendingChanges) { throw 'Commit your changes first so the release matches its source.' }
gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Sign in using gh auth login before releasing.' }
if (-not (Test-Path -LiteralPath 'updater.exe')) { & .\publish_updater.ps1 }

$repo = 'hasnain833/MediTrack_DotNet'
$tag = "v$Version"
$zipName = "DChemist_${tag}_Release.zip"
# Fresh staging avoids deleting an existing release or backup.
$releaseRoot = Join-Path $PSScriptRoot ('Publish\Releases\' + $tag + '_' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $releaseRoot $tag
$zip = Join-Path $releaseRoot $zipName
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

Write-Host '[1/5] Preparing version and source' -ForegroundColor Yellow
$csproj = Get-Content -LiteralPath 'DChemist.csproj' -Raw
$updatedCsproj = $csproj -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>" `
    -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$Version</FileVersion>"
if ($updatedCsproj -ne $csproj) {
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'DChemist.csproj'), $updatedCsproj)
    git add -- DChemist.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Cannot stage the version.' }
    git commit -m "Prepare $tag"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot commit the version.' }
}
$sourceCommit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve release source commit.' }

Write-Host '[2/5] Building release' -ForegroundColor Yellow
dotnet publish DChemist.csproj --no-restore -c Release -r win-x64 --self-contained -p:Platform=x64 -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. No release or manifest was published.' }
if (-not (Test-Path -LiteralPath (Join-Path $stage 'DChemist.dll'))) { throw 'Missing application assembly.' }
$builtVersion = (Get-Item -LiteralPath (Join-Path $stage 'DChemist.dll')).VersionInfo.FileVersion
if ($builtVersion -ne $Version) { throw "Built version $builtVersion differs from requested $Version." }

# Preserve each shop's connection settings; debug symbols are unnecessary on shop PCs.
$devConfig = Join-Path $stage 'appsettings.json'
if (Test-Path -LiteralPath $devConfig) { Remove-Item -LiteralPath $devConfig }
Get-ChildItem -LiteralPath $stage -Recurse -File -Filter '*.pdb' | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName
}
if (Test-Path -LiteralPath 'SHOP_PC_UPDATE.md') { Copy-Item -LiteralPath 'SHOP_PC_UPDATE.md' -Destination $stage }

Write-Host '[3/5] Creating package' -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, 'Optimal', $true)
$sha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$notesFile = Join-Path $releaseRoot 'release-notes.md'
[IO.File]::WriteAllText($notesFile, $Notes)

Write-Host '[4/5] Publishing source and GitHub release' -ForegroundColor Yellow
git push origin HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source push failed; no release or manifest was published.' }
gh release create $tag $zip --repo $repo --target $sourceCommit --title $tag --notes-file $notesFile --latest
if ($LASTEXITCODE -ne 0) { throw 'GitHub release failed; the updater manifest was not changed.' }

Write-Host '[5/5] Publishing updater manifest' -ForegroundColor Yellow
[ordered]@{
    LatestVersion = $Version
    DownloadUrl = "https://github.com/$repo/releases/download/$tag/$zipName"
    ReleaseNotes = $Notes
    PackageSha256 = $sha
} | ConvertTo-Json | ForEach-Object { [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'version.json'), $_) }
git add -- version.json
if ($LASTEXITCODE -ne 0) { throw 'Cannot stage the manifest.' }
git commit -m "Release $tag"
if ($LASTEXITCODE -ne 0) { throw 'Cannot commit the manifest.' }
git push origin HEAD
if ($LASTEXITCODE -ne 0) { throw 'Manifest push failed. The GitHub release exists; push version.json before shops can see it.' }

Write-Host "Released $tag. Shop PCs will see it on their next update check." -ForegroundColor Green
Write-Host "Package: $zip"
