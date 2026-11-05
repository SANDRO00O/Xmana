$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$publisher = 'CN=Xmana Development'
$password = ([Guid]::NewGuid().ToString('N') + 'Aa!')
$algorithm = [System.Security.Cryptography.HashAlgorithmName]::SHA256
$padding = [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
$subject = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new($publisher)
$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$certificate = $null
$addedStores = @()

try {
    Write-Host 'Creating an ephemeral RSA-2048 X.509 code-signing certificate.'
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        $subject,
        $rsa,
        $algorithm,
        $padding
    )

    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true)
    )
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
            $true
        )
    )

    $ekuOids = [System.Security.Cryptography.OidCollection]::new()
    [void]$ekuOids.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($ekuOids, $true)
    )

    $notBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5)
    $notAfter = [DateTimeOffset]::UtcNow.AddDays(30)
    $certificate = $request.CreateSelfSigned($notBefore, $notAfter)

    $pfxPath = Join-Path $env:RUNNER_TEMP 'xmana-dev-signing.pfx'
    $cerPath = Join-Path $env:GITHUB_WORKSPACE 'artifacts\Xmana-Dev-Certificate.cer'
    $certUtil = Join-Path $env:WINDIR 'System32\certutil.exe'

    if (-not (Test-Path $certUtil)) { throw "certutil.exe was not found at $certUtil." }
    if (-not (Test-Path (Split-Path $cerPath -Parent))) {
        New-Item -ItemType Directory -Force -Path (Split-Path $cerPath -Parent) | Out-Null
    }

    Write-Host 'Exporting temporary PFX and public certificate.'
    [System.IO.File]::WriteAllBytes(
        $pfxPath,
        $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password)
    )
    $cerBytes = $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    [System.IO.File]::WriteAllBytes($cerPath, $cerBytes)
    $thumbprint = $certificate.Thumbprint

    foreach ($storeName in @('Root', 'TrustedPeople')) {
        Write-Host "Adding the temporary public certificate to CurrentUser\$storeName with certutil."
        & $certUtil -user -addstore -f $storeName $cerPath
        if ($LASTEXITCODE -ne 0) {
            throw "Adding the development certificate to CurrentUser\$storeName failed with exit code $LASTEXITCODE."
        }
        $addedStores += $storeName
    }

    "::add-mask::$password" | Out-Host
    "PFX_PATH=$pfxPath" | Out-File -FilePath $env:GITHUB_ENV -Append
    "PFX_PASSWORD=$password" | Out-File -FilePath $env:GITHUB_ENV -Append
    "CERT_THUMBPRINT=$thumbprint" | Out-File -FilePath $env:GITHUB_ENV -Append
    Write-Host "Development certificate ready. Thumbprint: $thumbprint"
}
catch {
    foreach ($storeName in $addedStores) {
        & (Join-Path $env:WINDIR 'System32\certutil.exe') -user -delstore $storeName $certificate.Thumbprint
    }
    throw
}
finally {
    if ($certificate) { $certificate.Dispose() }
    if ($rsa) { $rsa.Dispose() }
}
