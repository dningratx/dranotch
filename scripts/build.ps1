param(
    [string]$Version,
    [ValidateSet("major", "minor", "patch")][string]$Bump,
    [string]$Changelog = "Pembaruan versi DRANOTCH",
    [switch]$SkipInstaller,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "=== DRANOTCH Build & Packaging Script ===" -ForegroundColor Cyan

$rootDir = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $rootDir "src\NotchMusic\NotchMusic.csproj"
$publishDir = Join-Path $rootDir "dist\Publish"
$installerDir = Join-Path $rootDir "dist\Installer"
$innoScript = Join-Path $rootDir "Installer\NotchMusic.iss"

# 1. Determine Version (Major / Minor / Patch / Custom)
[xml]$projXml = Get-Content $projectPath
$currentVersionNode = $projXml.SelectSingleNode("//PropertyGroup/Version")
$currentVersionStr = if ($currentVersionNode) { $currentVersionNode.InnerText.Trim() } else { "1.0.0" }

$targetVersion = $currentVersionStr
$isMajor = $false
$isMinor = $false

if ($Bump) {
    # Parse Semantic Version: Major.Minor.Patch
    $parts = $currentVersionStr.Split('.')
    $maj = if ($parts.Length -ge 1) { [int]$parts[0] } else { 1 }
    $min = if ($parts.Length -ge 2) { [int]$parts[1] } else { 0 }
    $pat = if ($parts.Length -ge 3) { [int]$parts[2] } else { 0 }

    switch ($Bump) {
        "major" {
            $maj++
            $min = 0
            $pat = 0
            $isMajor = $true
        }
        "minor" {
            $min++
            $pat = 0
            $isMinor = $true
        }
        "patch" {
            $pat++
        }
    }
    $targetVersion = "$maj.$min.$pat"
    Write-Host "Bumping version ($Bump): $currentVersionStr -> $targetVersion" -ForegroundColor Green
} elseif ($Version) {
    $targetVersion = $Version.Trim()
    if ($targetVersion.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
        $targetVersion = $targetVersion.Substring(1)
    }
    Write-Host "Setting explicit version: $targetVersion" -ForegroundColor Green
} else {
    Write-Host "Using current version: $targetVersion" -ForegroundColor Gray
}

# 2. Update version in NotchMusic.csproj
if ($targetVersion -ne $currentVersionStr) {
    Write-Host "Updating NotchMusic.csproj to version $targetVersion..." -ForegroundColor Yellow
    $projContent = Get-Content $projectPath -Raw
    $projContent = $projContent -replace "<Version>[^<]+</Version>", "<Version>$targetVersion</Version>"
    Set-Content -Path $projectPath -Value $projContent -NoNewline
}

# 3. Update default version in NotchMusic.iss
$issContent = Get-Content $innoScript -Raw
if ($issContent -match '#define MyAppVersion "[^"]+"') {
    $issContent = $issContent -replace '#define MyAppVersion "[^"]+"', "#define MyAppVersion `"$targetVersion`""
    Set-Content -Path $innoScript -Value $issContent -NoNewline
}

# 4. Clean previous dist
$running = Get-Process NotchMusic -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running instance of NotchMusic to release file locks..." -ForegroundColor Yellow
    Stop-Process -Name NotchMusic -Force
    Start-Sleep -Milliseconds 600
}

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
if (Test-Path $installerDir) {
    Remove-Item -Recurse -Force $installerDir
}

# 5. Run unit tests
if (-not $SkipTests) {
    Write-Host "Running Unit Tests..." -ForegroundColor Yellow
    dotnet test "$rootDir\tests\NotchMusic.Tests\NotchMusic.Tests.csproj" -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed!"
        exit 1
    }
}

# 6. Publish self-contained win-x64
Write-Host "Publishing self-contained Windows x64 release for v$targetVersion..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Dotnet publish failed!"
    exit 1
}

Write-Host "Portable build published successfully to: $publishDir" -ForegroundColor Green

# 7. Inno Setup Compiler
if (-not $SkipInstaller) {
    $isccPaths = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $isccExe = $null
    foreach ($p in $isccPaths) {
        if (Test-Path $p) {
            $isccExe = $p
            break
        }
    }

    if ($isccExe) {
        Write-Host "Compiling Inno Setup Installer for version $targetVersion using $isccExe..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
        & "$isccExe" "/DMyAppVersion=$targetVersion" "$innoScript"
        if ($LASTEXITCODE -eq 0) {
            $expectedInstaller = Join-Path $installerDir "DRANOTCH-Setup-$targetVersion.exe"
            Write-Host "Installer created successfully: $expectedInstaller" -ForegroundColor Green

            # Generate version.json manifest for update service
            $versionMetadata = @{
                version = $targetVersion
                title = "DRANOTCH v$targetVersion"
                releaseDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
                isMajor = $isMajor
                isMinor = $isMinor
                installerFileName = "DRANOTCH-Setup-$targetVersion.exe"
                downloadUrl = "https://github.com/dningratx/dranotch/releases/download/v$targetVersion/DRANOTCH-Setup-$targetVersion.exe"
                releasePageUrl = "https://github.com/dningratx/dranotch/releases/tag/v$targetVersion"
                changelog = $Changelog
            } | ConvertTo-Json -Depth 4

            Set-Content -Path (Join-Path $rootDir "dist\version.json") -Value $versionMetadata -Encoding UTF8
            Set-Content -Path (Join-Path $installerDir "version.json") -Value $versionMetadata -Encoding UTF8
            Write-Host "Generated update manifest: dist\version.json" -ForegroundColor Gray
        } else {
            Write-Error "Inno Setup compilation failed!"
        }
    } else {
        Write-Warning "Inno Setup compiler (ISCC.exe) not found. Portable build is ready, but installer could not be generated."
    }
}

Write-Host "=== Build Completed Successfully for v$targetVersion! ===" -ForegroundColor Cyan
