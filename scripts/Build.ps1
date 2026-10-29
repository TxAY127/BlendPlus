[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'Common.ps1')

$bin = Join-Path $script:RepoRoot 'artifacts\bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$output = Join-Path $bin 'BlendPlus.exe'
$compiler = Get-BlendPlusCompiler
$compilerArgs = Get-BlendPlusCompilerArguments -Output $output
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compiler failed: $LASTEXITCODE" }
Write-Host "Built: $output"
