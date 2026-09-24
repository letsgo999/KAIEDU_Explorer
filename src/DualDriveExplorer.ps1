param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArguments
)

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot 'Program.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Application source not found: $sourcePath"
}

Add-Type -Path $sourcePath -ReferencedAssemblies @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Net.Http.dll',
    'System.Security.dll',
    'System.Web.Extensions.dll',
    'System.Windows.Forms.dll'
)

[string[]]$argumentsToPass = if ($null -eq $AppArguments) { @('') } else { @($AppArguments) }
[DualDriveExplorer.Program]::Main($argumentsToPass)
