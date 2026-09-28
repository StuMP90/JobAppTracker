# =====================================================================
# JobAppTracker - Windows Store MSIX Package Builder
# =====================================================================
[CmdletBinding()]
param(
    [switch]$SkipTests = $false,
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

# 1. Determine Version (MSIX requires a 4-part quad: Major.Minor.Build.Revision)
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$proj = Get-Content "$ProjectRoot\JobAppTracker.csproj"
    $baseVer = $proj.Project.PropertyGroup.Version
    if (-not $baseVer) { $baseVer = "1.0.2" }
    
    $parts = $baseVer.Split('.')
    if ($parts.Length -eq 3) {
        $VersionQuad = "$baseVer.0"
    } elseif ($parts.Length -ge 4) {
        $VersionQuad = $baseVer
    } else {
        $VersionQuad = "$baseVer.0.0"
    }
} else {
    $parts = $Version.Split('.')
    if ($parts.Length -eq 3) {
        $VersionQuad = "$Version.0"
    } else {
        $VersionQuad = $Version
    }
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "     JobAppTracker Windows Store MSIX Builder     " -ForegroundColor Cyan
Write-Host "     Target Package Version: $VersionQuad        " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 2. Locate Windows 10/11 SDK Tools (makeappx.exe and makepri.exe)
$MakeAppxExe = $null
$MakePriExe = $null

$sdkKitsPath = "C:\Program Files (x86)\Windows Kits\10\bin"
if (Test-Path $sdkKitsPath) {
    $foundAppx = Get-ChildItem -Path $sdkKitsPath -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*\x64\*" } | Select-Object -First 1
    if ($foundAppx) { $MakeAppxExe = $foundAppx.FullName }

    $foundPri = Get-ChildItem -Path $sdkKitsPath -Recurse -Filter "makepri.exe" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like "*\x64\*" } | Select-Object -First 1
    if ($foundPri) { $MakePriExe = $foundPri.FullName }
}

if (-not $MakeAppxExe) {
    $cmd = Get-Command "makeappx.exe" -ErrorAction SilentlyContinue
    if ($cmd) { $MakeAppxExe = $cmd.Source }
}
if (-not $MakePriExe) {
    $cmd = Get-Command "makepri.exe" -ErrorAction SilentlyContinue
    if ($cmd) { $MakePriExe = $cmd.Source }
}

if (-not $MakeAppxExe) {
    Write-Error "makeappx.exe was not found. Please install the Windows 10/11 SDK."
    exit 1
}

Write-Host "Using MakeAppx: $MakeAppxExe" -ForegroundColor Green
if ($MakePriExe) {
    Write-Host "Using MakePri:  $MakePriExe" -ForegroundColor Green
}

# 3. Verification Tests
if (-not $SkipTests) {
    Write-Host "`n[1/5] Running automated verification test suite..." -ForegroundColor Yellow
    dotnet run --project "$ProjectRoot\Tests\JobAppTracker.Tests.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Verification tests failed. Halting MSIX packaging."
        exit 1
    }
} else {
    Write-Host "`n[1/5] Skipping verification tests (--SkipTests specified)." -ForegroundColor DarkGray
}

# 4. Publish Self-Contained 64-bit App
Write-Host "`n[2/5] Publishing self-contained 64-bit application (v$VersionQuad)..." -ForegroundColor Yellow
$LayoutDir = "$ProjectRoot\bin\Release\net8.0-windows\win-x64\msix-layout"
if (Test-Path $LayoutDir) {
    Remove-Item -Recurse -Force $LayoutDir -ErrorAction SilentlyContinue
}

$semVer = ($VersionQuad -split '\.')[0..2] -join '.'

dotnet publish "$ProjectRoot\JobAppTracker.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$semVer `
    --output "$LayoutDir"

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

# 5. Prepare MSIX Package Layout (Manifest + Assets)
Write-Host "`n[3/5] Staging AppxManifest.xml and Visual Assets..." -ForegroundColor Yellow

# Copy Assets folder
Copy-Item -Path "$ProjectRoot\Assets" -Destination "$LayoutDir\Assets" -Recurse -Force

# Read template manifest, update version quad, and write to layout
[xml]$manifest = Get-Content "$ProjectRoot\Package.appxmanifest"
$manifest.Package.Identity.Version = $VersionQuad
$manifest.Save("$LayoutDir\AppxManifest.xml")

# 6. Index Resources with MakePri (if available)
if ($MakePriExe) {
    Write-Host "`n[4/5] Indexing package resources with MakePri..." -ForegroundColor Yellow
    $priConfig = "$LayoutDir\priconfig.xml"
    & $MakePriExe createconfig /cf "$priConfig" /dq en-US /pv 10.0.0 /o
    if ($LASTEXITCODE -eq 0) {
        & $MakePriExe new /pr "$LayoutDir" /cf "$priConfig" /mn "$LayoutDir\AppxManifest.xml" /of "$LayoutDir\resources.pri" /o
    }
    if (Test-Path $priConfig) {
        Remove-Item -Force $priConfig -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "`n[4/5] Skipping MakePri (not found, relying on basic manifest)..." -ForegroundColor DarkGray
}

# 7. Package MSIX with MakeAppx
Write-Host "`n[5/5] Compiling MSIX Package with MakeAppx..." -ForegroundColor Yellow
$OutputDir = "$ProjectRoot\dist"
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$MsixFile = "$OutputDir\JobAppTracker_${VersionQuad}_x64.msix"
if (Test-Path $MsixFile) {
    Remove-Item -Force $MsixFile -ErrorAction SilentlyContinue
}

& $MakeAppxExe pack /d "$LayoutDir" /p "$MsixFile" /o

if ($LASTEXITCODE -ne 0) {
    Write-Error "MakeAppx packaging failed."
    exit 1
}

if (Test-Path $MsixFile) {
    $sizeMb = [Math]::Round((Get-Item $MsixFile).Length / 1MB, 2)
    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " SUCCESS: MSIX Package created successfully!" -ForegroundColor Green
    Write-Host " File: $MsixFile ($sizeMb MB)" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green
    Write-Host " Ready to upload to Microsoft Partner Center Packages page!" -ForegroundColor Cyan
} else {
    Write-Error "Expected MSIX was not found at $MsixFile"
    exit 1
}
