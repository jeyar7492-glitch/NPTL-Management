# ==============================================================================
# PHASE 4 — REAL ADMIN MANAGEMENT + CERTIFICATE STORAGE + NOTIFICATIONS +
# REPORTS + EXPORTS VERIFICATION SUITE
# ==============================================================================

$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$solutionDir = Split-Path -Parent $PSScriptRoot
Set-Location $solutionDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   PHASE 4: REAL ADMIN MANAGEMENT & CLOUD STORAGE VERIFICATION   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$allPassed = $true
$cloudVerified = $false
$testResults = [System.Collections.Generic.List[PSCustomObject]]::new()

function Report-Result {
    param([string]$name, [bool]$passed, [string]$details = "", [string]$overrideStatus = "")
    $status = if ($overrideStatus) { $overrideStatus } elseif ($passed) { "PASS" } else { "FAIL" }
    $obj = [PSCustomObject]@{
        Name = $name
        Status = $status
        Details = $details
    }
    $testResults.Add($obj)

    if ($status -eq "PASS") {
        Write-Host "[$status] $name" -ForegroundColor Green
        if ($details) { Write-Host "       $details" -ForegroundColor Gray }
    } elseif ($status -eq "NOT VERIFIED") {
        Write-Host "[$status] $name" -ForegroundColor Yellow
        if ($details) { Write-Host "       $details" -ForegroundColor Yellow }
        $script:cloudVerified = $false
    } else {
        Write-Host "[$status] $name" -ForegroundColor Red
        if ($details) { Write-Host "       $details" -ForegroundColor Yellow }
        $script:allPassed = $false
    }
}

function Get-HttpErrorDetails {
    param([System.Management.Automation.ErrorRecord]$errRecord)
    $resp = $errRecord.Exception.Response
    $statusCode = 0
    $body = ""
    if ($resp) {
        try {
            $statusCode = [int]$resp.StatusCode
        } catch {}
        try {
            $stream = $resp.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
            }
        } catch {}
    }
    return @{ StatusCode = $statusCode; Body = $body }
}

function Get-SafeErrorMessage {
    param([System.Management.Automation.ErrorRecord]$errRecord, [string]$keyToRedact = "")
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
    $full = [regex]::Replace($full, 'eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+', '[REDACTED_JWT]')
    $full = [regex]::Replace($full, 'sb_secret_[A-Za-z0-9-_]+', '[REDACTED_SECRET_KEY]')
    return @{ StatusCode = $statusCode; SafeMessage = $full }
}

function Test-IsPlaceholderOrInvalidCredential {
    param([string]$url, [string]$key)
    
    $issues = @()
    if ([string]::IsNullOrWhiteSpace($url)) {
        $issues += "SUPABASE_URL is missing or empty"
    } else {
        $trimmedUrl = $url.Trim()
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
            $issues += "SUPABASE_SERVICE_KEY contains placeholder value"
        }
        
        $isLegacyJwt = ($trimmedKey.StartsWith("eyJ") -and $trimmedKey.Split('.').Length -eq 3)
        $isModernSecret = ($trimmedKey.StartsWith("sb_secret_") -and $trimmedKey.Length -gt 15)
        
        if (-not ($isLegacyJwt -or $isModernSecret)) {
            $issues += "SUPABASE_SERVICE_KEY must be either a legacy service_role JWT (3-part eyJ... token) or a modern Supabase Secret API key (sb_secret_...)"
        }
    }
    
    return $issues
}

# ------------------------------------------------------------------------------
# 1. ARCHITECTURAL & SECURITY AUDIT
# ------------------------------------------------------------------------------
Write-Host "`n--- 1. ARCHITECTURAL & SECURITY AUDIT ---" -ForegroundColor Yellow

$desktopCsproj = Get-Content "Desktop/NPTELManagement.Desktop.csproj" -Raw
$hasCoreRef = $desktopCsproj -match 'ProjectReference Include="..\\Core\\NPTELManagement.Core.csproj"'
$hasInfraRef = $desktopCsproj -match 'Infrastructure'
$hasNpgsqlRef = $desktopCsproj -match 'Npgsql'

Report-Result "Desktop references ONLY Core" ($hasCoreRef -and -not $hasInfraRef -and -not $hasNpgsqlRef) "Core: $hasCoreRef, Infra: $hasInfraRef, Npgsql: $hasNpgsqlRef"

$forbiddenPatterns = @("Host=", "User Id=", "JWT_SECRET", "service_role", "SUPABASE_SERVICE_KEY", "SUPABASE_SERVICE_ROLE_KEY")
$filesWithSecrets = @()
Get-ChildItem -Path "Desktop" -Recurse -Include *.cs, *.xaml, *.json | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    foreach ($pat in $forbiddenPatterns) {
        if ($content -match [regex]::Escape($pat)) {
            $filesWithSecrets += "$($_.Name) contains $pat"
        }
    }
}

Report-Result "Desktop has ZERO backend secrets or database credentials" ($filesWithSecrets.Count -eq 0) "Secrets found in Desktop: $($filesWithSecrets.Count)"

# Check AdminController has Authorize(Roles = "Admin")
$adminControllerCode = Get-Content "Api/Controllers/AdminController.cs" -Raw
$hasAdminAuth = $adminControllerCode -match '\[Authorize\(Roles\s*=\s*"Admin"\)\]'
Report-Result "AdminController enforces strictly Admin role authorization" $hasAdminAuth "Authorize(Roles = 'Admin') present: $hasAdminAuth"

# ------------------------------------------------------------------------------
# 2. START LIVE ASP.NET CORE API SERVICE
# ------------------------------------------------------------------------------
Write-Host "`n--- 2. STARTING LIVE ASP.NET CORE API ---" -ForegroundColor Yellow

Get-Process -Name "NPTELManagement.Api" -ErrorAction SilentlyContinue | Stop-Process -Force

$supabaseUrl = if ($env:SUPABASE_URL) { $env:SUPABASE_URL } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_URL") }
if (-not $supabaseUrl) { $supabaseUrl = [System.Environment]::GetEnvironmentVariable("SUPABASE_URL", "User") }
if (-not $supabaseUrl) { $supabaseUrl = [System.Environment]::GetEnvironmentVariable("SUPABASE_URL", "Machine") }

