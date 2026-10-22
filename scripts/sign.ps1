param(
    [string[]]$FilePath,
    [switch]$ExportCert = $true
)

$ErrorActionPreference = "Stop"

Write-Host "=== DRANOTCH Code Signing Tool ===" -ForegroundColor Cyan

$rootDir = Split-Path -Parent $PSScriptRoot
$certName = "DRANOTCH"

# 1. Find or create Code Signing Certificate
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -like "*CN=$certName*" -or $_.Subject -like "*O=$certName*" } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "Creating new Self-Signed Code Signing Certificate for '$certName'..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject "CN=$certName, O=$certName, OU=Desktop Applications" `
        -FriendlyName "$certName Code Signing Certificate" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "Created certificate: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    Write-Host "Using existing Code Signing Certificate: $($cert.Thumbprint) (Valid until: $($cert.NotAfter.ToShortDateString()))" -ForegroundColor Green
}

# 2. Export public certificate (.cer) and helper script
if ($ExportCert) {
    $outDirs = @(
        (Join-Path $rootDir "dist"),
        (Join-Path $rootDir "dist\Installer")
    )

    foreach ($dir in $outDirs) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
        }

        $cerPath = Join-Path $dir "$certName.cer"
        Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
        Write-Host "Exported public certificate: $cerPath" -ForegroundColor Gray

        # Create one-click helper script to trust the certificate
        $batPath = Join-Path $dir "Install-Certificate.bat"
        $batContent = @"
@echo off
title Memasang Sertifikat Digital DRANOTCH
cls
echo ======================================================================
echo   DRANOTCH - Pemasangan Sertifikat Digital Mandiri (Trusted Root)
echo ======================================================================
echo.
echo Script ini akan mendaftarkan sertifikat DRANOTCH ke Otoritas
echo Sertifikasi Tepercaya (Trusted Root) pada komputer Anda.
echo.
echo Windows akan menampilkan jendela peringatan keamanan konfirmasi:
echo "Security Warning: Do you want to install this certificate?"
echo.
echo Silakan klik [YES] atau [YA] untuk menyetujui.
echo Setelah disetujui, peringatan SmartScreen tidak akan muncul lagi.
echo.
pause
echo.
echo Mendaftarkan sertifikat...
certutil -addstore -user root "%~dp0DRANOTCH.cer"
echo.
if %ERRORLEVEL% EQU 0 (
    echo [BERHASIL] Sertifikat DRANOTCH berhasil dipasang dan dipercaya!
    echo Sekarang installer dan aplikasi DRANOTCH dapat dibuka tanpa peringatan.
) else (
    echo [INFO] Pemasangan sertifikat dibatalkan atau terjadi kesalahan (%ERRORLEVEL%).
)
echo.
pause
"@
        Set-Content -Path $batPath -Value $batContent -Encoding ASCII
        Write-Host "Created certificate installer script: $batPath" -ForegroundColor Gray
    }
}

# 3. Determine target files to sign
$targets = @()
if ($FilePath -and $FilePath.Length -gt 0) {
    foreach ($p in $FilePath) {
        if (Test-Path $p) {
            $targets += (Resolve-Path $p).Path
        } else {
            Write-Warning "Target path not found: $p"
        }
    }
} else {
    # Default targets: Publish exe and Installer exe
    $publishExe = Join-Path $rootDir "dist\Publish\NotchMusic.exe"
    if (Test-Path $publishExe) {
        $targets += $publishExe
    }

    $installerDir = Join-Path $rootDir "dist\Installer"
    if (Test-Path $installerDir) {
        $installerExes = Get-ChildItem -Path $installerDir -Filter "*.exe" | Select-Object -ExpandProperty FullName
        if ($installerExes) {
            $targets += $installerExes
        }
    }
}

# 4. Sign target files
$timestampUrl = "http://timestamp.digicert.com"

if ($targets.Count -eq 0) {
    Write-Host "No executables found to sign. Specify -FilePath or build first." -ForegroundColor Yellow
} else {
    foreach ($target in $targets) {
        Write-Host "Signing binary: $target..." -ForegroundColor Yellow
        $signed = $false

        # Attempt signing with timestamp server
        try {
            $sig = Set-AuthenticodeSignature -FilePath $target -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $timestampUrl -ErrorAction Stop
            if ($sig) {
                Write-Host "  -> Successfully signed with DigiCert timestamp! (Status: $($sig.Status))" -ForegroundColor Green
                $signed = $true
            }
        } catch {
            Write-Warning "  -> Timestamp server unreachable, falling back to local signature..."
        }

        if (-not $signed) {
            try {
                $sig = Set-AuthenticodeSignature -FilePath $target -Certificate $cert -HashAlgorithm SHA256 -ErrorAction Stop
                Write-Host "  -> Successfully signed (no timestamp)! (Status: $($sig.Status))" -ForegroundColor Green
            } catch {
                Write-Error "  -> Failed to sign ${target}: $_"
            }
        }
    }
}

Write-Host "=== Code Signing Completed! ===" -ForegroundColor Cyan
