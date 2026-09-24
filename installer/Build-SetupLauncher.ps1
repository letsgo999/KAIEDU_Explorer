param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\KAIEDU-Explorer-Setup.exe')
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found.'
}

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Windows.Forms.dll `
    /out:$OutputPath (Join-Path $PSScriptRoot 'SetupLauncher.cs')
if ($LASTEXITCODE -ne 0) { throw "Setup launcher build failed with exit code $LASTEXITCODE." }

Write-Output "Built: $OutputPath"
