# Run with: pwsh -NoProfile -File tests/ReleaseSigning.Tests.ps1
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../scripts/Assert-ReleaseSignature.ps1'
$fixture = [System.IO.Path]::GetTempFileName()
$passed = 0

function Assert-Rejected([scriptblock] $Action, [string] $Expected) {
    try {
        & $Action
    } catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw }
        $script:passed++
        return
    }
    throw "Expected rejection containing: $Expected"
}

try {
    # Real Windows checks must reject an unsigned file and a missing file.
    Assert-Rejected { & $scriptPath -Path $fixture } 'signature rejected'
    Assert-Rejected { & $scriptPath -Path "$fixture.missing" } 'not found'

    # Exercise certificate states without installing a test root certificate.
    function Get-AuthenticodeSignature {
        param([string] $LiteralPath)
        $global:releaseSignatureTestResult
    }

    foreach ($status in @('NotSigned', 'HashMismatch', 'NotTrusted', 'UnknownError')) {
        $global:releaseSignatureTestResult = [pscustomobject]@{
            Status = $status
            SignerCertificate = [pscustomobject]@{ Subject = 'Test publisher'; Thumbprint = 'test' }
            SignatureType = 'Authenticode'
            TimeStamperCertificate = [pscustomobject]@{ Subject = 'Test timestamp' }
        }
        Assert-Rejected { & $scriptPath -Path $fixture } 'signature rejected'
    }

    $global:releaseSignatureTestResult.Status = 'Valid'
    & $scriptPath -Path $fixture
    $passed++

    $global:releaseSignatureTestResult.TimeStamperCertificate = $null
    Assert-Rejected { & $scriptPath -Path $fixture } 'no trusted timestamp'
    $global:releaseSignatureTestResult.TimeStamperCertificate = [pscustomobject]@{ Subject = 'Test timestamp' }
    $global:releaseSignatureTestResult.SignatureType = 'Catalog'
    Assert-Rejected { & $scriptPath -Path $fixture } 'embedded Authenticode'
    $global:releaseSignatureTestResult.SignatureType = 'Authenticode'
    $global:releaseSignatureTestResult.SignerCertificate = $null
    Assert-Rejected { & $scriptPath -Path $fixture } 'signature rejected'
} finally {
    Remove-Item -LiteralPath $fixture -ErrorAction SilentlyContinue
    Remove-Item Function:Get-AuthenticodeSignature -ErrorAction SilentlyContinue
    Remove-Variable releaseSignatureTestResult -Scope Global -ErrorAction SilentlyContinue
}

Write-Host "$passed release signing checks passed."
