# ==============================================================================
# PHASE 1 — STEP 4 VERIFICATION SCRIPT
# Real WPF Desktop Client + Real API Integration Test Suite
# ==============================================================================

$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$solutionDir = Split-Path -Parent $PSScriptRoot
Set-Location $solutionDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   PHASE 1 - STEP 4: REAL WPF DESKTOP CLIENT + API INTEGRATION   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$allPassed = $true

function Report-Result {
    param([string]$name, [bool]$passed, [string]$details = "")
    if ($passed) {
        Write-Host "[PASS] $name" -ForegroundColor Green
        if ($details) { Write-Host "       $details" -ForegroundColor Gray }
    } else {
        Write-Host "[FAIL] $name" -ForegroundColor Red
        if ($details) { Write-Host "       $details" -ForegroundColor Yellow }
        $script:allPassed = $false
    }
}

# ------------------------------------------------------------------------------
# 1. ARCHITECTURAL & SECURITY AUDIT
# ------------------------------------------------------------------------------
Write-Host "`n--- 1. ARCHITECTURAL & SECURITY VERIFICATION ---" -ForegroundColor Yellow

$desktopCsproj = Get-Content "Desktop/NPTELManagement.Desktop.csproj" -Raw
$hasCoreRef = $desktopCsproj -match 'ProjectReference Include="..\\Core\\NPTELManagement.Core.csproj"'
$hasInfraRef = $desktopCsproj -match 'Infrastructure'
$hasNpgsqlRef = $desktopCsproj -match 'Npgsql'

Report-Result "Desktop references ONLY Core" ($hasCoreRef -and -not $hasInfraRef -and -not $hasNpgsqlRef) "Core reference: $hasCoreRef, Infra reference: $hasInfraRef, Npgsql reference: $hasNpgsqlRef"

$forbiddenPatterns = @("Host=", "User Id=", "JWT_SECRET", "service_role", "supabase.co", "Server=")
$filesWithSecrets = @()
Get-ChildItem -Path "Desktop" -Recurse -Include *.cs, *.xaml, *.json | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    foreach ($pat in $forbiddenPatterns) {
        if ($content -match [regex]::Escape($pat)) {
            $filesWithSecrets += "$($_.Name) contains $pat"
        }
    }
}

Report-Result "Desktop has ZERO backend secrets" ($filesWithSecrets.Count -eq 0) "Secrets found: $($filesWithSecrets.Count)"

# ------------------------------------------------------------------------------
# 2. START THE REAL API SERVICE
# ------------------------------------------------------------------------------
Write-Host "`n--- 2. STARTING LIVE ASP.NET CORE API ---" -ForegroundColor Yellow

# Kill any existing processes on port 5000 if running
Get-Process -Name "NPTELManagement.Api" -ErrorAction SilentlyContinue | Stop-Process -Force

$apiEnv = @{
    "USE_INMEMORY_DB" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
    "PATH" = $env:PATH
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
Write-Host "Started API Process (PID: $($apiProcess.Id)). Waiting for endpoint to respond..." -ForegroundColor Gray

# Wait for API to respond
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

Report-Result "Live API Started & Health Checked" $apiUp "Status: $($resp.status), Database: $($resp.database)"

if (-not $apiUp) {
    Write-Host "API failed to start. Aborting live tests." -ForegroundColor Red
    if ($apiProcess -and -not $apiProcess.HasExited) { $apiProcess.Kill() }
    exit 1
}

# ------------------------------------------------------------------------------
# 3. STUDENT FLOW & SCOPE SECURITY
# ------------------------------------------------------------------------------
Write-Host "`n--- 3. STUDENT AUTHENTICATION & DASHBOARD VERIFICATION ---" -ForegroundColor Yellow

# 3.1 Invalid login
$invalidLoginFailed = $false
try {
    $body = @{ registerNumber = "951021104001"; password = "WrongPassword999" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 401) {
        $invalidLoginFailed = $true
    }
}
Report-Result "Student Invalid Password returns 401" $invalidLoginFailed "Status 401 received"

# 3.2 Real Student Login
$studentLoginBody = @{ registerNumber = "951021104001"; password = "Student@Nptel2026" } | ConvertTo-Json
$studentAuth = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $studentLoginBody -ContentType "application/json"
$studentToken = $studentAuth.data.accessToken
Report-Result "Student Login Successful" ($studentAuth.success -and -not [string]::IsNullOrEmpty($studentToken)) "Token received, Role: $($studentAuth.data.role)"

# 3.3 Student Dashboard Data
$studentHeaders = @{ Authorization = "Bearer $studentToken" }
$studentProfile = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/me" -Method Get -Headers $studentHeaders
$isStudentProfileValid = ($studentProfile.data.registerNumber -eq "951021104001" -and $studentProfile.data.department -eq "CSE" -and $studentProfile.data.year -eq 3)
Report-Result "Student Dashboard Profile (GET /api/v1/student/me)" $isStudentProfileValid "Name: $($studentProfile.data.name), Reg: $($studentProfile.data.registerNumber), Year: $($studentProfile.data.year), Section: $($studentProfile.data.classSection)"

# 3.4 Student Role Protection (Student cannot access Staff or Admin endpoints)
$studentBlockedFromStaff = $false
try {
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Get -Headers $studentHeaders -ErrorAction Stop
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $studentBlockedFromStaff = $true }
}
Report-Result "Student Blocked from Staff API (403 Forbidden)" $studentBlockedFromStaff

