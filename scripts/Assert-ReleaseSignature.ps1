[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Path
)

$ErrorActionPreference = 'Stop'

foreach ($file in $Path) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Release file not found: $file"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate) {
        throw "Release signature rejected for ${file}: $($signature.Status). A publicly trusted code-signing certificate is required."
    }
    if ($signature.SignatureType -ne 'Authenticode') {
        throw "Release file must have an embedded Authenticode signature: $file"
    }
    if (-not $signature.TimeStamperCertificate) {
        throw "Release signature has no trusted timestamp: $file"
    }

    Write-Host "Verified: $file"
    Write-Host "Publisher: $($signature.SignerCertificate.Subject)"
    Write-Host "Certificate: $($signature.SignerCertificate.Thumbprint)"
}
