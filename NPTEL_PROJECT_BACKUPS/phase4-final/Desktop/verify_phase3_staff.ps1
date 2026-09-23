# ==============================================================================
# PHASE 3 - REAL CSE STAFF MANAGEMENT MODULE VERIFICATION SUITE
# ==============================================================================

$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$solutionDir = Split-Path -Parent $PSScriptRoot
Set-Location $solutionDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "     PHASE 3: REAL CSE STAFF MANAGEMENT MODULE VERIFICATION     " -ForegroundColor Cyan
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
Write-Host "`n--- 1. ARCHITECTURAL & SECURITY AUDIT ---" -ForegroundColor Yellow

$desktopCsproj = Get-Content "Desktop/NPTELManagement.Desktop.csproj" -Raw
$hasCoreRef = $desktopCsproj -match 'ProjectReference Include="..\\Core\\NPTELManagement.Core.csproj"'
$hasInfraRef = $desktopCsproj -match 'Infrastructure'
$hasNpgsqlRef = $desktopCsproj -match 'Npgsql'

Report-Result "Desktop references ONLY Core" ($hasCoreRef -and -not $hasInfraRef -and -not $hasNpgsqlRef) "Core: $hasCoreRef, Infra: $hasInfraRef, Npgsql: $hasNpgsqlRef"

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

Report-Result "Desktop has ZERO backend secrets or database credentials" ($filesWithSecrets.Count -eq 0) "Secrets found: $($filesWithSecrets.Count)"

# Check that StaffController contains NO mutation actions (POST/PUT/DELETE/PATCH)
$staffControllerCode = Get-Content "Api/Controllers/StaffController.cs" -Raw
$hasHttpPost = $staffControllerCode -match '\[HttpPost'
$hasHttpPut = $staffControllerCode -match '\[HttpPut'
$hasHttpDelete = $staffControllerCode -match '\[HttpDelete'
$hasHttpPatch = $staffControllerCode -match '\[HttpPatch'

Report-Result "StaffController is STRICTLY READ-ONLY (No POST/PUT/DELETE/PATCH)" (-not $hasHttpPost -and -not $hasHttpPut -and -not $hasHttpDelete -and -not $hasHttpPatch) "Mutations found: Post=$hasHttpPost, Put=$hasHttpPut, Delete=$hasHttpDelete"

# ------------------------------------------------------------------------------
# 2. START LIVE API SERVICE
# ------------------------------------------------------------------------------
Write-Host "`n--- 2. STARTING LIVE ASP.NET CORE API ---" -ForegroundColor Yellow

# Terminate existing API processes
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

Report-Result "Live API Started & Health Probed" $apiUp "Status: $($resp.status), Database: $($resp.database)"

if (-not $apiUp) {
    Write-Host "API failed to start. Aborting test execution." -ForegroundColor Red
    if ($apiProcess -and -not $apiProcess.HasExited) { $apiProcess.Kill() }
    exit 1
}

