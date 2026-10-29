$ErrorActionPreference = 'Stop'
$script:RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-BlendPlusCompiler {
    if ($env:OS -ne 'Windows_NT') { throw 'Build BlendPlus on Windows.' }
    foreach ($framework in @('Framework64', 'Framework')) {
        $candidate = Join-Path $env:WINDIR "Microsoft.NET\$framework\v4.0.30319\csc.exe"
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw '.NET Framework C# compiler not found. Install .NET Framework 4.8.'
}

function Get-BlendPlusCompilerArguments {
    param([string]$Output, [string]$Target = 'winexe', [string]$Main = 'MainForm')
    @(
        '/nologo', '/optimize+', '/platform:anycpu', "/target:$Target", "/main:$Main", "/out:$Output",
        '/reference:System.dll', '/reference:System.Core.dll',
        '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
        '/reference:System.Web.Extensions.dll',
        "/resource:$(Join-Path $script:RepoRoot 'assets\menu-hook.txt'),menu-hook.txt",
        "/resource:$(Join-Path $script:RepoRoot 'assets\previous-menu-hook.txt'),previous-menu-hook.txt",
        "/resource:$(Join-Path $script:RepoRoot 'assets\BlendPlusMenuLimiter.qml'),BlendPlusMenuLimiter.qml",
        (Join-Path $script:RepoRoot 'src\BlendPlus\AssemblyInfo.cs'),
        (Join-Path $script:RepoRoot 'src\BlendPlus\BlendPlus.cs'),
        (Join-Path $script:RepoRoot 'src\BlendPlus\MenuExtension.cs')
    )
}

function Remove-BlendPlusArtifactDirectory {
    param([string]$Path)
    $resolved = [IO.Path]::GetFullPath($Path)
    $allowed = [IO.Path]::GetFullPath((Join-Path $script:RepoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete outside artifacts: $resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        if ((Get-Item -LiteralPath $resolved -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing to delete a linked directory: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
