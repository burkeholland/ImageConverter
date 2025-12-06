#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls Image Converter and removes shell integration.

.DESCRIPTION
    This script removes the Image Converter application and its shell context menu entries.

.EXAMPLE
    .\Uninstall.ps1
#>

param(
    [string]$InstallPath = "$env:ProgramFiles\ImageConverter"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Image Converter Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Error: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click and select 'Run as Administrator'" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Remove shell extension
Write-Host "[1/3] Removing shell context menu entries..." -ForegroundColor Cyan

$imageExtensions = @(".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".svg")
$registryKeyName = "ImageConverter"

foreach ($ext in $imageExtensions) {
    $keyPath = "Registry::HKEY_CLASSES_ROOT\SystemFileAssociations\$ext\shell\$registryKeyName"
    
    try {
        if (Test-Path $keyPath) {
            Remove-Item -Path $keyPath -Recurse -Force
            Write-Host "  Removed registration for $ext" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  Failed to remove for $ext : $_" -ForegroundColor Yellow
    }
}

# Remove generic image type registration
$keyPath = "Registry::HKEY_CLASSES_ROOT\SystemFileAssociations\image\shell\$registryKeyName"
try {
    if (Test-Path $keyPath) {
        Remove-Item -Path $keyPath -Recurse -Force
        Write-Host "  Removed registration for image type" -ForegroundColor Green
    }
}
catch {
    Write-Host "  Failed to remove for image type: $_" -ForegroundColor Yellow
}

# Remove Start Menu shortcut
Write-Host "[2/3] Removing Start Menu shortcut..." -ForegroundColor Cyan
$startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Image Converter.lnk"

if (Test-Path $startMenuPath) {
    Remove-Item -Path $startMenuPath -Force
    Write-Host "  Removed Start Menu shortcut" -ForegroundColor Green
}

# Remove application files
Write-Host "[3/3] Removing application files..." -ForegroundColor Cyan

if (Test-Path $InstallPath) {
    # Check if the application is running
    $process = Get-Process -Name "ImageConverter" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "  Stopping running instance..." -ForegroundColor Yellow
        Stop-Process -Name "ImageConverter" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    
    Remove-Item -Path $InstallPath -Recurse -Force
    Write-Host "  Removed application files from $InstallPath" -ForegroundColor Green
}
else {
    Write-Host "  Application folder not found (already removed?)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Uninstallation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Image Converter has been removed from your system." -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to exit"