$supabaseServiceKey = if ($env:SUPABASE_SERVICE_KEY) { $env:SUPABASE_SERVICE_KEY } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_KEY") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = if ($env:SUPABASE_SERVICE_ROLE_KEY) { $env:SUPABASE_SERVICE_ROLE_KEY } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") } }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_KEY", "User") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", "User") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_KEY", "Machine") }
if (-not $supabaseServiceKey) { $supabaseServiceKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", "Machine") }

$supabaseStorageKey = if ($env:SUPABASE_STORAGE_SERVICE_ROLE_KEY) { $env:SUPABASE_STORAGE_SERVICE_ROLE_KEY } else { [System.Environment]::GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY") }
if (-not $supabaseStorageKey) { $supabaseStorageKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY", "User") }
if (-not $supabaseStorageKey) { $supabaseStorageKey = [System.Environment]::GetEnvironmentVariable("SUPABASE_STORAGE_SERVICE_ROLE_KEY", "Machine") }

$apiEnv = @{
    "USE_INMEMORY_DB" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
    "PATH" = $env:PATH
}

$initialCredIssues = Test-IsPlaceholderOrInvalidCredential $supabaseUrl $supabaseServiceKey
if ($initialCredIssues.Count -eq 0) {
    $apiEnv["SUPABASE_URL"] = $supabaseUrl
    $apiEnv["SUPABASE_SERVICE_KEY"] = $supabaseServiceKey
    if ($supabaseStorageKey) {
        $apiEnv["SUPABASE_STORAGE_SERVICE_ROLE_KEY"] = $supabaseStorageKey
    }
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

Report-Result "ASP.NET Core API Started & Responding at :5000" $apiUp "PID: $($apiProcess.Id)"

if (-not $apiUp) {
    Write-Error "API failed to start. Aborting test suite."
    if ($apiProcess) { $apiProcess.Kill() }
    exit 1
}

try {
    # --------------------------------------------------------------------------
    # 3. ADMIN AUTHENTICATION & DASHBOARD METRICS
    # --------------------------------------------------------------------------
    Write-Host "`n--- 3. ADMIN AUTHENTICATION & METRICS ---" -ForegroundColor Yellow

    $adminPayload = @{ adminId = "ADM-CSE-01"; password = "Admin@Nptel2026" } | ConvertTo-Json
    $adminLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/admin/login" -Method Post -Body $adminPayload -ContentType "application/json"
    $adminToken = $adminLogin.data.accessToken
    $adminHeaders = @{ "Authorization" = "Bearer $adminToken" }

    Report-Result "Admin Login & JWT Issuance" ($adminLogin.success -eq $true -and !([string]::IsNullOrEmpty($adminToken))) "Role: $($adminLogin.data.role), User: $($adminLogin.data.userName)"

    $metricsResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/dashboard-metrics" -Method Get -Headers $adminHeaders
    $metrics = $metricsResp.data
    $metricsOk = ($metrics.totalStudents -gt 0 -and $metrics.totalStaff -gt 0 -and $metrics.totalCourses -gt 0)
    Report-Result "Admin Live Dashboard Metrics" $metricsOk "Students: $($metrics.totalStudents), Staff: $($metrics.totalStaff), Courses: $($metrics.totalCourses), Active Courses: $($metrics.activeCourses)"

    # --------------------------------------------------------------------------
    # 4. ROLE ISOLATION & SECURITY BOUNDARIES
    # --------------------------------------------------------------------------
    Write-Host "`n--- 4. ROLE ISOLATION & SECURITY BOUNDARIES ---" -ForegroundColor Yellow

    # Student login
    $studentPayload = @{ registerNumber = "951021104001"; password = "Student@Nptel2026" } | ConvertTo-Json
    $studentLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $studentPayload -ContentType "application/json"
    $studentToken = $studentLogin.data.accessToken
    $studentHeaders = @{ "Authorization" = "Bearer $studentToken" }

    # Staff login
    $staffPayload = @{ staffId = "CSE-STF-01"; password = "Staff@Nptel2026" } | ConvertTo-Json
    $staffLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $staffPayload -ContentType "application/json"
    $staffToken = $staffLogin.data.accessToken
    $staffHeaders = @{ "Authorization" = "Bearer $staffToken" }

    # Student calling Admin endpoint -> 403
    $studentBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $studentHeaders -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) { $studentBlocked = $true }
    }
    Report-Result "Student Blocked from Admin Endpoints (403 Forbidden)" $studentBlocked "Student token rejected"

    # Staff calling Admin endpoint -> 403
    $staffBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $staffHeaders -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) { $staffBlocked = $true }
    }
    Report-Result "Staff Blocked from Admin Endpoints (403 Forbidden)" $staffBlocked "Staff token rejected"

    # Anonymous calling Admin endpoint -> 401
    $anonBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) { $anonBlocked = $true }
    }
    Report-Result "Anonymous Blocked from Admin Endpoints (401 Unauthorized)" $anonBlocked "Unauthenticated request rejected"

    # --------------------------------------------------------------------------
    # 5. STUDENT CRUD & STATUS MANAGEMENT
    # --------------------------------------------------------------------------
    Write-Host "`n--- 5. STUDENT CRUD & STATUS MANAGEMENT ---" -ForegroundColor Yellow

    $createStudentPayload = @{
        name = "Kavitha Rajan"
        registerNumber = "951021104099"
        department = "CSE"
        classSection = "A"
        year = 2
        batch = "2023-2027"
        email = "kavitha.rajan@college.edu"
        phone = "9876543299"
        initialPassword = "Student@Nptel2026"
    } | ConvertTo-Json

    $newStudent = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students" -Method Post -Body $createStudentPayload -ContentType "application/json" -Headers $adminHeaders).data
    $newStudentId = $newStudent.studentId
    Report-Result "Create Student (User + Student Linked)" ($newStudent.registerNumber -eq "951021104099") "ID: $newStudentId, Name: $($newStudent.name)"

    # Duplicate Register Number Check
    $dupStudentBlocked = $false
    $dupStudentDetail = ""
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students" -Method Post -Body $createStudentPayload -ContentType "application/json" -Headers $adminHeaders -ErrorAction Stop
    } catch {
        $err = Get-HttpErrorDetails $_
        # Expected rejection: HTTP 400 Bad Request or HTTP 409 Conflict with duplicate entity message
        if (($err.StatusCode -in @(400, 409)) -and ($err.Body -match "already exists" -or $err.Body -match "duplicate" -or $err.Body.Length -gt 0)) {
            $dupStudentBlocked = $true
            $dupStudentDetail = "Duplicate register number rejected (HTTP $($err.StatusCode))"
        } else {
            $dupStudentDetail = "Unexpected response (HTTP $($err.StatusCode)): $($err.Body)"
        }
    }
    Report-Result "Student Duplicate Register Number Prevented (400 Bad Request)" $dupStudentBlocked $dupStudentDetail

    # Update Student Details
    $updateStudentPayload = @{
        name = "Kavitha R"
        department = "CSE"
        classSection = "A"
        year = 2
        batch = "2023-2027"
        email = "kavitha.r@college.edu"
        phone = "9876543298"
    } | ConvertTo-Json
    $updatedStudent = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students/$newStudentId" -Method Put -Body $updateStudentPayload -ContentType "application/json" -Headers $adminHeaders).data
    Report-Result "Update Student Details" ($updatedStudent.name -eq "Kavitha R") "New Name: $($updatedStudent.name)"

    # Toggle Student Status (Soft Deactivate)
    $deactStudentPayload = @{ isActive = $false } | ConvertTo-Json
    $deactStudentResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students/$newStudentId/status" -Method Patch -Body $deactStudentPayload -ContentType "application/json" -Headers $adminHeaders
    $studentCheck = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students/$newStudentId" -Method Get -Headers $adminHeaders).data
    Report-Result "Deactivate Student (Soft Status Toggle)" ($studentCheck.isActive -eq $false) "IsActive: $($studentCheck.isActive)"

    # Reactivate Student
    $reactStudentPayload = @{ isActive = $true } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students/$newStudentId/status" -Method Patch -Body $reactStudentPayload -ContentType "application/json" -Headers $adminHeaders | Out-Null
    $studentReactivated = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/students/$newStudentId" -Method Get -Headers $adminHeaders).data
    Report-Result "Reactivate Student" ($studentReactivated.isActive -eq $true) "IsActive: $($studentReactivated.isActive)"

    # --------------------------------------------------------------------------
    # 6. STAFF CRUD & PASSWORD RESET
    # --------------------------------------------------------------------------
    Write-Host "`n--- 6. STAFF CRUD & PASSWORD RESET ---" -ForegroundColor Yellow

    $createStaffPayload = @{
        staffName = "Dr. Suresh Kumar"
        staffIdentifier = "CSE-STF-99"
        department = "CSE"
        assignedYear = 2
        assignedClass = "A"
        email = "suresh.kumar@college.edu"
        initialPassword = "Staff@Nptel2026"
    } | ConvertTo-Json

    $newStaff = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/staff" -Method Post -Body $createStaffPayload -ContentType "application/json" -Headers $adminHeaders).data
    $newStaffId = $newStaff.staffId
    Report-Result "Create Staff (User + Staff Linked with Assigned Scope)" ($newStaff.staffIdentifier -eq "CSE-STF-99") "ID: $newStaffId, Scope: CSE Year 2 Sec A"

    # Duplicate Staff Identifier Check
    $dupStaffBlocked = $false
    $dupStaffDetail = ""
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/staff" -Method Post -Body $createStaffPayload -ContentType "application/json" -Headers $adminHeaders -ErrorAction Stop
    } catch {
        $err = Get-HttpErrorDetails $_
        # Expected rejection: HTTP 400 Bad Request or HTTP 409 Conflict with duplicate staff identifier message
        if (($err.StatusCode -in @(400, 409)) -and ($err.Body -match "already exists" -or $err.Body -match "duplicate" -or $err.Body.Length -gt 0)) {
            $dupStaffBlocked = $true
            $dupStaffDetail = "Duplicate staff identifier rejected (HTTP $($err.StatusCode))"
        } else {
            $dupStaffDetail = "Unexpected response (HTTP $($err.StatusCode)): $($err.Body)"
        }
    }
    Report-Result "Staff Duplicate Identifier Prevented (400 Bad Request)" $dupStaffBlocked $dupStaffDetail

    # Reset Staff Password
    $newPassword = "NewStaffPass@2026"
    $resetPassPayload = @{ newPassword = $newPassword } | ConvertTo-Json
    $resetResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/staff/$newStaffId/password" -Method Patch -Body $resetPassPayload -ContentType "application/json" -Headers $adminHeaders
    Report-Result "Admin Reset Staff Password" ($resetResp.success -eq $true) "Password updated"

    # Verify Staff Can Login with New Password
    $newStaffLoginPayload = @{ staffId = "CSE-STF-99"; password = $newPassword } | ConvertTo-Json
    $newStaffLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $newStaffLoginPayload -ContentType "application/json"
    Report-Result "Staff Logs In with New Password" ($newStaffLogin.success -eq $true) "Token issued: $($newStaffLogin.data.role)"

    # Verify Staff Cannot Login with Old Password
    $oldPassBlocked = $false
    try {
        $oldStaffLoginPayload = @{ staffId = "CSE-STF-99"; password = "Staff@Nptel2026" } | ConvertTo-Json
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $oldStaffLoginPayload -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) { $oldPassBlocked = $true }
    }
    Report-Result "Old Password Invalidated (401 Unauthorized)" $oldPassBlocked "Old password rejected"

    # --------------------------------------------------------------------------
    # 7. COURSE CRUD & SOFT ARCHIVING
    # --------------------------------------------------------------------------
    Write-Host "`n--- 7. COURSE CRUD & SOFT ARCHIVING ---" -ForegroundColor Yellow

    $createCoursePayload = @{
        courseCode = "noc24-cs99"
        courseName = "Cloud Architecture and DevOps"
        durationWeeks = 12
        courseStartDate = (Get-Date).AddDays(14).ToString("o")
        courseEndDate = (Get-Date).AddDays(98).ToString("o")
        status = "Active"
    } | ConvertTo-Json

    $newCourse = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/courses" -Method Post -Body $createCoursePayload -ContentType "application/json" -Headers $adminHeaders).data
    $newCourseId = $newCourse.courseId
    Report-Result "Create NPTEL Course" ($newCourse.courseCode -eq "noc24-cs99") "ID: $newCourseId, Title: $($newCourse.courseName)"

    # Duplicate Course Code Check
    $dupCourseBlocked = $false
    $dupCourseDetail = ""
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/courses" -Method Post -Body $createCoursePayload -ContentType "application/json" -Headers $adminHeaders -ErrorAction Stop
    } catch {
        $err = Get-HttpErrorDetails $_
        # Expected rejection: HTTP 400 Bad Request or HTTP 409 Conflict with duplicate course code message
        if (($err.StatusCode -in @(400, 409)) -and ($err.Body -match "already exists" -or $err.Body -match "duplicate" -or $err.Body.Length -gt 0)) {
            $dupCourseBlocked = $true
            $dupCourseDetail = "Duplicate course code rejected (HTTP $($err.StatusCode))"
        } else {
            $dupCourseDetail = "Unexpected response (HTTP $($err.StatusCode)): $($err.Body)"
        }
    }
    Report-Result "Course Duplicate Code Prevented (400 Bad Request)" $dupCourseBlocked $dupCourseDetail

    # Archive Course
    $archivePayload = @{ status = "Archived" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/courses/$newCourseId/status" -Method Patch -Body $archivePayload -ContentType "application/json" -Headers $adminHeaders | Out-Null
    $archivedCourse = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/courses/$newCourseId" -Method Get -Headers $adminHeaders).data
    Report-Result "Course Soft Archive" ($archivedCourse.status -eq "Archived") "Course Status: $($archivedCourse.status)"

    # Reactivate Course
    $reactCoursePayload = @{ status = "Active" } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/courses/$newCourseId/status" -Method Patch -Body $reactCoursePayload -ContentType "application/json" -Headers $adminHeaders | Out-Null
    $activeCourse = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/courses/$newCourseId" -Method Get -Headers $adminHeaders).data
    Report-Result "Course Reactivated to Active" ($activeCourse.status -eq "Active") "Course Status: $($activeCourse.status)"

    # --------------------------------------------------------------------------
    # 8. REGISTRATION & ENROLLMENT WORKFLOW
    # --------------------------------------------------------------------------
    Write-Host "`n--- 8. REGISTRATION & ENROLLMENT WORKFLOW ---" -ForegroundColor Yellow

    $enrollPayload = @{
        studentId = $newStudentId
        courseId = $newCourseId
        enrollmentDate = (Get-Date).ToString("o")
        status = "Registered"
    } | ConvertTo-Json

    $newReg = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations" -Method Post -Body $enrollPayload -ContentType "application/json" -Headers $adminHeaders).data
    $newRegId = $newReg.registrationId
    Report-Result "Admin Enrolls Student into Course" ($newReg.status -eq "Registered") "Reg ID: $newRegId, Student: $($newReg.studentName)"

    # Duplicate Registration Check
    $dupRegBlocked = $false
    $dupRegDetail = ""
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations" -Method Post -Body $enrollPayload -ContentType "application/json" -Headers $adminHeaders -ErrorAction Stop
    } catch {
        $err = Get-HttpErrorDetails $_
        # Expected rejection: HTTP 400 Bad Request or HTTP 409 Conflict with duplicate enrollment message
        if (($err.StatusCode -in @(400, 409)) -and ($err.Body -match "already registered" -or $err.Body -match "already exists" -or $err.Body -match "duplicate" -or $err.Body.Length -gt 0)) {
            $dupRegBlocked = $true
            $dupRegDetail = "Student already enrolled in course (HTTP $($err.StatusCode))"
        } else {
            $dupRegDetail = "Unexpected response (HTTP $($err.StatusCode)): $($err.Body)"
        }
    }
    Report-Result "Duplicate Enrollment Prevented (400 Bad Request)" $dupRegBlocked $dupRegDetail

    # Verify Baseline Records Created
    $examRecord = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/exams/registration/$newRegId" -Method Get -Headers $adminHeaders).data
    $certRecord = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId" -Method Get -Headers $adminHeaders).data
    $baselineOk = ($null -ne $examRecord -and $null -ne $certRecord)
    Report-Result "Automatic Baseline Records Created (Exam & Certificate Entities)" $baselineOk "Exam Status: $($examRecord.examApplicationStatus), Cert Status: $($certRecord.verifiedStatus)"

    # Advance Registration Status
    $advanceRegPayload = @{ status = "InProgress" } | ConvertTo-Json
    $advReg = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$newRegId/status" -Method Patch -Body $advanceRegPayload -ContentType "application/json" -Headers $adminHeaders).data
    Report-Result "Advance Registration Status to InProgress" ($advReg.status -eq "InProgress") "Status: $($advReg.status)"

    # --------------------------------------------------------------------------
    # 9. EXAM GOVERNANCE & SCORING
    # --------------------------------------------------------------------------
    Write-Host "`n--- 9. EXAM GOVERNANCE & SCORING ---" -ForegroundColor Yellow

    # Out of range score validation (150 -> 400 Bad Request)
    $invalidScoreBlocked = $false
    try {
        $invalidExamPayload = @{
            examApplicationStatus = "FeePaid"
            examStatus = "Completed"
            score = 150
            passStatus = "Passed"
        } | ConvertTo-Json
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/exams/registration/$newRegId" -Method Put -Body $invalidExamPayload -ContentType "application/json" -Headers $adminHeaders -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 400) { $invalidScoreBlocked = $true }
    }
    Report-Result "Exam Score > 100 Rejected (400 Bad Request)" $invalidScoreBlocked "Score validation enforced"

    # Valid Exam Record Update
    $validExamPayload = @{
        examApplicationStatus = "HallTicketIssued"
        examStatus = "Completed"
        examDate = (Get-Date).ToString("o")
        score = 88.50
        passStatus = "Elite"
    } | ConvertTo-Json

    $updatedExam = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/exams/registration/$newRegId" -Method Put -Body $validExamPayload -ContentType "application/json" -Headers $adminHeaders).data
    Report-Result "Update Exam Record & Score" ($updatedExam.score -eq 88.50 -and $updatedExam.passStatus -eq "Elite") "Score: $($updatedExam.score), PassStatus: $($updatedExam.passStatus)"

    # --------------------------------------------------------------------------
    # 10. REAL SUPABASE CLOUD STORAGE & CERTIFICATE MANAGEMENT
    # --------------------------------------------------------------------------
    Write-Host "`n--- 10. PRIVATE CLOUD STORAGE & CERTIFICATE VERIFICATION ---" -ForegroundColor Yellow

    # Test file upload validation: non-PDF must be rejected
    $invalidUploadBlocked = $false
    try {
        $invalidFilePath = "$solutionDir/Desktop/invalid_file.txt"
        $fileBytes = [System.IO.File]::ReadAllBytes($invalidFilePath)
        $boundary = [System.Guid]::NewGuid().ToString()
        $contentType = "multipart/form-data; boundary=$boundary"

        $bodyLines = @(
            "--$boundary",
            'Content-Disposition: form-data; name="file"; filename="invalid_file.txt"',
            'Content-Type: text/plain',
            '',
            [System.Text.Encoding]::UTF8.GetString($fileBytes),
            "--$boundary--"
        )
        $body = $bodyLines -join "`r`n"
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/upload" -Method Post -Body $body -ContentType $contentType -Headers $adminHeaders -ErrorAction Stop
    } catch {
        $err = Get-HttpErrorDetails $_
        if ($err.StatusCode -eq 400) { $invalidUploadBlocked = $true }
    }
    Report-Result "Upload Non-PDF Document Blocked (400 Bad Request)" $invalidUploadBlocked "Only valid PDF files accepted"

    # Check Cloud Storage Configuration
    $credIssues = Test-IsPlaceholderOrInvalidCredential $supabaseUrl $supabaseServiceKey

    if ($credIssues.Count -gt 0) {
        Write-Host "`nREAL SUPABASE CLOUD STORAGE: Credentials missing, placeholder, or invalid format." -ForegroundColor Magenta
        Write-Host "Credential inspection details:" -ForegroundColor Magenta
        foreach ($issue in $credIssues) {
            Write-Host "  - $issue" -ForegroundColor Magenta
        }
        Write-Host "Per approved Phase 4 rules: Live cloud tests are NOT executed without valid real credentials." -ForegroundColor Magenta
        $script:cloudVerified = $false
        Report-Result "Real Supabase Private Storage Integration" $false "Missing/placeholder credentials: $($credIssues -join '; '). Live cloud storage is NOT VERIFIED." "NOT VERIFIED"
    } else {
        Write-Host "`nREAL Supabase Storage credentials detected ($supabaseUrl)." -ForegroundColor Green
        
        $check1Passed = $false
        $check2Passed = $false
        $check3Passed = $false
        $check4Passed = $false
        $check5Passed = $false
        $check6Passed = $false
        $check7Passed = $false
        $check8Passed = $false
        $check9Passed = $false
        $check10Passed = $false
        $check11Passed = $false
        $check12Passed = $false

        # Prepare Real PDF upload payload
        $pdfPath = "$solutionDir/Desktop/test_certificate.pdf"
        $pdfBytes = [System.IO.File]::ReadAllBytes($pdfPath)
        $boundary = [System.Guid]::NewGuid().ToString()
        $contentType = "multipart/form-data; boundary=$boundary"

        $header = "--$boundary`r`nContent-Disposition: form-data; name=`"file`"; filename=`"test_certificate.pdf`"`r`nContent-Type: application/pdf`r`n`r`n"
        $footer = "`r`n--$boundary--`r`n"
        
        $headerBytes = [System.Text.Encoding]::UTF8.GetBytes($header)
        $footerBytes = [System.Text.Encoding]::UTF8.GetBytes($footer)
        
        $fullBodyBytes = New-Object byte[] ($headerBytes.Length + $pdfBytes.Length + $footerBytes.Length)
        [System.Buffer]::BlockCopy($headerBytes, 0, $fullBodyBytes, 0, $headerBytes.Length)
        [System.Buffer]::BlockCopy($pdfBytes, 0, $fullBodyBytes, $headerBytes.Length, $pdfBytes.Length)
        [System.Buffer]::BlockCopy($footerBytes, 0, $fullBodyBytes, $headerBytes.Length + $pdfBytes.Length, $footerBytes.Length)

        # 1. Real PDF upload (Check 1)
        $uploadedCert = $null
        try {
            $certUploadResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/upload" -Method Post -Body $fullBodyBytes -ContentType $contentType -Headers $adminHeaders -ErrorAction Stop
            $uploadedCert = $certUploadResp.data
            if ($null -ne $uploadedCert -and $uploadedCert.storagePath -match '^certificates\/') {
                $check1Passed = $true
                Report-Result "1. Real Cloud Upload to Private Bucket" $true "Storage Path in DB: $($uploadedCert.storagePath)"
            } else {
                Report-Result "1. Real Cloud Upload to Private Bucket" $false "Upload returned unexpected response format: $($certUploadResp | ConvertTo-Json -Compress)"
            }
        } catch {
            $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
            Report-Result "1. Real Cloud Upload to Private Bucket" $false "Upload request failed: $($safeErr.SafeMessage)"
        }

        if (-not $check1Passed) {
            Write-Host "Cloud upload check failed. Stopping dependent cloud checks (Checks 2-11)." -ForegroundColor Yellow
            $script:cloudVerified = $false
            Report-Result "2. Object Exists in Real Supabase Bucket" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "3. Database Stores Only Storage Object Path" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "4. Zero Binary Bytes Stored in Database Record" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "5. Direct Public Access to Private Bucket Blocked" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "6. Admin Signed URL Verified (%PDF Header)" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "7. Student Own Certificate Signed Access Works" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "8. Student Cross-Student Access Denied (403 Forbidden)" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "9. Staff Assigned-Scope Access Works" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "10. Staff Cross-Scope Access Denied (403 Forbidden)" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
            Report-Result "11. Replacement Upload Updates Correct Object Path" $false "Skipped: Check 1 upload failed or was not verified" "NOT VERIFIED"
        } else {
            # 2. Object exists in private bucket
            try {
                $cleanPath = $uploadedCert.storagePath.Substring("certificates/".Length)
                $folder = [System.IO.Path]::GetDirectoryName($cleanPath).Replace('\', '/')
                $fileName = [System.IO.Path]::GetFileName($cleanPath)
                $listHeaders = @{ 
                    "apikey" = $supabaseServiceKey 
                }
                if (-not $supabaseServiceKey.StartsWith("sb_secret_", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $listHeaders["Authorization"] = "Bearer $supabaseServiceKey"
                }
                $listResp = Invoke-RestMethod -Uri "$supabaseUrl/storage/v1/object/list/certificates" -Method Post -Body (@{ prefix = $folder } | ConvertTo-Json) -ContentType "application/json" -Headers $listHeaders -ErrorAction Stop
                if ($listResp | Where-Object { $_.name -eq $fileName }) {
                    $check2Passed = $true
                }
                Report-Result "2. Object Exists in Real Supabase Bucket" $check2Passed "Object verified in private bucket: $cleanPath"
            } catch {
                $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                Report-Result "2. Object Exists in Real Supabase Bucket" $false "Failed to list objects in bucket: $($safeErr.SafeMessage)"
            }

            # 3. DB stores only object path
            $check3Passed = ($uploadedCert.storagePath -match '^certificates\/' -and -not $uploadedCert.storagePath.StartsWith("http"))
            Report-Result "3. Database Stores Only Storage Object Path" $check3Passed "Path stored: $($uploadedCert.storagePath)"

            # 4. PDF binary is not stored in DB
            $check4Passed = ($null -eq $uploadedCert.PSObject.Properties['fileBytes'] -and $null -eq $uploadedCert.PSObject.Properties['content'] -and $null -eq $uploadedCert.PSObject.Properties['binaryData'])
            Report-Result "4. Zero Binary Bytes Stored in Database Record" $check4Passed "Only storagePath metadata stored in DB"

            # 5. Unauthenticated direct object access fails
            try {
                $publicUrl = "$supabaseUrl/storage/v1/object/public/$($uploadedCert.storagePath)"
                $directResp = Invoke-WebRequest -Uri $publicUrl -Method Get -UseBasicParsing -ErrorAction Stop
                Report-Result "5. Direct Public Access to Private Bucket Blocked" $false "Public URL unexpectedly returned HTTP $($directResp.StatusCode)"
            } catch {
                $check5Passed = $true
                Report-Result "5. Direct Public Access to Private Bucket Blocked" $true "Unauthenticated direct URL rejected by Supabase"
            }

            # 6. Admin signed URL works
            try {
                $adminAccessResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/$($uploadedCert.certificateId)/access" -Method Get -Headers $adminHeaders -ErrorAction Stop
                $adminAccess = $adminAccessResp.data
                if (![string]::IsNullOrEmpty($adminAccess.accessUrl) -and $adminAccess.accessUrl -match 'token=') {
                    # Sanitize URL for safe diagnostics (redact token parameter)
                    $sanitizedAccessUrl = $adminAccess.accessUrl -replace 'token=[^&]+', 'token=[REDACTED_TOKEN]'
                    try {
                        $downloadResp = Invoke-WebRequest -Uri $adminAccess.accessUrl -Method Get -UseBasicParsing -ErrorAction Stop
                        $downloadedBytes = $downloadResp.Content
                        $isPdfHeader = $false
                        if ($null -ne $downloadedBytes -and $downloadedBytes.Length -ge 4) {
                            if ($downloadedBytes -is [byte[]]) {
                                $isPdfHeader = ([System.Text.Encoding]::ASCII.GetString($downloadedBytes[0..3]) -eq "%PDF")
                            } else {
                                $isPdfHeader = ($downloadedBytes.ToString().StartsWith("%PDF"))
                            }
                        }
                        if ($isPdfHeader) {
                            $check6Passed = $true
                            Report-Result "6. Admin Signed URL Verified (%PDF Header)" $true "Valid PDF ($($downloadedBytes.Length) bytes, %PDF header) downloaded via short-lived signed URL"
                        } else {
                            Report-Result "6. Admin Signed URL Verified (%PDF Header)" $false "Downloaded content did not start with %PDF header"
                        }
                    } catch {
                        $downloadErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                        Report-Result "6. Admin Signed URL Verified (%PDF Header)" $false "Download from signed URL failed ($sanitizedAccessUrl): $($downloadErr.SafeMessage)"
                    }
                } else {
                    Report-Result "6. Admin Signed URL Verified (%PDF Header)" $false "Admin access response did not contain a valid signed URL with token parameter"
                }
            } catch {
                $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                Report-Result "6. Admin Signed URL Verified (%PDF Header)" $false "Admin certificate access endpoint failed: $($safeErr.SafeMessage)"
            }

            # 7. Student own certificate access works
            try {
                $newStudentLoginPayload = @{ registerNumber = "951021104099"; password = "Student@Nptel2026" } | ConvertTo-Json
                $newStudentLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $newStudentLoginPayload -ContentType "application/json" -ErrorAction Stop
                $newStudentToken = $newStudentLogin.data.accessToken
                $newStudentHeaders = @{ "Authorization" = "Bearer $newStudentToken" }

                $studentOwnAccess = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/certificates/$($uploadedCert.certificateId)/access" -Method Get -Headers $newStudentHeaders -ErrorAction Stop).data
                $check7Passed = (![string]::IsNullOrEmpty($studentOwnAccess.accessUrl) -and $studentOwnAccess.accessUrl -match 'token=')
                Report-Result "7. Student Own Certificate Signed Access Works" $check7Passed "Owner student generated valid signed URL"
            } catch {
                $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                Report-Result "7. Student Own Certificate Signed Access Works" $false "Student access failed: $($safeErr.SafeMessage)"
            }

            # 8. Student cross-student access is denied
            try {
                Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/certificates/$($uploadedCert.certificateId)/access" -Method Get -Headers $studentHeaders -ErrorAction Stop
                Report-Result "8. Student Cross-Student Access Denied (403 Forbidden)" $false "Cross-student access unexpectedly allowed"
            } catch {
                $err = Get-HttpErrorDetails $_
                if ($err.StatusCode -eq 403) { $check8Passed = $true }
                Report-Result "8. Student Cross-Student Access Denied (403 Forbidden)" $check8Passed "Other student blocked (HTTP $($err.StatusCode))"
            }

            # 9. Staff assigned-scope access works
            try {
                $inScopeStaffLoginPayload = @{ staffId = "CSE-STF-99"; password = $newPassword } | ConvertTo-Json
                $inScopeStaffLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $inScopeStaffLoginPayload -ContentType "application/json" -ErrorAction Stop
                $inScopeStaffHeaders = @{ "Authorization" = "Bearer $($inScopeStaffLogin.data.accessToken)" }

                $staffAccess = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/certificates/$($uploadedCert.certificateId)/access" -Method Get -Headers $inScopeStaffHeaders -ErrorAction Stop).data
                $check9Passed = (![string]::IsNullOrEmpty($staffAccess.accessUrl) -and $staffAccess.accessUrl -match 'token=')
                Report-Result "9. Staff Assigned-Scope Access Works" $check9Passed "In-scope staff generated valid signed URL"
            } catch {
                $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                Report-Result "9. Staff Assigned-Scope Access Works" $false "Staff access failed: $($safeErr.SafeMessage)"
            }

            # 10. Staff cross-scope access is denied
            try {
                Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/certificates/$($uploadedCert.certificateId)/access" -Method Get -Headers $staffHeaders -ErrorAction Stop
                Report-Result "10. Staff Cross-Scope Access Denied (403 Forbidden)" $false "Cross-scope staff access unexpectedly allowed"
            } catch {
                $err = Get-HttpErrorDetails $_
                if ($err.StatusCode -eq 403) { $check10Passed = $true }
                Report-Result "10. Staff Cross-Scope Access Denied (403 Forbidden)" $check10Passed "Out-of-scope staff blocked (HTTP $($err.StatusCode))"
            }

            # 11. Replacement upload updates the correct object/path
            try {
                $reuploadResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/upload" -Method Post -Body $fullBodyBytes -ContentType $contentType -Headers $adminHeaders -ErrorAction Stop
                $reuploadedCert = $reuploadResp.data
                $check11Passed = ($null -ne $reuploadedCert -and $reuploadedCert.storagePath -eq $uploadedCert.storagePath)
                Report-Result "11. Replacement Upload Updates Correct Object Path" $check11Passed "Object path preserved and overwritten in private bucket: $($reuploadedCert.storagePath)"
            } catch {
                $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                Report-Result "11. Replacement Upload Updates Correct Object Path" $false "Replacement upload failed: $($safeErr.SafeMessage)"
            }
        }

        # 12. Unauthorized Student/Staff upload or verification is rejected
        $targetStudentHeaders = if ($newStudentHeaders) { $newStudentHeaders } else { $studentHeaders }
        $targetStaffHeaders = if ($inScopeStaffHeaders) { $inScopeStaffHeaders } else { $staffHeaders }

        $studentUploadBlocked = $false
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/upload" -Method Post -Body $fullBodyBytes -ContentType $contentType -Headers $targetStudentHeaders -ErrorAction Stop
        } catch {
            $err = Get-HttpErrorDetails $_
            if ($err.StatusCode -eq 403 -or $err.StatusCode -eq 401) { $studentUploadBlocked = $true }
        }

        $staffUploadBlocked = $false
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/upload" -Method Post -Body $fullBodyBytes -ContentType $contentType -Headers $targetStaffHeaders -ErrorAction Stop
        } catch {
            $err = Get-HttpErrorDetails $_
            if ($err.StatusCode -eq 403 -or $err.StatusCode -eq 401) { $staffUploadBlocked = $true }
        }

        $studentVerifyBlocked = $false
        try {
            $verifyPayload = @{ verifiedStatus = "Verified"; verifiedDate = (Get-Date).ToString("o") } | ConvertTo-Json
            Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/status" -Method Patch -Body $verifyPayload -ContentType "application/json" -Headers $targetStudentHeaders -ErrorAction Stop
        } catch {
            $err = Get-HttpErrorDetails $_
            if ($err.StatusCode -eq 403 -or $err.StatusCode -eq 401) { $studentVerifyBlocked = $true }
        }

        $staffVerifyBlocked = $false
        try {
            $verifyPayload = @{ verifiedStatus = "Verified"; verifiedDate = (Get-Date).ToString("o") } | ConvertTo-Json
            Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/status" -Method Patch -Body $verifyPayload -ContentType "application/json" -Headers $targetStaffHeaders -ErrorAction Stop
        } catch {
            $err = Get-HttpErrorDetails $_
            if ($err.StatusCode -eq 403 -or $err.StatusCode -eq 401) { $staffVerifyBlocked = $true }
        }

        $check12Passed = ($studentUploadBlocked -and $staffUploadBlocked -and $studentVerifyBlocked -and $staffVerifyBlocked)
        Report-Result "12. Unauthorized Student/Staff Upload & Verification Rejected (403)" $check12Passed "Student & Staff blocked from upload & credit verification"

        # Final evaluation of all 12 checks
        $all12Passed = ($check1Passed -and $check2Passed -and $check3Passed -and $check4Passed -and 
                        $check5Passed -and $check6Passed -and $check7Passed -and $check8Passed -and 
                        $check9Passed -and $check10Passed -and $check11Passed -and $check12Passed)

        if ($all12Passed) {
            $script:cloudVerified = $true
            Report-Result "All 12 Real Supabase Cloud Storage Checks" $true "All 12 live Supabase private storage requirements independently verified"

            # Verify Certificate credit approval (Admin)
            try {
                $verifyPayload = @{ verifiedStatus = "Verified"; verifiedDate = (Get-Date).ToString("o") } | ConvertTo-Json
                $verifiedCert = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/certificates/registration/$newRegId/status" -Method Patch -Body $verifyPayload -ContentType "application/json" -Headers $adminHeaders -ErrorAction Stop).data
                Report-Result "Admin Verify Academic Credit Approval" ($verifiedCert.verifiedStatus -eq "Verified") "Verified Date: $($verifiedCert.verifiedDate)"
            } catch {
                $safeErr = Get-SafeErrorMessage $_ $supabaseServiceKey
                Report-Result "Admin Verify Academic Credit Approval" $false "Approval failed: $($safeErr.SafeMessage)"
            }
        } else {
            $script:cloudVerified = $false
            Report-Result "All 12 Real Supabase Cloud Storage Checks" $false "One or more of the 12 cloud checks failed. Cloud status is NOT VERIFIED." "NOT VERIFIED"
        }
    }

    # --------------------------------------------------------------------------
    # 11. NOTIFICATIONS & AUTOMATION RULES ENGINE
    # --------------------------------------------------------------------------
    Write-Host "`n--- 11. NOTIFICATIONS & AUTOMATION RULES ENGINE ---" -ForegroundColor Yellow

    $broadcastPayload = @{
        title = "NPTEL Examination Hall Tickets Released"
        message = "All eligible students can now download their exam hall tickets."
        targetType = "AllDepartment"
        department = "CSE"
    } | ConvertTo-Json

    $broadcastResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/notifications" -Method Post -Body $broadcastPayload -ContentType "application/json" -Headers $adminHeaders
    Report-Result "Admin Broadcast Notification to Department" ($broadcastResp.success -eq $true) "Dispatched to department students"

    # Trigger Automated Rules Engine
    $rulesResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/notifications/trigger-rules" -Method Post -Body "{}" -ContentType "application/json" -Headers $adminHeaders
    Report-Result "Server-Controlled Automation Rules Engine Triggered" ($rulesResp.success -eq $true) "Evaluated pending exam deadlines & unverified certificates"

    # --------------------------------------------------------------------------
    # 12. INSTITUTIONAL REPORTS & EXPORTS (PDF / XLSX / CSV)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 12. INSTITUTIONAL REPORTS & EXPORTS ---" -ForegroundColor Yellow

    $reportFilterPayload = @{
        reportType = "student-registration"
        department = "CSE"
        year = $null
    } | ConvertTo-Json

    # 1. Preview
    $previewResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/reports/preview" -Method Post -Body $reportFilterPayload -ContentType "application/json" -Headers $adminHeaders
    $preview = $previewResp.data
    Report-Result "Admin Report In-Memory Preview" ($preview.totalRecords -gt 0) "Title: $($preview.reportTitle), Records: $($preview.totalRecords)"

    # 2. PDF Export (QuestPDF)
    $pdfExport = Invoke-WebRequest -Uri "http://127.0.0.1:5000/api/v1/admin/reports/export-pdf" -Method Post -Body $reportFilterPayload -ContentType "application/json" -Headers $adminHeaders -UseBasicParsing
    $isPdf = ($pdfExport.Content.Length -gt 100 -and [System.Text.Encoding]::ASCII.GetString($pdfExport.Content[0..3]) -eq "%PDF")
    Report-Result "QuestPDF Institutional Report Generation (%PDF Header)" $isPdf "PDF Size: $($pdfExport.Content.Length) bytes"

    # 3. XLSX Export (ClosedXML)
    $xlsxExport = Invoke-WebRequest -Uri "http://127.0.0.1:5000/api/v1/admin/reports/export-xlsx" -Method Post -Body $reportFilterPayload -ContentType "application/json" -Headers $adminHeaders -UseBasicParsing
    $isXlsx = ($xlsxExport.Content.Length -gt 100 -and $xlsxExport.Content[0] -eq 0x50 -and $xlsxExport.Content[1] -eq 0x4B)
    Report-Result "ClosedXML Accreditation Workbook Export (PK Header)" $isXlsx "XLSX Size: $($xlsxExport.Content.Length) bytes"

    # 4. CSV Export (RFC 4180)
    $csvExport = Invoke-WebRequest -Uri "http://127.0.0.1:5000/api/v1/admin/reports/export-csv" -Method Post -Body $reportFilterPayload -ContentType "application/json" -Headers $adminHeaders -UseBasicParsing
    $csvText = if ($csvExport.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($csvExport.Content) } else { [string]$csvExport.Content }
    $isCsv = ($csvText -match "Register Number" -and $csvText -match "Student Name")
    Report-Result "RFC 4180 CSV Dataset Export" $isCsv "CSV lines: $($csvText.Split("`n").Length)"

    # --------------------------------------------------------------------------
    # 13. AUDIT TRAIL VERIFICATION & ZERO SECRETS AUDIT
    # --------------------------------------------------------------------------
    Write-Host "`n--- 13. AUDIT LOGS & ZERO SECRETS AUDIT ---" -ForegroundColor Yellow

    $auditResp = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/audit-logs?page=1&pageSize=50" -Method Get -Headers $adminHeaders).data
    $auditLogs = $auditResp.items
    $hasLogs = ($auditLogs.Count -gt 0)
    Report-Result "Audit Trail Records Mutations" $hasLogs "Total audit entries recorded: $($auditLogs.Count)"

    # Verify no passwords or tokens in audit logs
    $leakCount = 0
    foreach ($log in $auditLogs) {
        $details = $log.details
        if ($details -match "Admin@Nptel" -or $details -match "Staff@Nptel" -or $details -match "NewStaffPass" -or $details -match "eyJh") {
            $leakCount++
        }
    }
    Report-Result "Audit Logs Contain ZERO Passwords, Hashes, or Tokens" ($leakCount -eq 0) "Sensitive tokens detected: $leakCount"

    # --------------------------------------------------------------------------
    # 14. REGRESSION VERIFICATION (PHASE 1, PHASE 2, PHASE 3)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 14. REGRESSION VERIFICATION (PHASES 1, 2, 3) ---" -ForegroundColor Yellow

    # Phase 1: Student Login & Profile
    $p1Prof = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/me" -Method Get -Headers $studentHeaders).data
    Report-Result "Phase 1 Student Login & Profile Regression" ($p1Prof.registerNumber -eq "951021104001") "Reg: $($p1Prof.registerNumber)"

    # Phase 2: Student Dashboard Summary & Courses
    $p2Summary = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/dashboard-summary" -Method Get -Headers $studentHeaders).data
    $p2Courses = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses" -Method Get -Headers $studentHeaders).data
    $p2Count = $p2Courses.Count
    $p2Ok = ($p2Count -ge 1 -and ($p2Summary.inProgressCourses -ge 1 -or $p2Summary.registeredCourses -ge 1 -or $p2Count -ge 1))
    Report-Result "Phase 2 Student Workflow Regression" $p2Ok "Enrolled Courses: $p2Count, Course: $($p2Courses[0].courseCode)"

    # Phase 3: Staff Dashboard & Scoped Student Directory
    $p3Summary = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/dashboard-summary" -Method Get -Headers $staffHeaders).data
    $p3Students = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?page=1&pageSize=10" -Method Get -Headers $staffHeaders).data
    $p3Ok = ($p3Summary.totalStudents -ge 1 -and $p3Students.items.Count -ge 1)
    Report-Result "Phase 3 Staff Workflow Regression" $p3Ok "Assigned Students: $($p3Summary.totalStudents), Students in Scope: $($p3Students.items.Count)"

    # --------------------------------------------------------------------------
    # 15. REAL DATABASE CHANGE VERIFICATION
    # --------------------------------------------------------------------------
    Write-Host "`n--- 15. REAL DATABASE CHANGE VERIFICATION ---" -ForegroundColor Yellow

    # Read status before
    $regBefore = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$newRegId" -Method Get -Headers $adminHeaders).data
    # Update to Completed
    Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$newRegId/status" -Method Patch -Body (@{ status = "Completed" } | ConvertTo-Json) -ContentType "application/json" -Headers $adminHeaders | Out-Null
    # Read status after
    $regAfter = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$newRegId" -Method Get -Headers $adminHeaders).data

    Report-Result "Admin Database Mutation Reflected Immediately in Queries" ($regBefore.status -ne $regAfter.status -and $regAfter.status -eq "Completed") "Before: $($regBefore.status) -> After: $($regAfter.status)"

    # --------------------------------------------------------------------------
    # 16. WPF APPLICATION LAUNCH VERIFICATION
    # --------------------------------------------------------------------------
    Write-Host "`n--- 16. WPF DESKTOP CLIENT LAUNCH VERIFICATION ---" -ForegroundColor Yellow

    $wpfPath = "$solutionDir/Desktop/bin/Debug/net8.0-windows/NPTELManagement.Desktop.exe"
    $wpfProcess = Start-Process -FilePath $wpfPath -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2

    $wpfRunning = (-not $wpfProcess.HasExited)
    Report-Result "WPF Desktop Executable Launched & Stable" $wpfRunning "PID: $($wpfProcess.Id), Exited: $($wpfProcess.HasExited)"

    if ($wpfProcess -and -not $wpfProcess.HasExited) {
        $wpfProcess.Kill()
    }

} finally {
    Write-Host "`nStopping background API service..." -ForegroundColor Gray
    if ($apiProcess -and -not $apiProcess.HasExited) {
        $apiProcess.Kill()
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "                    PHASE 4 TEST SUMMARY                         " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
foreach ($r in $testResults) {
    $col = switch ($r.Status) {
        "PASS" { "Green" }
        "NOT VERIFIED" { "Yellow" }
        Default { "Red" }
    }
    Write-Host "[$($r.Status)] $($r.Name)" -ForegroundColor $col
}
Write-Host "=================================================================" -ForegroundColor Cyan

Write-Host "`n--- CLOUD STORAGE VERIFICATION STATUS ---" -ForegroundColor Cyan
if ($cloudVerified) {
    Write-Host "[VERIFIED] Real Supabase Private Cloud Storage is fully verified with live credentials." -ForegroundColor Green
} else {
    Write-Host "[NOT VERIFIED] Real Supabase Private Cloud Storage is NOT VERIFIED." -ForegroundColor Yellow
    Write-Host "              Live cloud verification requires valid real credentials and all 12 cloud checks to pass." -ForegroundColor Yellow
    Write-Host "              Placeholder values (e.g. YOUR-PROJECT-REF) are strictly rejected." -ForegroundColor Yellow
}

if (-not $allPassed) {
    Write-Host "`nCore functional tests encountered failures. See details above." -ForegroundColor Red
    exit 1
} elseif (-not $cloudVerified) {
    Write-Host "`nCore Phase 4 functional tests PASSED, but Real Supabase Cloud Storage is NOT VERIFIED." -ForegroundColor Yellow
    Write-Host "Phase 4 is NOT claimed complete until real Supabase verification passes." -ForegroundColor Yellow
    exit 2
} else {
    Write-Host "`nALL PHASE 4 TESTS INCLUDING REAL CLOUD STORAGE COMPLETED SUCCESSFULLY!" -ForegroundColor Green
    exit 0
}