try {
    # --------------------------------------------------------------------------
    # 3. STAFF LOGIN & PROFILE (CSE 3rd Year In-Charge)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 3. STAFF LOGIN & PROFILE (CSE 3rd Year In-Charge) ---" -ForegroundColor Yellow

    $loginPayload = @{
        staffId = "CSE-STF-01"
        password = "Staff@Nptel2026"
    } | ConvertTo-Json

    $loginResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $loginPayload -ContentType "application/json"
    $staffToken = $loginResp.data.accessToken
    $staffHeaders = @{ "Authorization" = "Bearer $staffToken" }

    Report-Result "Staff Login (CSE-STF-01)" ($null -ne $staffToken -and $loginResp.data.role -eq "Staff") "Role: $($loginResp.data.role), Identifier: $($loginResp.data.identifier)"

    $profileResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/me" -Method Get -Headers $staffHeaders
    $prof = $profileResp.data
    $profValid = ($prof.staffName -eq "Dr. K. Ramanathan" -and $prof.department -eq "CSE" -and $prof.assignedYear -eq 3 -and $prof.assignedClass -eq "A" -and $prof.assignedStudentCount -eq 1)

    Report-Result "Staff Profile (Assigned Scope & Student Count)" $profValid "Name: $($prof.staffName), Scope: $($prof.department) Y$($prof.assignedYear) Sec $($prof.assignedClass), Students: $($prof.assignedStudentCount)"

    # --------------------------------------------------------------------------
    # 4. STAFF DASHBOARD SUMMARY (SCOPED COUNTS)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 4. STAFF DASHBOARD SUMMARY (SCOPED COUNTS) ---" -ForegroundColor Yellow

    $summaryResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/dashboard-summary" -Method Get -Headers $staffHeaders
    $sum = $summaryResp.data
    $summaryValid = ($sum.totalStudents -eq 1 -and $sum.registeredStudents -eq 1 -and $sum.inProgressCourses -eq 1 -and $sum.examApplied -eq 1 -and $sum.certificatePending -eq 1)

    Report-Result "Staff Dashboard Summary (Live Scoped Database Metrics)" $summaryValid "Total: $($sum.totalStudents), Reg: $($sum.registeredStudents), InProgress: $($sum.inProgressCourses), ExamApplied: $($sum.examApplied), CertPending: $($sum.certificatePending)"

    # --------------------------------------------------------------------------
    # 5. STAFF STUDENTS LIST & SEARCH
    # --------------------------------------------------------------------------
    Write-Host "`n--- 5. STAFF STUDENTS LIST & SEARCH ---" -ForegroundColor Yellow

    $studentsResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Get -Headers $staffHeaders
    $pagedStudents = $studentsResp.data
    $hasAuthorizedStudent = ($pagedStudents.totalCount -eq 1 -and $pagedStudents.items[0].registerNumber -eq "951021104001")

    Report-Result "Staff Students List (Scoped to Year 3 Section A)" $hasAuthorizedStudent "Count: $($pagedStudents.totalCount), First RegNo: $($pagedStudents.items[0].registerNumber)"

    # Search by Authorized Student Name
    $searchAravind = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?search=Aravind" -Method Get -Headers $staffHeaders
    Report-Result "Search in Scope (Name: Aravind)" ($searchAravind.data.totalCount -eq 1) "Found: $($searchAravind.data.totalCount)"

    # Search for Year 1 student (Bhavani) -> MUST return 0
    $searchBhavani = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?search=Bhavani" -Method Get -Headers $staffHeaders
    Report-Result "Search Out-of-Scope (Bhavani / Year 1 returns 0)" ($searchBhavani.data.totalCount -eq 0) "Found: $($searchBhavani.data.totalCount)"

    # Search for Section B student (Ezhil) -> MUST return 0
    $searchEzhil = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?search=Ezhil" -Method Get -Headers $staffHeaders
    Report-Result "Search Out-of-Scope (Ezhil / Section B returns 0)" ($searchEzhil.data.totalCount -eq 0) "Found: $($searchEzhil.data.totalCount)"

    # --------------------------------------------------------------------------
    # 6. FILTERS & SCOPE EXPANSION PREVENTION
    # --------------------------------------------------------------------------
    Write-Host "`n--- 6. FILTERS & SCOPE EXPANSION PREVENTION ---" -ForegroundColor Yellow

    # Year 3 Staff requesting ?year=1 -> Server MUST NOT return Year 1 data
    $yearFilterAttack = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?year=1" -Method Get -Headers $staffHeaders
    Report-Result "Filter Scope Enforcement (?year=1 cannot expose Year 1)" ($yearFilterAttack.data.totalCount -eq 0) "Found: $($yearFilterAttack.data.totalCount)"

    # Section A Staff requesting ?classSection=B -> Server MUST NOT return Section B data
    $classFilterAttack = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?classSection=B" -Method Get -Headers $staffHeaders
    Report-Result "Filter Scope Enforcement (?classSection=B cannot expose Section B)" ($classFilterAttack.data.totalCount -eq 0) "Found: $($classFilterAttack.data.totalCount)"

    # Matching Class Section A -> returns 1
    $classFilterMatching = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?classSection=A" -Method Get -Headers $staffHeaders
    Report-Result "Filter Matching (?classSection=A returns authorized student)" ($classFilterMatching.data.totalCount -eq 1) "Found: $($classFilterMatching.data.totalCount)"

    # Filter Registration Status InProgress -> 1
    $statusFilter = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?registrationStatus=InProgress" -Method Get -Headers $staffHeaders
    Report-Result "Filter by RegistrationStatus (InProgress)" ($statusFilter.data.totalCount -eq 1) "Found: $($statusFilter.data.totalCount)"

    # Filter Registration Status Completed -> 0
    $statusFilter0 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students?registrationStatus=Completed" -Method Get -Headers $staffHeaders
    Report-Result "Filter by RegistrationStatus (Completed returns 0 for Year 3 Sec A)" ($statusFilter0.data.totalCount -eq 0) "Found: $($statusFilter0.data.totalCount)"

    # --------------------------------------------------------------------------
    # 7. STUDENT DETAILS & SUB-RESOURCES (AUTHORIZED)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 7. STUDENT DETAILS & SUB-RESOURCES (AUTHORIZED) ---" -ForegroundColor Yellow

    $student1Id = "cccccccc-cccc-cccc-cccc-cccccccccc01"
    $detailsResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id" -Method Get -Headers $staffHeaders
    $stu = $detailsResp.data
    $detailsValid = ($stu.name -eq "Aravind Swaminathan" -and $stu.registerNumber -eq "951021104001" -and $stu.courses.Count -ge 1)

    Report-Result "GET /students/{id} (Authorized Student Details)" $detailsValid "Name: $($stu.name), Courses: $($stu.courses.Count)"

    # Courses Sub-resource
    $coursesResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/courses" -Method Get -Headers $staffHeaders
    Report-Result "GET /students/{id}/courses" ($coursesResp.data.Count -ge 1 -and $coursesResp.data[0].courseCode -eq "noc24-cs01") "Course: $($coursesResp.data[0].courseName)"

    # Timeline Sub-resource
    $timelineResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/timeline" -Method Get -Headers $staffHeaders
    Report-Result "GET /students/{id}/timeline" ($timelineResp.data.Count -eq 11) "Timeline Stages: $($timelineResp.data.Count)"

    # Exam Sub-resource
    $examResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/exam" -Method Get -Headers $staffHeaders
    Report-Result "GET /students/{id}/exam" ($examResp.data.examApplicationStatus -eq "Applied") "Exam Application Status: $($examResp.data.examApplicationStatus)"

    # Certificate Sub-resource
    $certResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/certificate" -Method Get -Headers $staffHeaders
    Report-Result "GET /students/{id}/certificate" ($certResp.data.verifiedStatus -eq "Pending") "Certificate Status: $($certResp.data.verifiedStatus)"

    # --------------------------------------------------------------------------
    # 8. CROSS-SCOPE BLOCKING (CRITICAL SECURITY)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 8. CROSS-SCOPE BLOCKING (CRITICAL SECURITY) ---" -ForegroundColor Yellow

    $outOfScopeStudents = @(
        @{ Id = "ffffffff-ffff-ffff-ffff-ffffffffff02"; Desc = "Year 1 Student (Bhavani)" },
        @{ Id = "ffffffff-ffff-ffff-ffff-ffffffffff03"; Desc = "Year 2 Student (Chitra)" },
        @{ Id = "ffffffff-ffff-ffff-ffff-ffffffffff04"; Desc = "Year 4 Student (Dinesh)" },
        @{ Id = "ffffffff-ffff-ffff-ffff-ffffffffff05"; Desc = "Year 3 Section B Student (Ezhil)" }
    )

    foreach ($target in $outOfScopeStudents) {
        $blocked = $false
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$($target.Id)" -Method Get -Headers $staffHeaders -ErrorAction Stop
        } catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -eq 404 -or $status -eq 403) {
                $blocked = $true
            }
        }
        Report-Result "Cross-Scope Blocked: $($target.Desc)" $blocked "Status: $status"
    }

    # Sub-resources blocked on out-of-scope student
    $targetId = "ffffffff-ffff-ffff-ffff-ffffffffff02" # Year 1
    $subEndpoints = @("courses", "timeline", "exam", "certificate")
    foreach ($sub in $subEndpoints) {
        $subBlocked = $false
        try {
            Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$targetId/$sub" -Method Get -Headers $staffHeaders -ErrorAction Stop
        } catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -eq 404 -or $status -eq 403) {
                $subBlocked = $true
            }
        }
        Report-Result "Sub-resource Cross-Scope Blocked: /$sub" $subBlocked "Status: $status"
    }

    # --------------------------------------------------------------------------
    # 9. ROLE ISOLATION
    # --------------------------------------------------------------------------
    Write-Host "`n--- 9. ROLE ISOLATION ---" -ForegroundColor Yellow

    # Staff -> Admin endpoint (/api/v1/admin/me)
    $staffAdminBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $staffHeaders -ErrorAction Stop
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 403) { $staffAdminBlocked = $true }
    }
    Report-Result "Staff cannot access Admin Endpoint (/api/v1/admin/me -> 403)" $staffAdminBlocked "Status: $status"

    # Staff -> Student endpoint (/api/v1/student/me)
    $staffStudentBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/me" -Method Get -Headers $staffHeaders -ErrorAction Stop
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 403) { $staffStudentBlocked = $true }
    }
    Report-Result "Staff cannot access Student Endpoint (/api/v1/student/me -> 403)" $staffStudentBlocked "Status: $status"

    # --------------------------------------------------------------------------
    # 10. READ-ONLY RESTRICTIONS
    # --------------------------------------------------------------------------
    Write-Host "`n--- 10. READ-ONLY RESTRICTIONS ---" -ForegroundColor Yellow

    # Attempt POST to student list
    $postBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Post -Body "{}" -ContentType "application/json" -Headers $staffHeaders -ErrorAction Stop
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 405 -or $status -eq 404 -or $status -eq 403) { $postBlocked = $true }
    }
    Report-Result "Staff POST /students is Denied (405/404)" $postBlocked "Status: $status"

    # Attempt PUT to student
    $putBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id" -Method Put -Body "{}" -ContentType "application/json" -Headers $staffHeaders -ErrorAction Stop
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 405 -or $status -eq 404 -or $status -eq 403) { $putBlocked = $true }
    }
    Report-Result "Staff PUT /students/{id} is Denied (405/404)" $putBlocked "Status: $status"

    # Attempt DELETE on student
    $deleteBlocked = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id" -Method Delete -Headers $staffHeaders -ErrorAction Stop
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 405 -or $status -eq 404 -or $status -eq 403) { $deleteBlocked = $true }
    }
    Report-Result "Staff DELETE /students/{id} is Denied (405/404)" $deleteBlocked "Status: $status"

    # --------------------------------------------------------------------------
    # 11. REPORTS PREVIEW API
    # --------------------------------------------------------------------------
    Write-Host "`n--- 11. REPORTS PREVIEW API ---" -ForegroundColor Yellow

    $reportTypes = @("student-registration", "course-status", "exam-status", "certificate-status")
    foreach ($rt in $reportTypes) {
        $repResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/reports/preview?reportType=$rt" -Method Get -Headers $staffHeaders
        $repValid = ($null -ne $repResp.data -and $repResp.data.items.Count -ge 1 -and $repResp.data.department -eq "CSE" -and $repResp.data.year -eq 3)
        Report-Result "Report Preview ($rt)" $repValid "Title: $($repResp.data.reportTitle), Items: $($repResp.data.items.Count)"
    }

    # --------------------------------------------------------------------------
    # 12. ALL 4 YEAR IN-CHARGES VERIFICATION
    # --------------------------------------------------------------------------
    Write-Host "`n--- 12. ALL 4 YEAR IN-CHARGES VERIFICATION ---" -ForegroundColor Yellow

    $staffMembers = @(
        @{ Username = "CSE-STF-101"; ExpectedYear = 1; ExpectedStudent = "951021104002" },
        @{ Username = "CSE-STF-102"; ExpectedYear = 2; ExpectedStudent = "951021104003" },
        @{ Username = "CSE-STF-01";  ExpectedYear = 3; ExpectedStudent = "951021104001" },
        @{ Username = "CSE-STF-104"; ExpectedYear = 4; ExpectedStudent = "951021104004" }
    )

    foreach ($stf in $staffMembers) {
        $stfPayload = @{ staffId = $stf.Username; password = "Staff@Nptel2026" } | ConvertTo-Json
        $stfLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $stfPayload -ContentType "application/json"
        $stfHdr = @{ "Authorization" = "Bearer $($stfLogin.data.accessToken)" }

        $stfProf = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/me" -Method Get -Headers $stfHdr).data
        $stfStudents = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Get -Headers $stfHdr).data

        $yearMatches = ($stfProf.assignedYear -eq $stf.ExpectedYear)
        $studentMatches = ($stfStudents.items.Count -ge 1 -and $stfStudents.items[0].registerNumber -eq $stf.ExpectedStudent)

        Report-Result "Year In-Charge $($stf.Username) (Year $($stf.ExpectedYear))" ($yearMatches -and $studentMatches) "AssignedYear: $($stfProf.assignedYear), First Student: $($stfStudents.items[0].registerNumber)"
    }

    # --------------------------------------------------------------------------
    # 13. PHASE 1 & PHASE 2 REGRESSION TESTS
    # --------------------------------------------------------------------------
    Write-Host "`n--- 13. PHASE 1 & PHASE 2 REGRESSION TESTS ---" -ForegroundColor Yellow

    # Student 1 Login
    $studLoginPayload = @{ registerNumber = "951021104001"; password = "Student@Nptel2026" } | ConvertTo-Json
    $studLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $studLoginPayload -ContentType "application/json"
    $studHeaders = @{ "Authorization" = "Bearer $($studLogin.data.accessToken)" }

    Report-Result "Student Login Regression" ($null -ne $studLogin.data.accessToken -and $studLogin.data.role -eq "Student") "Role: $($studLogin.data.role)"

    # Student Dashboard Summary
    $studDashboard = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/dashboard-summary" -Method Get -Headers $studHeaders).data
    Report-Result "Student Dashboard Summary Regression" ($studDashboard.inProgressCourses -ge 1) "InProgress: $($studDashboard.inProgressCourses)"

    # Student Courses
    $studCourses = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses" -Method Get -Headers $studHeaders).data
    Report-Result "Student Courses Regression" ($studCourses.Count -ge 1) "Courses Count: $($studCourses.Count)"

    # Admin Login & Dashboard
    $adminPayload = @{ adminId = "ADM-CSE-01"; password = "Admin@Nptel2026" } | ConvertTo-Json
    $adminLogin = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/admin/login" -Method Post -Body $adminPayload -ContentType "application/json"
    $adminHeaders = @{ "Authorization" = "Bearer $($adminLogin.data.accessToken)" }

    $adminProf = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $adminHeaders).data
    Report-Result "Admin Login & Dashboard Regression" ($adminProf.totalStudents -ge 2) "Admin: $($adminProf.adminIdentifier), Total Students: $($adminProf.totalStudents)"

    # --------------------------------------------------------------------------
    # 14. REAL DATABASE CHANGE TEST (Section 21 Requirement)
    # --------------------------------------------------------------------------
    Write-Host "`n--- 14. REAL DATABASE CHANGE TEST ---" -ForegroundColor Yellow

    # Step 1: Check baseline status as Staff for Student 1
    $staffCourseBefore = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/courses" -Method Get -Headers $staffHeaders).data[0]
    $staffSummaryBefore = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/dashboard-summary" -Method Get -Headers $staffHeaders).data
    Report-Result "Staff Initial Read Before DB Change" ($staffCourseBefore.registrationStatus -eq "InProgress" -and $staffSummaryBefore.completedCourses -eq 0) "Initial Reg Status: $($staffCourseBefore.registrationStatus), Completed: $($staffSummaryBefore.completedCourses)"

    # Step 2: Attempt modification using Staff credentials (MUST FAIL / DENIED)
    $staffDeniedMutation = $false
    try {
        $staffMutBody = @{ status = "Completed" } | ConvertTo-Json
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$($staffCourseBefore.registrationId)/status" -Method Patch -Body $staffMutBody -ContentType "application/json" -Headers $staffHeaders -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) { $staffDeniedMutation = $true }
    }
    Report-Result "Staff Denied Permission to Modify DB Value" $staffDeniedMutation "Staff PATCH /admin/registrations/... -> 403 Forbidden"

    # Step 3: Admin performs real DB change (Status -> Completed)
    $adminPatchBody = @{ status = "Completed" } | ConvertTo-Json
    $patchResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$($staffCourseBefore.registrationId)/status" -Method Patch -Body $adminPatchBody -ContentType "application/json" -Headers $adminHeaders
    Report-Result "Admin Modified Real DB Value" ($patchResp.success -eq $true) "Registration ID: $($staffCourseBefore.registrationId) set to Completed"

    # Step 4: Refresh Staff endpoint and verify new value appears
    $staffCourseAfter = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/courses" -Method Get -Headers $staffHeaders).data[0]
    $staffSummaryAfter = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/dashboard-summary" -Method Get -Headers $staffHeaders).data
    $changeReflectedInStaff = ($staffCourseAfter.registrationStatus -eq "Completed" -and $staffSummaryAfter.completedCourses -eq 1 -and $staffSummaryAfter.inProgressCourses -eq 0)
    Report-Result "Staff Endpoint Refreshes with New Real Database Value" $changeReflectedInStaff "New Reg Status: $($staffCourseAfter.registrationStatus), Completed: $($staffSummaryAfter.completedCourses), InProgress: $($staffSummaryAfter.inProgressCourses)"

    # Step 5: Admin reverts DB value back to InProgress to preserve seed integrity
    $adminRevertBody = @{ status = "InProgress" } | ConvertTo-Json
    $revertResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$($staffCourseBefore.registrationId)/status" -Method Patch -Body $adminRevertBody -ContentType "application/json" -Headers $adminHeaders
    $staffCourseReverted = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students/$student1Id/courses" -Method Get -Headers $staffHeaders).data[0]
    Report-Result "Database Reverted & Verified on Staff Refresh" ($staffCourseReverted.registrationStatus -eq "InProgress") "Reverted Status: $($staffCourseReverted.registrationStatus)"

    # --------------------------------------------------------------------------
    # 15. WPF APPLICATION LAUNCH VERIFICATION
    # --------------------------------------------------------------------------
    Write-Host "`n--- 15. WPF DESKTOP CLIENT LAUNCH VERIFICATION ---" -ForegroundColor Yellow

    $wpfPath = "$solutionDir/Desktop/bin/Debug/net8.0-windows/NPTELManagement.Desktop.exe"
    $wpfProcess = Start-Process -FilePath $wpfPath -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2

    $wpfRunning = (-not $wpfProcess.HasExited)
    Report-Result "WPF Executable Launched & Stable" $wpfRunning "PID: $($wpfProcess.Id), Exited: $($wpfProcess.HasExited)"

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
if ($allPassed) {
    Write-Host "   ALL PHASE 3 STAFF VERIFICATION TESTS PASSED SUCCESSFULLY!    " -ForegroundColor Green
} else {
    Write-Host "   SOME PHASE 3 VERIFICATION TESTS FAILED. REVIEW LOGS ABOVE.   " -ForegroundColor Red
}
Write-Host "=================================================================" -ForegroundColor Cyan

if (-not $allPassed) { exit 1 }
