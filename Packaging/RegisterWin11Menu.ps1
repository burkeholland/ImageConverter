#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Registers Image Converter with Windows 11's modern context menu using Sparse Package.

.DESCRIPTION
    This script registers the application with a package identity, allowing it to appear
    in Windows 11's main right-click context menu (not just "Show more options").

.NOTES
    Requires Windows 10 version 2004 (build 19041) or later.
    Must be run as Administrator.
#>

param(
    [switch]$Unregister
)

$ErrorActionPreference = "Stop"

# Configuration
$PackageName = "ImageConverter"
$Publisher = "CN=ImageConverter"
$Version = "1.0.0.0"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppDir = Join-Path $ScriptDir "..\ImageConverter.App\bin\Release\net8.0-windows\win-x64\publish"
$ManifestPath = Join-Path $ScriptDir "AppxManifest.xml"
$ExePath = Join-Path $AppDir "ImageConverter.exe"

# Check Windows version
$WinVer = [System.Environment]::OSVersion.Version
if ($WinVer.Build -lt 19041) {
    Write-Host "Error: This feature requires Windows 10 version 2004 (build 19041) or later." -ForegroundColor Red
    exit 1
}

if ($Unregister) {
    Write-Host "Unregistering sparse package..." -ForegroundColor Yellow
    
    try {
        Get-AppxPackage -Name $PackageName | Remove-AppxPackage
        Write-Host "Successfully unregistered!" -ForegroundColor Green
    }
    catch {
        Write-Host "Package not found or already unregistered." -ForegroundColor Yellow
    }
    
    exit 0
}

# Verify app exists
if (-not (Test-Path $ExePath)) {
    Write-Host "Error: Application not found at $ExePath" -ForegroundColor Red
    Write-Host "Please build the application first using Build.bat" -ForegroundColor Yellow
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Windows 11 Context Menu Registration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create the manifest
Write-Host "[1/3] Creating package manifest..." -ForegroundColor Cyan

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
         xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
         xmlns:desktop4="http://schemas.microsoft.com/appx/manifest/desktop/windows10/4"
         xmlns:desktop5="http://schemas.microsoft.com/appx/manifest/desktop/windows10/5"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap uap3 desktop desktop4 desktop5 rescap">

  <Identity Name="$PackageName" Publisher="$Publisher" Version="$Version" ProcessorArchitecture="x64" />

  <Properties>
    <DisplayName>Image Converter</DisplayName>
    <PublisherDisplayName>Image Converter</PublisherDisplayName>
    <Description>Convert images between formats from the right-click menu</Description>
    <Logo>Assets\app.png</Logo>
  </Properties>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>

  <Applications>
    <Application Id="App" Executable="ImageConverter.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Image Converter" 
                          Description="Convert images between formats"
                          BackgroundColor="transparent" 
                          Square150x150Logo="Assets\app.png"
                          Square44x44Logo="Assets\app.png">
      </uap:VisualElements>

      <Extensions>
        <!-- Context menu handler -->
        <desktop4:Extension Category="windows.fileExplorerContextMenus">
          <desktop4:FileExplorerContextMenus>
            <desktop5:ItemType Type=".jpg">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".jpeg">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".png">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".gif">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".bmp">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".webp">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".tiff">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".tif">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
            <desktop5:ItemType Type=".svg">
              <desktop5:Verb Id="ImageConverterConvert" Clsid="B1ACA79B-47E6-4E50-BE81-535E6267C8B7" />
            </desktop5:ItemType>
          </desktop4:FileExplorerContextMenus>
        </desktop4:Extension>
      </Extensions>
    </Application>
  </Applications>
</Package>
"@

# Save manifest to app directory
$AppManifestPath = Join-Path $AppDir "AppxManifest.xml"
$manifest | Out-File -FilePath $AppManifestPath -Encoding utf8

# Copy assets if needed
$AssetsDir = Join-Path $AppDir "Assets"
if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null
}

$SourceAssets = Join-Path $ScriptDir "..\ImageConverter.App\Assets\app.png"
if (Test-Path $SourceAssets) {
    Copy-Item $SourceAssets -Destination $AssetsDir -Force
}

Write-Host "[2/3] Registering sparse package..." -ForegroundColor Cyan

try {
    # Remove existing package if present
    Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
    
    # Register the sparse package
    Add-AppxPackage -Register $AppManifestPath -ExternalLocation $AppDir
    
    Write-Host "  Package registered successfully!" -ForegroundColor Green
}
catch {
    Write-Host "  Error registering package: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Note: Sparse package registration requires Developer Mode to be enabled." -ForegroundColor Yellow
    Write-Host "To enable: Settings > Privacy & Security > For developers > Developer Mode" -ForegroundColor Yellow
    exit 1
}

Write-Host "[3/3] Registering COM class for context menu..." -ForegroundColor Cyan

# Register the COM class that handles the context menu
$clsid = "B1ACA79B-47E6-4E50-BE81-535E6267C8B7"
$regPath = "HKCU:\Software\Classes\CLSID\{$clsid}"

try {
    # Create CLSID entry
    New-Item -Path $regPath -Force | Out-Null
    Set-ItemProperty -Path $regPath -Name "(Default)" -Value "Image Converter Context Menu"
    
    # Create InprocServer32 (but we'll use LocalServer32 for exe)
    $localServerPath = "$regPath\LocalServer32"
    New-Item -Path $localServerPath -Force | Out-Null
    Set-ItemProperty -Path $localServerPath -Name "(Default)" -Value "`"$ExePath`" `"%1`""
    
    Write-Host "  COM class registered!" -ForegroundColor Green
}
catch {
    Write-Host "  Warning: Could not register COM class: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Registration Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "The 'Convert Image...' option should now appear in the main" -ForegroundColor White
Write-Host "Windows 11 context menu when right-clicking image files." -ForegroundColor White
Write-Host ""
Write-Host "Note: You may need to restart Explorer or sign out/in for" -ForegroundColor Yellow
Write-Host "changes to take effect." -ForegroundColor Yellow
Write-Host ""

Read-Host "Press Enter to exit"
