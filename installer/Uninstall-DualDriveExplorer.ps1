$ErrorActionPreference = 'Stop'
$installDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DualDriveExplorer'))
$installScript = Join-Path $installDir 'DualDriveExplorer.ps1'

Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($installScript, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

foreach ($target in @(
    'Software\Classes\*\shell\DualDriveExplorer.CopyGoogleUrl',
    'Software\Classes\Directory\shell\DualDriveExplorer.CopyGoogleUrl'
)) {
    try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($target, $false) } catch { }
}

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'Dual Drive Explorer.lnk'
Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue

$localRoot = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\') + '\'
if ($installDir.StartsWith($localRoot, [StringComparison]::OrdinalIgnoreCase) -and
    [IO.Path]::GetFileName($installDir) -eq 'DualDriveExplorer' -and
    (Test-Path -LiteralPath $installDir)) {
    Get-ChildItem -LiteralPath $installDir -File |
        Where-Object { $_.Name -ne 'settings.json' } |
        Remove-Item -Force
}
Write-Output 'Dual Drive Explorer was uninstalled. Settings and encrypted OAuth data were preserved.'
