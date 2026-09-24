# ==============================================================================
# PHASE 5 — TARGETED RERUN: TEST 3.8 & TEST 3.10 ONLY
# Admin Certificate Upload & Admin Signed URL + %PDF Validation
# Reuses the exact, proven Phase 4 Admin Auth and Cloud Storage contracts.
# ==============================================================================

$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$solutionDir = Split-Path -Parent $PSScriptRoot
Set-Location $solutionDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "      PHASE 5 TARGETED TEST RERUN: TEST 3.8 & TEST 3.10 ONLY     " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

function Get-SafeErrorMessage {
    param([System.Management.Automation.ErrorRecord]$errRecord, [string]$keyToRedact = "", [string]$storageKeyToRedact = "")
    $msg = if ($errRecord -and $errRecord.Exception) { $errRecord.Exception.Message } else { "Unknown error" }
    $statusCode = 0
    $body = ""
    if ($errRecord -and $errRecord.Exception -and $errRecord.Exception.Response) {
        try { $statusCode = [int]$errRecord.Exception.Response.StatusCode } catch {}
        try {
            $stream = $errRecord.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
            }
        } catch {}
    }
    $full = if ($statusCode -gt 0) {
        if ($body) { "HTTP $statusCode - $msg (Response: $body)" } else { "HTTP $statusCode - $msg" }
    } else {
        $msg
    }
    if (![string]::IsNullOrEmpty($keyToRedact)) {
        $full = $full.Replace($keyToRedact, "[REDACTED_SERVICE_KEY]")
    }
    if (![string]::IsNullOrEmpty($storageKeyToRedact)) {
        $full = $full.Replace($storageKeyToRedact, "[REDACTED_STORAGE_KEY]")
    }
    $full = [regex]::Replace($full, 'eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+', '[REDACTED_JWT]')
    $full = [regex]::Replace($full, 'sb_secret_[A-Za-z0-9-_]+', '[REDACTED_SECRET_KEY]')
    return @{ StatusCode = $statusCode; SafeMessage = $full }
}

function Normalize-SupabaseUrl {
    param([string]$rawUrl)
    if ([string]::IsNullOrWhiteSpace($rawUrl)) { return "" }
    $url = $rawUrl.Trim().TrimEnd('/')
    $url = [regex]::Replace($url, '(\.supabase\.co)+$', '.supabase.co', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not ($url.StartsWith('http://', [System.StringComparison]::OrdinalIgnoreCase) -or $url.StartsWith('https://', [System.StringComparison]::OrdinalIgnoreCase))) {
        $url = 'https://' + $url
    }
    try {
        $uri = [System.Uri]$url
        if (-not $uri.Host.Contains('.')) {
            $url = 'https://' + $uri.Host + '.supabase.co'
        }
    } catch {}
    return $url.TrimEnd('/')
}

function Test-IsPlaceholderOrInvalidCredential {
    param([string]$url, [string]$key)
    
    $issues = @()
    if ([string]::IsNullOrWhiteSpace($url)) {
        $issues += "SUPABASE_URL is missing or empty"
    } else {
        $trimmedUrl = (Normalize-SupabaseUrl $url)
        $urlLower = $trimmedUrl.ToLowerInvariant()
        if ($urlLower -match 'your[-_]?project[-_]?ref|your[-_]?project|your[-_]?actual|placeholder|example\.com|<.*>|\[.*\]') {
            $issues += "SUPABASE_URL contains placeholder value ($trimmedUrl)"
        }
        if (-not ($trimmedUrl -match '^https?://[a-zA-Z0-9\.\-_]+')) {
            $issues += "SUPABASE_URL is not a valid HTTP/HTTPS URL ($trimmedUrl)"
        }
    }

    if ([string]::IsNullOrWhiteSpace($key)) {
        $issues += "SUPABASE_SERVICE_KEY is missing or empty"
    } else {
        $trimmedKey = $key.Trim()
        $keyLower = $trimmedKey.ToLowerInvariant()
        if ($keyLower -match 'your[-_]?service[-_]?role[-_]?key|your[-_]?service[-_]?key|your[-_]?actual|placeholder|<.*>|\[.*\]') {
            $issues += "SUPABASE_SERVICE_KEY contains placeholder text"
        }

        $isLegacyJwt = ($trimmedKey.StartsWith("eyJ") -and $trimmedKey.Split('.').Length -eq 3)
        $isModernSecret = ($trimmedKey.StartsWith("sb_secret_") -and $trimmedKey.Length -gt 15)

        if (-not ($isLegacyJwt -or $isModernSecret)) {
            $issues += "SUPABASE_SERVICE_KEY is neither a valid Supabase Secret API key nor a valid service_role JWT"
        }
    }

    return $issues
}