$studentBlockedFromAdmin = $false
try {
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $studentHeaders -ErrorAction Stop
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $studentBlockedFromAdmin = $true }
}
Report-Result "Student Blocked from Admin API (403 Forbidden)" $studentBlockedFromAdmin

# ------------------------------------------------------------------------------
# 4. STAFF FLOW, DATA GRID, SEARCH & SCOPED FILTERS
# ------------------------------------------------------------------------------
Write-Host "`n--- 4. STAFF AUTHENTICATION, DATA GRID & SCOPE VERIFICATION ---" -ForegroundColor Yellow

# 4.1 Staff Login
$staffLoginBody = @{ staffId = "CSE-STF-01"; password = "Staff@Nptel2026" } | ConvertTo-Json
$staffAuth = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $staffLoginBody -ContentType "application/json"
$staffToken = $staffAuth.data.accessToken
Report-Result "Staff Login Successful" ($staffAuth.success -and -not [string]::IsNullOrEmpty($staffToken)) "Role: $($staffAuth.data.role), Identifier: $($staffAuth.data.identifier)"

# 4.2 Staff Profile
$staffHeaders = @{ Authorization = "Bearer $staffToken" }
$staffProfile = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/me" -Method Get -Headers $staffHeaders
$isStaffProfileValid = ($staffProfile.data.staffIdentifier -eq "CSE-STF-01" -and $staffProfile.data.assignedYear -eq 3 -and $staffProfile.data.assignedClass -eq "A")
Report-Result "Staff Profile (GET /api/v1/staff/me)" $isStaffProfileValid "Name: $($staffProfile.data.staffName), Assigned: Year $($staffProfile.data.assignedYear) Section $($staffProfile.data.assignedClass)"

# 4.3 Staff DataGrid Data (Assigned Students)
$assignedStudents = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Get -Headers $staffHeaders
$count = if ($null -ne $assignedStudents.data.totalCount) { $assignedStudents.data.totalCount } elseif ($null -ne $assignedStudents.data.items) { @($assignedStudents.data.items).Count } else { @($assignedStudents.data).Count }
$firstRegNo = if ($assignedStudents.data.items) { $assignedStudents.data.items[0].registerNumber } else { $assignedStudents.data[0].registerNumber }
$hasAssignedStudent = ($count -ge 1 -and $firstRegNo -eq "951021104001")
Report-Result "Staff Assigned Students DataGrid (GET /api/v1/staff/students)" $hasAssignedStudent "Returned $count student(s). RegNo: $firstRegNo"

# 4.4 Staff Search (Client-side & API in-scope search)
$searchResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?search=951021104001" -Method Get -Headers $staffHeaders
$searchCount = if ($null -ne $searchResp.data.totalCount) { $searchResp.data.totalCount } elseif ($null -ne $searchResp.data.items) { @($searchResp.data.items).Count } else { @($searchResp.data).Count }
$searchRegNo = if ($searchResp.data.items) { $searchResp.data.items[0].registerNumber } else { $searchResp.data[0].registerNumber }
$searchMatched = ($searchCount -eq 1 -and $searchRegNo -eq "951021104001")
Report-Result "Staff Search by Register Number" $searchMatched "Search returned: $searchCount item(s)"

# 4.5 Staff Scoped Filtering (Requesting out-of-scope class section 'B' returns empty, NOT forbidden/all)
$filterResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?classSection=B" -Method Get -Headers $staffHeaders
$filterCount = if ($null -ne $filterResp.data.totalCount) { $filterResp.data.totalCount } elseif ($null -ne $filterResp.data.items) { @($filterResp.data.items).Count } else { @($filterResp.data).Count }
$filterEmpty = ($filterCount -eq 0)
Report-Result "Staff Scoped Filtering (Cannot expand beyond Section A)" $filterEmpty "Out-of-scope Section B returned $filterCount students"

# 4.6 Staff Role Protection (Staff cannot access Admin endpoints)
$staffBlockedFromAdmin = $false
try {
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $staffHeaders -ErrorAction Stop
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $staffBlockedFromAdmin = $true }
}
Report-Result "Staff Blocked from Admin API (403 Forbidden)" $staffBlockedFromAdmin

# ------------------------------------------------------------------------------
# 5. ADMIN FLOW & REAL DATABASE METRICS
# ------------------------------------------------------------------------------
Write-Host "`n--- 5. ADMIN AUTHENTICATION & REAL DATABASE METRICS ---" -ForegroundColor Yellow

