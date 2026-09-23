[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $projectRoot 'release/win-x64'
$archivePath = Join-Path $projectRoot 'release/SinhalaFontBridge-win-x64.zip'
$projectFile = Join-Path $projectRoot 'SinhalaFontBridge/SinhalaFontBridge.csproj'

& dotnet publish $projectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $publishDirectory 'README.md')
$packageItems = @('SinhalaFontBridge.exe', 'singlish_keys.json', 'Profiles', 'licenses', 'README.md') |
    ForEach-Object { Join-Path $publishDirectory $_ }
$projectLicense = Join-Path $projectRoot 'LICENSE'
if (Test-Path -LiteralPath $projectLicense) {
    $publishedLicense = Join-Path $publishDirectory 'LICENSE'
    Copy-Item -LiteralPath $projectLicense -Destination $publishedLicense
    $packageItems += $publishedLicense
}
foreach ($item in $packageItems) {
    if (-not (Test-Path -LiteralPath $item)) { throw "Missing package item: $item" }
}
Compress-Archive -LiteralPath $packageItems -DestinationPath $archivePath -CompressionLevel Optimal -Force
Write-Output "Portable folder: $publishDirectory"
Write-Output "ZIP package: $archivePath"
