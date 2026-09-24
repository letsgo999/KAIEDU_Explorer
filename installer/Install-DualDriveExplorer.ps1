param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\src')
)

$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'DualDriveExplorer'
$installScript = Join-Path $installDir 'DualDriveExplorer.ps1'
$installSource = Join-Path $installDir 'Program.cs'
$powerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$sourceScript = Join-Path $SourceDirectory 'DualDriveExplorer.ps1'
$sourceCode = Join-Path $SourceDirectory 'Program.cs'

foreach ($required in @($sourceScript, $sourceCode)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Application file not found: $required"
    }
}

Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($installScript, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath $sourceScript -Destination $installScript -Force
Copy-Item -LiteralPath $sourceCode -Destination $installSource -Force
Remove-Item -LiteralPath (Join-Path $installDir 'DualDriveExplorer.exe') -Force -ErrorAction SilentlyContinue

$menuText = [string]::Concat(
    [char]0xAD6C,[char]0xAE00,' ',[char]0xD074,[char]0xB77C,[char]0xC6B0,[char]0xB4DC,' URL ',
    [char]0xBCF5,[char]0xC0AC,[char]0xD558,[char]0xAE30)
$command = '"' + $powerShellExe + '" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $installScript + '" --copy-url "%1"'
$targets = @(
    'Software\Classes\*\shell\DualDriveExplorer.CopyGoogleUrl',
    'Software\Classes\Directory\shell\DualDriveExplorer.CopyGoogleUrl'
)
foreach ($target in $targets) {
    $menuKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($target)
    $menuKey.SetValue('', $menuText)
    $menuKey.SetValue('Position', 'Bottom')
    $commandKey = $menuKey.CreateSubKey('command')
    $commandKey.SetValue('', $command)
    $commandKey.Dispose()
    $menuKey.Dispose()
}

$startup = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startup 'Dual Drive Explorer.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $powerShellExe
$shortcut.Arguments = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $installScript + '"'
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = 'Open and remember two Explorer windows for Google Drive and local files.'
$shortcut.Save()

Start-Process -FilePath $powerShellExe -ArgumentList @(
    '-NoProfile', '-WindowStyle', 'Hidden', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $installScript + '"')
)
Write-Output "Installed: $installScript"
Write-Output "Context menu: $menuText"
Write-Output "Startup shortcut: $shortcutPath"
