[CmdletBinding()]
param([switch]$SkipBuild)
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }

$testDir = Join-Path $script:RepoRoot 'artifacts\tests'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$app = Join-Path $script:RepoRoot 'artifacts\bin\BlendPlus.exe'
$report = Join-Path $testDir 'engine.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
$process = Start-Process -FilePath $app -ArgumentList @('--self-test', ('"{0}"' -f $report)) -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'Engine self-test did not finish.' }
$result = Get-Content -LiteralPath $report -Raw
if (-not $result.StartsWith('PASS:')) { throw $result }
Write-Host $result

$testExe = Join-Path $testDir 'MenuExtensionTests.exe'
$compiler = Get-BlendPlusCompiler
$compilerArgs = @(Get-BlendPlusCompilerArguments -Output $testExe -Target exe -Main MenuTest)
$compilerArgs += Join-Path $script:RepoRoot 'tests\MenuExtensionTests.cs'
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Test compiler failed: $LASTEXITCODE" }
$fixture = Join-Path $testDir ('menu-fixture-' + [Guid]::NewGuid().ToString('N'))
try {
    & $testExe $fixture (Join-Path $script:RepoRoot 'assets\previous-menu-hook.txt')
    if ($LASTEXITCODE -ne 0) { throw "Menu tests failed: $LASTEXITCODE" }
} finally {
    Remove-BlendPlusArtifactDirectory -Path $fixture
}
Write-Host 'All automated checks passed. Jianying GUI and rendered images were not tested.'
