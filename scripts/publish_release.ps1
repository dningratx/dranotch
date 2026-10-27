$ErrorActionPreference = "Stop"

$source = @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public class CredHelper {
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credential);
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern void CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREDENTIAL {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    public static string GetPassword(string target) {
        IntPtr credPtr;
        if (CredRead(target, 1, 0, out credPtr)) {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            byte[] bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
            CredFree(credPtr);
            string sUtf8 = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrEmpty(sUtf8) && !sUtf8.Contains("\0")) return sUtf8;
            return Encoding.Unicode.GetString(bytes);
        }
        return null;
    }
}
'@

Add-Type -TypeDefinition $source -ErrorAction SilentlyContinue

$targets = @(
    'git:https://dningratx@github.com',
    'git:https://github.com',
    'LegacyGeneric:target=git:https://dningratx@github.com',
    'LegacyGeneric:target=git:https://github.com'
)

$token = $null
foreach ($t in $targets) {
    $pwd = [CredHelper]::GetPassword($t)
    if (![string]::IsNullOrWhiteSpace($pwd)) {
        $token = $pwd
        break
    }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Error "Could not retrieve GitHub token from Windows Credential Manager."
    exit 1
}

$repo = "dningratx/dranotch"
$tag = "v1.6.8"
$title = 'DRANOTCH v1.6.8 - To-Do Category Tree Branch & Remaining Countdown'
$notesPath = Join-Path $PSScriptRoot "..\dist\release_notes_v1.6.8.md"
$notes = [System.IO.File]::ReadAllText($notesPath, [System.Text.Encoding]::UTF8)

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept"        = "application/vnd.github+json"
    "User-Agent"    = "DRANOTCH-Release-Script"
}

Write-Host "Checking if release for $tag exists..." -ForegroundColor Cyan
$release = $null
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers $headers -Method Get
    Write-Host "Existing release found with ID $($release.id)" -ForegroundColor Yellow
} catch {
    Write-Host "Creating new release for $tag..." -ForegroundColor Cyan
    $body = @{
        tag_name         = $tag
        name             = $title
        body             = $notes
        draft            = $false
        prerelease       = $false
        generate_release_notes = $false
    } | ConvertTo-Json -Depth 4

    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers -Method Post -Body $body
    Write-Host "Created release with ID $($release.id)" -ForegroundColor Green
}

$releaseId = $release.id

$assets = @(
    @{
        Path = (Join-Path $PSScriptRoot "..\dist\Installer\DRANOTCH-Setup-1.6.8.exe")
        Name = "DRANOTCH-Setup-1.6.8.exe"
        ContentType = "application/octet-stream"
    },
    @{
        Path = (Join-Path $PSScriptRoot "..\dist\Installer\DRANOTCH.cer")
        Name = "DRANOTCH.cer"
        ContentType = "application/x-x509-ca-cert"
    },
    @{
        Path = (Join-Path $PSScriptRoot "..\dist\Installer\Install-Certificate.bat")
        Name = "Install-Certificate.bat"
        ContentType = "text/plain"
    }
)

foreach ($asset in $assets) {
    if (-not (Test-Path $asset.Path)) {
        Write-Warning "File not found: $($asset.Path)"
        continue
    }

    Write-Host "Uploading $($asset.Name)..." -ForegroundColor Cyan
    $uploadUri = "https://uploads.github.com/repos/$repo/releases/$releaseId/assets?name=$($asset.Name)"
    $uploadHeaders = @{
        "Authorization"  = "Bearer $token"
        "Accept"         = "application/vnd.github+json"
        "User-Agent"     = "DRANOTCH-Release-Script"
        "Content-Type"   = $asset.ContentType
    }

    $response = Invoke-RestMethod -Uri $uploadUri -Headers $uploadHeaders -Method Post -InFile $asset.Path
    Write-Host "Uploaded $($asset.Name) successfully!" -ForegroundColor Green
}

Write-Host "=== Release published successfully: $($release.html_url) ===" -ForegroundColor Green