# 1. Discover Credentials
$rawSupabaseUrl = if ($env:SUPABASE_URL) { $env:SUPABASE_URL } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_URL") }
if (-not $rawSupabaseUrl) { $rawSupabaseUrl = [System.Environment]::GetEnvironmentVariable("SUPABASE_URL", "User") }
if (-not $rawSupabaseUrl) { $rawSupabaseUrl = [System.Environment]::GetEnvironmentVariable("SUPABASE_URL", "Machine") }

$supabaseUrl = Normalize-SupabaseUrl $rawSupabaseUrl
if ($env:SUPABASE_URL) {
    $env:SUPABASE_URL = $supabaseUrl
}

$supabaseServiceKey = if ($env:SUPABASE_SERVICE_KEY) { $env:SUPABASE_SERVICE_KEY } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_KEY") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = if ($env:SUPABASE_SERVICE_ROLE_KEY) { $env:SUPABASE_SERVICE_ROLE_KEY } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") } }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_KEY", "User") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", "User") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_KEY", "Machine") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", "Machine") }

$supabaseStorageKey = if ($env:SUPABASE_STORAGE_SERVICE_ROLE_KEY) { $env:SUPABASE_STORAGE_SERVICE_ROLE_KEY } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY") }
if (-not $supabaseStorageKey) { $supabaseStorageKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY", "User") }
if (-not $supabaseStorageKey) { $supabaseStorageKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY", "Machine") }

if (-not $supabaseStorageKey -and $supabaseServiceKey -and $supabaseServiceKey.Trim().StartsWith("eyJ")) {
    $supabaseStorageKey = $supabaseServiceKey.Trim()
}

$hasModernSecret = (![string]::IsNullOrWhiteSpace($supabaseServiceKey) -and $supabaseServiceKey.Trim().StartsWith("sb_secret_"))
$hasLegacyStorageKey = (![string]::IsNullOrWhiteSpace($supabaseStorageKey) -and $supabaseStorageKey.Trim().StartsWith("eyJ"))

Write-Host "`nSafe Diagnostics:" -ForegroundColor Cyan
Write-Host "  - Supabase URL configured:         $([bool]$supabaseUrl)" -ForegroundColor Gray
Write-Host "  - modern secret configured:        $(if ($hasModernSecret) { 'YES' } else { 'NO' })" -ForegroundColor Gray
Write-Host "  - legacy storage key configured:   $(if ($hasLegacyStorageKey) { 'YES' } else { 'NO' })" -ForegroundColor Gray

$credIssues = Test-IsPlaceholderOrInvalidCredential $supabaseUrl $supabaseServiceKey

if ($credIssues.Count -gt 0) {
    Write-Host "`nREAL Supabase Cloud Credentials: NOT AVAILABLE IN CURRENT ENVIRONMENT" -ForegroundColor Magenta
    Write-Host "Details:" -ForegroundColor Magenta
    foreach ($issue in $credIssues) {
        Write-Host "  - $issue" -ForegroundColor Magenta
    }
    Write-Host "`nPer QA requirements: Mock or local storage is STRICTLY FORBIDDEN." -ForegroundColor Yellow
    Write-Host "Marking Tests 3.8 and 3.10 explicitly as [NOT VERIFIED].`n" -ForegroundColor Yellow

    Write-Host "[NOT VERIFIED] 3.8 Admin Certificate Upload to Cloud Storage" -ForegroundColor Yellow
    Write-Host "       Requires live Supabase credentials (SUPABASE_URL, SUPABASE_SERVICE_KEY). Mock/local storage not used." -ForegroundColor Gray

    Write-Host "[NOT VERIFIED] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Yellow
    Write-Host "       Dependent on live cloud storage upload. Mock/local storage not used." -ForegroundColor Gray

    Write-Host "`n=================================================================" -ForegroundColor Cyan
    Write-Host "TARGETED RERUN SUMMARY: 0 PASSED, 2 NOT VERIFIED, 0 FAILED" -ForegroundColor Yellow
    Write-Host "Note: Phase 5 is NOT claimed fully cloud-verified without live credentials." -ForegroundColor Yellow
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 2
}

if (-not $hasLegacyStorageKey) {
    Write-Host "`nREAL Supabase Storage REST Credentials: NOT CONFIGURED" -ForegroundColor Yellow
    Write-Host "Direct Supabase Storage REST operations require a legacy service_role JWT for Authorization header." -ForegroundColor Yellow
    Write-Host "Modern sb_secret keys cannot be passed as Authorization: Bearer (triggers 'Invalid Compact JWS' / HTTP 400)." -ForegroundColor Yellow
    Write-Host "Missing required environment variable: SUPABASE_STORAGE_SERVICE_ROLE_KEY" -ForegroundColor Magenta
    Write-Host "`nPer QA requirements: Mock or local storage is STRICTLY FORBIDDEN." -ForegroundColor Yellow
    Write-Host "Marking Tests 3.8 and 3.10 explicitly as [NOT VERIFIED].`n" -ForegroundColor Yellow

    Write-Host "[NOT VERIFIED] 3.8 Admin Certificate Upload to Cloud Storage" -ForegroundColor Yellow
    Write-Host "       Requires live Supabase Storage credentials. Missing required environment variable: SUPABASE_STORAGE_SERVICE_ROLE_KEY. Mock/local storage not used." -ForegroundColor Gray

    Write-Host "[NOT VERIFIED] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Yellow
    Write-Host "       Dependent on live cloud storage upload. Missing required environment variable: SUPABASE_STORAGE_SERVICE_ROLE_KEY. Mock/local storage not used." -ForegroundColor Gray

    Write-Host "`n=================================================================" -ForegroundColor Cyan
    Write-Host "TARGETED RERUN SUMMARY: 0 PASSED, 2 NOT VERIFIED, 0 FAILED" -ForegroundColor Yellow
    Write-Host "Missing required environment variable: SUPABASE_STORAGE_SERVICE_ROLE_KEY" -ForegroundColor Yellow
    Write-Host "Note: Phase 5 is NOT claimed fully cloud-verified without live credentials." -ForegroundColor Yellow
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 2
}

Write-Host "`nREAL Supabase Storage credentials detected: $supabaseUrl" -ForegroundColor Green

# 2. Start Live ASP.NET Core API
Get-Process -Name "NPTELManagement.Api" -ErrorAction SilentlyContinue | Stop-Process -Force

$apiEnv = @{
    "USE_INMEMORY_DB" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
    "PATH" = $env:PATH
    "SUPABASE_URL" = $supabaseUrl
    "SUPABASE_SERVICE_KEY" = $supabaseServiceKey
    "SUPABASE_STORAGE_SERVICE_ROLE_KEY" = $supabaseStorageKey
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = "C:\Program Files\dotnet\dotnet.exe"
$startInfo.Arguments = "run --project `"$solutionDir/Api/NPTELManagement.Api.csproj`" --urls http://127.0.0.1:5000"
$startInfo.WorkingDirectory = $solutionDir
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true

foreach ($key in $apiEnv.Keys) {
    $startInfo.EnvironmentVariables[$key] = $apiEnv[$key]
}

$apiProcess = [System.Diagnostics.Process]::Start($startInfo)
Write-Host "Started API Process (PID: $($apiProcess.Id)). Waiting for health probe..." -ForegroundColor Gray

$apiUp = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    try {
        $resp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/health" -Method Get -TimeoutSec 2 -ErrorAction Stop
        if ($resp.status -eq "ok" -and $resp.database -eq "connected") {
            $apiUp = $true
            break
        }
    } catch {}
}

if (-not $apiUp) {
    Write-Host "API failed to start on :5000." -ForegroundColor Red
    if ($apiProcess) { $apiProcess.Kill() }
    exit 1
}

$tempPdf = "$solutionDir/Desktop/test_cert_phase5_rerun.pdf"
$certId = [Guid]::Empty
$test38Passed = $false
$test310Passed = $false

try {
    # --------------------------------------------------------------------------
    # Proven Phase 4 Admin Authentication Contract
    # Route: POST /api/v1/auth/admin/login
    # Body:  { "adminId": "ADM-CSE-01", "password": "Admin@Nptel2026" }
    # --------------------------------------------------------------------------
    $adminPayload = @{ adminId = "ADM-CSE-01"; password = "Admin@Nptel2026" } | ConvertTo-Json
    $adminLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/admin/login" -Method Post -Body $adminPayload -ContentType "application/json" -ErrorAction Stop
    $adminToken = $adminLogin.data.accessToken
    $adminHeaders = @{ "Authorization" = "Bearer $adminToken" }
    Write-Host "Admin Login Successful (Role: $($adminLogin.data.role), User: $($adminLogin.data.name))`n" -ForegroundColor Gray

    # Get target registration from seeded data
    $regsResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations?pageSize=1" -Method Get -Headers $adminHeaders -ErrorAction Stop
    $targetReg = $regsResp.data.items[0]
    $regId = $targetReg.registrationId

    # Prepare binary PDF payload matching verified Phase 4 runner
    $dummyPdf = "%PDF-1.4`n1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n2 0 obj`n<< /Type /Pages /Kids [3 0 R] /Count 1 >>`nendobj`n3 0 obj`n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>`nendobj`nxref`n0 4`n0000000000 65535 f `n0000000009 00000 n `n0000000058 00000 n `n0000000115 00000 n `ntrailer`n<< /Size 4 /Root 1 0 R >>`nstartxref`n190`n%%EOF"
    [System.IO.File]::WriteAllText($tempPdf, $dummyPdf)
    $pdfBytes = [System.IO.File]::ReadAllBytes($tempPdf)

    $boundary = [System.Guid]::NewGuid().ToString()
    $contentType = "multipart/form-data; boundary=$boundary"
    $header = "--$boundary`r`nContent-Disposition: form-data; name=`"file`"; filename=`"test_cert_phase5_rerun.pdf`"`r`nContent-Type: application/pdf`r`n`r`n"
    $footer = "`r`n--$boundary--`r`n"

    $headerBytes = [System.Text.Encoding]::UTF8.GetBytes($header)
    $footerBytes = [System.Text.Encoding]::UTF8.GetBytes($footer)

    $fullBodyBytes = New-Object byte[] ($headerBytes.Length + $pdfBytes.Length + $footerBytes.Length)
    [System.Buffer]::BlockCopy($headerBytes, 0, $fullBodyBytes, 0, $headerBytes.Length)
    [System.Buffer]::BlockCopy($pdfBytes, 0, $fullBodyBytes, $headerBytes.Length, $pdfBytes.Length)
    [System.Buffer]::BlockCopy($footerBytes, 0, $fullBodyBytes, $headerBytes.Length + $pdfBytes.Length, $footerBytes.Length)

    # --------------------------------------------------------------------------
    # Test 3.8: Admin Certificate Upload to Real Private Supabase Storage
    # --------------------------------------------------------------------------
    Write-Host "Executing Test 3.8: Admin Certificate Upload..." -ForegroundColor Cyan
    try {
        $uploadResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$regId/upload" -Method Post -Body $fullBodyBytes -ContentType $contentType -Headers $adminHeaders -ErrorAction Stop
        if ($uploadResp.data -and $uploadResp.data.certificateId -and $uploadResp.data.storagePath -match '^certificates\/') {
            $certId = [Guid]$uploadResp.data.certificateId
            $test38Passed = $true
            Write-Host "[PASS] 3.8 Admin Certificate Upload to Cloud Storage" -ForegroundColor Green
            Write-Host "       Uploaded successfully to private storage. CertificateId: $certId, StoragePath: $($uploadResp.data.storagePath)" -ForegroundColor Gray
        } else {
            Write-Host "[FAIL] 3.8 Admin Certificate Upload to Cloud Storage" -ForegroundColor Red
            Write-Host "       Upload returned unexpected structure: $($uploadResp | ConvertTo-Json -Compress)" -ForegroundColor Yellow
        }
    } catch {
        $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey $supabaseStorageKey
        Write-Host "[FAIL] 3.8 Admin Certificate Upload to Cloud Storage" -ForegroundColor Red
        Write-Host "       Upload error: $($safeErr.SafeMessage)" -ForegroundColor Yellow
    }

    # --------------------------------------------------------------------------
    # Test 3.10: Admin Signed URL & %PDF Validation
    # --------------------------------------------------------------------------
    Write-Host "`nExecuting Test 3.10: Admin Signed URL & %PDF Validation..." -ForegroundColor Cyan
    if ($test38Passed -and $certId -ne [Guid]::Empty) {
        try {
            $accessResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/$certId/access" -Method Get -Headers $adminHeaders -ErrorAction Stop
            $signedUrl = $accessResp.data.accessUrl

            if ($signedUrl -and $signedUrl.StartsWith("https://") -and $signedUrl -match 'token=') {
                $downloadResp = Invoke-WebRequest -Uri $signedUrl -Method Get -UseBasicParsing -ErrorAction Stop
                $downloadedBytes = $downloadResp.Content

                $isPdf = $false
                if ($null -ne $downloadedBytes -and $downloadedBytes.Length -ge 4) {
                    if ($downloadedBytes -is [byte[]]) {
                        $isPdf = ([System.Text.Encoding]::ASCII.GetString($downloadedBytes[0..3]) -eq "%PDF")
                    } else {
                        $isPdf = ($downloadedBytes.ToString().StartsWith("%PDF"))
                    }
                }

                if ($isPdf) {
                    $test310Passed = $true
                    Write-Host "[PASS] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Green
                    Write-Host "       Signed URL verified. Downloaded $($downloadedBytes.Length) bytes with valid %PDF magic header" -ForegroundColor Gray
                } else {
                    Write-Host "[FAIL] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Red
                    Write-Host "       Downloaded content did not start with %PDF header" -ForegroundColor Yellow
                }
            } else {
                Write-Host "[FAIL] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Red
                Write-Host "       Invalid or unsigned URL returned from access endpoint" -ForegroundColor Yellow
            }
        } catch {
            $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey $supabaseStorageKey
            Write-Host "[FAIL] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Red
            Write-Host "       Signed URL download error: $($safeErr.SafeMessage)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[NOT VERIFIED] 3.10 Admin Signed URL & %PDF Validation" -ForegroundColor Yellow
        Write-Host "       Skipped because 3.8 was not successful." -ForegroundColor Yellow
    }

} catch {
    $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey $supabaseStorageKey
    Write-Host "`nCRITICAL ERROR in Targeted Runner: $($safeErr.SafeMessage)" -ForegroundColor Red
} finally {
    if (Test-Path $tempPdf) { Remove-Item $tempPdf -Force }
    if ($apiProcess -and -not $apiProcess.HasExited) {
        $apiProcess.Kill()
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
if ($test38Passed -and $test310Passed) {
    Write-Host "TARGETED RERUN SUMMARY: 2 PASSED, 0 NOT VERIFIED, 0 FAILED" -ForegroundColor Green
    Write-Host "All targeted cloud tests passed cleanly against real Supabase private storage." -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "TARGETED RERUN SUMMARY: 3.8 Passed: $test38Passed, 3.10 Passed: $test310Passed" -ForegroundColor Yellow
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 1
}
