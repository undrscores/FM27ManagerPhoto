# Fails (exit 1) if a built DLL, or the source tree, references networking, process launching,
# registry access or dynamic assembly loading.
# Usage:  pwsh scripts/check-dll.ps1 -Dll path\to\FM27ManagerPhoto.dll
#         pwsh scripts/check-dll.ps1 -Source        (scans *.cs and *.csproj instead)
param([string]$Dll, [switch]$Source)

$forbidden = @(
    'System.Net', 'HttpClient', 'WebClient', 'WebRequest', 'Socket',
    'System.Diagnostics.Process', 'ProcessStartInfo',
    'Microsoft.Win32', 'RegistryKey',
    'Assembly.Load', 'LoadFrom', 'LoadFile', 'DllImport', 'http://', 'https://'
)

if ($Source) {
    $root = Split-Path $PSScriptRoot -Parent
    $files = Get-ChildItem $root -Recurse -Include *.cs, *.csproj |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
    $text = ($files | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
    # Ignore the 'DllImport'-style words inside comments is not attempted: keep the check strict.
    $label = "source files"
}
else {
    if (-not $Dll -or -not (Test-Path $Dll)) { Write-Error "DLL not found: $Dll"; exit 2 }
    # Type/assembly names live in the metadata string heap as UTF-8, so a byte-level scan finds them.
    $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($Dll))
    $label = $Dll
}

$hits = $forbidden | Where-Object { $text.Contains($_) }
if ($hits) {
    Write-Host "FAIL: $label contains forbidden references: $($hits -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "OK: $label has no networking, process, registry or assembly-loading references."
