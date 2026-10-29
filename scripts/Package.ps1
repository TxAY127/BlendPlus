[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'Common.ps1')
& (Join-Path $PSScriptRoot 'Build.ps1')
& (Join-Path $PSScriptRoot 'Test.ps1') -SkipBuild

$version = (Get-Content -LiteralPath (Join-Path $script:RepoRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.]+)?$') { throw 'Invalid VERSION.' }
$artifacts = Join-Path $script:RepoRoot 'artifacts'
$stage = Join-Path $artifacts "stage\BlendPlus-$version-windows"
Remove-BlendPlusArtifactDirectory -Path $stage
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $artifacts 'bin\BlendPlus.exe') -Destination $stage
foreach ($name in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md', 'VERSION')) {
    Copy-Item -LiteralPath (Join-Path $script:RepoRoot $name) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $script:RepoRoot 'docs') -Destination $stage -Recurse

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-ProjectZip {
    param([string]$Zip, [string]$Base, [string[]]$Files, [string]$Prefix)
    if (Test-Path -LiteralPath $Zip) { Remove-Item -LiteralPath $Zip -Force }
    $archive = [IO.Compression.ZipFile]::Open($Zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $Files | Sort-Object) {
            $relative = $file.Substring($Base.TrimEnd('\', '/').Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file, "$Prefix/$relative", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
}
$binaryZip = Join-Path $artifacts "BlendPlus-$version-windows.zip"
$binaryFiles = @(Get-ChildItem -LiteralPath $stage -File -Recurse -Force | ForEach-Object { $_.FullName })
Write-ProjectZip -Zip $binaryZip -Base $stage -Files $binaryFiles -Prefix "BlendPlus-$version-windows"

# Explicit source allowlist excludes artifacts, backups, local media and test projects.
$sourceFiles = @()
foreach ($dir in @('src', 'assets', 'tests', 'scripts', 'docs', '.github')) {
    $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $script:RepoRoot $dir) -File -Recurse -Force | ForEach-Object { $_.FullName }
}
foreach ($name in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'VERSION', 'CHANGELOG.md', 'CONTRIBUTING.md', '.gitignore', '.gitattributes', '.editorconfig')) {
    $sourceFiles += Join-Path $script:RepoRoot $name
}
$sourceZip = Join-Path $artifacts "BlendPlus-$version-source.zip"
Write-ProjectZip -Zip $sourceZip -Base $script:RepoRoot -Files $sourceFiles -Prefix 'BlendPlus'

$checksums = foreach ($file in @($binaryZip, $sourceZip)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($file)
}
$checksums | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Packages: $binaryZip ; $sourceZip"
