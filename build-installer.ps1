# =====================================================================
# JobAppTracker - Installer Build Script (Inno Setup 7)
# =====================================================================
[CmdletBinding()]
param(
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "     JobAppTracker Windows Installer Builder      " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Verification Tests
if (-not $SkipTests) {
    Write-Host "`n[1/4] Running automated verification test suite..." -ForegroundColor Yellow
    dotnet run --project "$ProjectRoot\Tests\JobAppTracker.Tests.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Verification tests failed. Halting installer build."
        exit 1
    }
} else {
    Write-Host "`n[1/4] Skipping verification tests (--SkipTests specified)." -ForegroundColor DarkGray
}

# 2. Publish Self-Contained Release
Write-Host "`n[2/4] Publishing self-contained 64-bit application..." -ForegroundColor Yellow
$PublishDir = "$ProjectRoot\bin\Release\net8.0-windows\win-x64\publish"
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir -ErrorAction SilentlyContinue
}

dotnet publish "$ProjectRoot\JobAppTracker.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

# 3. Locate Inno Setup Compiler (ISCC.exe)
Write-Host "`n[3/4] Locating Inno Setup 7 compiler (ISCC.exe)..." -ForegroundColor Yellow
$IsccCandidates = @(
    "ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$IsccPath = $null
foreach ($candidate in $IsccCandidates) {
    $found = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($found) {
        $IsccPath = $found.Source
        break
    }
    if (Test-Path $candidate) {
        $IsccPath = $candidate
        break
    }
}

if (-not $IsccPath) {
    Write-Error "Inno Setup compiler (ISCC.exe) was not found. Please ensure Inno Setup is installed."
    exit 1
}

Write-Host "Found Inno Setup at: $IsccPath" -ForegroundColor Green

# 4. Compile Installer
Write-Host "`n[4/4] Compiling Windows Installer..." -ForegroundColor Yellow
$IssFile = "$ProjectRoot\installer.iss"
& $IsccPath $IssFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
    exit 1
}

$InstallerFile = "$ProjectRoot\dist\JobAppTrackerSetup.exe"
if (Test-Path $InstallerFile) {
    $sizeMb = [Math]::Round((Get-Item $InstallerFile).Length / 1MB, 2)
    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " SUCCESS: Installer generated successfully!" -ForegroundColor Green
    Write-Host " File: $InstallerFile ($sizeMb MB)" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green
} else {
    Write-Error "Expected installer was not created at $InstallerFile"
    exit 1
}