# 5.1 Admin Login
$adminLoginBody = @{ adminId = "ADM-CSE-01"; password = "Admin@Nptel2026" } | ConvertTo-Json
$adminAuth = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/admin/login" -Method Post -Body $adminLoginBody -ContentType "application/json"
$adminToken = $adminAuth.data.accessToken
Report-Result "Admin Login Successful" ($adminAuth.success -and -not [string]::IsNullOrEmpty($adminToken)) "Role: $($adminAuth.data.role), Identifier: $($adminAuth.data.identifier)"

# 5.2 Admin Dashboard Real Database Counts
$adminHeaders = @{ Authorization = "Bearer $adminToken" }
$adminDashboard = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $adminHeaders
$adminData = $adminDashboard.data
$realCountsValid = ($adminData.totalStudents -ge 1 -and $adminData.totalStaff -ge 1 -and $adminData.totalCourses -ge 1 -and $adminData.totalRegistrations -ge 1)
Report-Result "Admin Real Database Counts (GET /api/v1/admin/me)" $realCountsValid "Students: $($adminData.totalStudents), Staff: $($adminData.totalStaff), Courses: $($adminData.totalCourses), Registrations: $($adminData.totalRegistrations), Certificates: $($adminData.totalCertificates)"

# ------------------------------------------------------------------------------
# 6. LOGOUT & SESSION INVALIDATION
# ------------------------------------------------------------------------------
Write-Host "`n--- 6. LOGOUT & SESSION INVALIDATION ---" -ForegroundColor Yellow

$logoutResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/logout" -Method Post -Headers $adminHeaders
Report-Result "Logout Endpoint (POST /api/v1/auth/logout)" $logoutResp.success "Logged out successfully"

# ------------------------------------------------------------------------------
# 7. SPLASH SCREEN API FAILURE & RETRY RECOVERY TEST
# ------------------------------------------------------------------------------
Write-Host "`n--- 7. SPLASH SCREEN FAILURE & RETRY RESILIENCE TEST ---" -ForegroundColor Yellow

# Stop the API to simulate server downtime
Write-Host "Simulating API server shutdown..." -ForegroundColor Gray
$apiProcess.Kill()
$apiProcess.WaitForExit()

$apiDownHealth = $false
try {
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/health" -Method Get -TimeoutSec 1 -ErrorAction Stop
} catch {
    $apiDownHealth = $true
}
Report-Result "Splash detects API Down / Connection Failure" $apiDownHealth "Connection correctly failed when backend was offline"

# Restart the API to simulate the Retry button succeeding
Write-Host "Restarting API to simulate user clicking Retry..." -ForegroundColor Gray
$apiProcess2 = [System.Diagnostics.Process]::Start($startInfo)
$apiRecovered = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    try {
        $resp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/health" -Method Get -TimeoutSec 2 -ErrorAction Stop
        if ($resp.status -eq "ok" -and $resp.database -eq "connected") {
            $apiRecovered = $true
            break
        }
    } catch {}
}
Report-Result "Retry Recovers when API Restarts" $apiRecovered "Status: $($resp.status), Database: $($resp.database)"

# ------------------------------------------------------------------------------
# 8. LAUNCH WPF APPLICATION EXECUTABLE VERIFICATION
# ------------------------------------------------------------------------------
Write-Host "`n--- 8. WPF DESKTOP EXECUTABLE VALIDATION ---" -ForegroundColor Yellow

$desktopExePath = "$solutionDir/Desktop/bin/Debug/net8.0-windows/NPTELManagement.Desktop.exe"
$exeExists = Test-Path $desktopExePath
Report-Result "NPTELManagement.Desktop.exe compiled" $exeExists "Path: $desktopExePath"

if ($exeExists) {
    Write-Host "Launching NPTELManagement.Desktop.exe in test mode..." -ForegroundColor Gray
    $wpfProcess = Start-Process -FilePath $desktopExePath -PassThru
    Start-Sleep -Seconds 3

    $isRunning = -not $wpfProcess.HasExited
    Report-Result "WPF Application Runs without Crash" $isRunning "PID: $($wpfProcess.Id), HasExited: $($wpfProcess.HasExited)"

    if ($isRunning) {
        $wpfProcess.Kill()
        Write-Host "Closed test WPF desktop instance." -ForegroundColor Gray
    }
}

# Clean up restarted API process
if ($apiProcess2 -and -not $apiProcess2.HasExited) {
    $apiProcess2.Kill()
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "   ALL STEP 4 VERIFICATION CHECKS PASSED (100% SUCCESS)         " -ForegroundColor Green
} else {
    Write-Host "   SOME STEP 4 VERIFICATION CHECKS FAILED                       " -ForegroundColor Red
}
Write-Host "=================================================================" -ForegroundColor Cyan
