# ==============================================================================
# PHASE 2 - REAL NPTEL COURSE & STUDENT WORKFLOW VERIFICATION SUITE
# ==============================================================================

$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$solutionDir = Split-Path -Parent $PSScriptRoot
Set-Location $solutionDir

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "    PHASE 2: REAL NPTEL COURSE & STUDENT WORKFLOW VERIFICATION   " -ForegroundColor Cyan
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
    # ------------------------------------------------------------------------------
    # 3. STUDENT AUTHENTICATION
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 3. STUDENT AUTHENTICATION & TOKENS ---" -ForegroundColor Yellow

    # Student 1 Login
    $loginBody1 = @{ registerNumber = "951021104001"; password = "Student@Nptel2026" } | ConvertTo-Json
    $loginResp1 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $loginBody1 -ContentType "application/json"
    $student1Token = $loginResp1.data.accessToken
    Report-Result "Student 1 Login (951021104001)" ($null -ne $student1Token -and $student1Token.Length -gt 20) "Token acquired: $($student1Token.Substring(0, 15))..."

    # Student 2 Login
    $loginBody2 = @{ registerNumber = "951021104002"; password = "Student@Nptel2026" } | ConvertTo-Json
    $loginResp2 = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/student/login" -Method Post -Body $loginBody2 -ContentType "application/json"
    $student2Token = $loginResp2.data.accessToken
    Report-Result "Student 2 Login (951021104002)" ($null -ne $student2Token -and $student2Token.Length -gt 20) "Token acquired: $($student2Token.Substring(0, 15))..."

    $student1Headers = @{ Authorization = "Bearer $student1Token" }
    $student2Headers = @{ Authorization = "Bearer $student2Token" }

    # ------------------------------------------------------------------------------
    # 4. REAL STUDENT DASHBOARD SUMMARY
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 4. STUDENT DASHBOARD SUMMARY (REAL COUNTS) ---" -ForegroundColor Yellow

    $summaryResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/dashboard-summary" -Method Get -Headers $student1Headers
    $summary = $summaryResp.data
    $summaryValid = ($summary.inProgressCourses -eq 1 -and $summary.examPending -eq 1 -and $summary.certificatesPending -eq 1)

    Report-Result "Real Dashboard Summary Counts" $summaryValid "InProgress: $($summary.inProgressCourses), ExamPending: $($summary.examPending), CertPending: $($summary.certificatesPending)"

    # ------------------------------------------------------------------------------
    # 5. STUDENT COURSES LIST
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 5. STUDENT COURSES LIST ---" -ForegroundColor Yellow

    $coursesResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses" -Method Get -Headers $student1Headers
    $courses = $coursesResp.data
    $course1 = $courses[0]
    $coursesValid = ($courses.Count -ge 1 -and $course1.courseCode -eq "noc24-cs01" -and $course1.registrationStatus -eq "InProgress")

    Report-Result "Student Courses List" $coursesValid "Found $($courses.Count) course(s). First: $($course1.courseCode) - $($course1.courseName)"

    $reg1Id = $course1.registrationId

    # ------------------------------------------------------------------------------
    # 6. COURSE DETAILS, TIMELINE, EXAM & CERTIFICATE
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 6. COURSE DETAILS & 11-STAGE TIMELINE ---" -ForegroundColor Yellow

    $detailsResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg1Id" -Method Get -Headers $student1Headers
    $details = $detailsResp.data

    # Verify Timeline
    $timeline = $details.timeline
    $timelineValid = ($timeline.Count -eq 11 -and $timeline[0].title -eq "Course Registered" -and $timeline[0].status -eq "Completed" -and $timeline[2].title -eq "Course Completed" -and $timeline[2].status -eq "Current")
    Report-Result "11-Stage Milestone Timeline" $timelineValid "Milestones count: $($timeline.Count). Stage 1: $($timeline[0].title) ($($timeline[0].status)), Stage 3: $($timeline[2].title) ($($timeline[2].status))"

    # Verify Exam
    $exam = $details.exam
    $examValid = ($exam.examApplicationStatus -eq "Applied" -and $exam.examStatus -eq "Scheduled" -and $exam.hallTicketStatus -eq "Available")
    Report-Result "Exam Status Details" $examValid "Application: $($exam.examApplicationStatus), Status: $($exam.examStatus), HallTicket: $($exam.hallTicketStatus)"

    # Verify Certificate
    $cert = $details.certificate
    $certValid = ($cert.verifiedStatus -eq "Pending" -and $null -eq $cert.storagePath)
    Report-Result "Certificate Status Details" $certValid "Status: $($cert.verifiedStatus), Safe metadata only: $($null -eq $cert.storagePath)"

    # Sub-endpoints check
    $timelineSub = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg1Id/timeline" -Method Get -Headers $student1Headers).data
    Report-Result "Dedicated Timeline Endpoint" ($timelineSub.Count -eq 11) "Count: $($timelineSub.Count)"

    $examSub = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg1Id/exam" -Method Get -Headers $student1Headers).data
    Report-Result "Dedicated Exam Endpoint" ($examSub.examStatus -eq "Scheduled") "Status: $($examSub.examStatus)"

    $certSub = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg1Id/certificate" -Method Get -Headers $student1Headers).data
    Report-Result "Dedicated Certificate Endpoint" ($certSub.verifiedStatus -eq "Pending") "Status: $($certSub.verifiedStatus)"

    # ------------------------------------------------------------------------------
    # 7. NOTIFICATIONS & MARK AS READ
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 7. NOTIFICATIONS & MARK AS READ ---" -ForegroundColor Yellow

    $notifsResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/notifications" -Method Get -Headers $student1Headers
    $notifs = $notifsResp.data
    $unreadNotif = $notifs | Where-Object { -not $_.isRead } | Select-Object -First 1

    Report-Result "Student Notifications Retrieved" ($notifs.Count -ge 2) "Total notifications: $($notifs.Count), Unread found: $($null -ne $unreadNotif)"

    if ($null -ne $unreadNotif) {
        $readResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/notifications/$($unreadNotif.notificationId)/read" -Method Post -Headers $student1Headers
        Report-Result "Mark Notification As Read" ($readResp.success -eq $true) "Notification $($unreadNotif.notificationId) marked as read"

        # Verify state is updated
        $updatedNotifs = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/notifications" -Method Get -Headers $student1Headers).data
        $target = $updatedNotifs | Where-Object { $_.notificationId -eq $unreadNotif.notificationId }
        Report-Result "Verified Notification Persisted As Read" ($target.isRead -eq $true) "isRead: $($target.isRead)"
    }

    # ------------------------------------------------------------------------------
    # 8. STRICT STUDENT OWNERSHIP & SECURITY
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 8. STRICT STUDENT OWNERSHIP & SECURITY ENFORCEMENT ---" -ForegroundColor Yellow

    # Get Student 2's registration ID
    $student2Courses = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses" -Method Get -Headers $student2Headers).data
    $reg2Id = $student2Courses[0].registrationId

    # Student 1 attempts to access Student 2's course details
    $crossAccessDenied = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg2Id" -Method Get -Headers $student1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) {
            $crossAccessDenied = $true
        }
    }
    Report-Result "Student 1 CANNOT access Student 2's course (returns 404)" $crossAccessDenied "Status 404 received"

    # Student 1 attempts to access Student 2's timeline
    $crossTimelineDenied = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg2Id/timeline" -Method Get -Headers $student1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) {
            $crossTimelineDenied = $true
        }
    }
    Report-Result "Student 1 CANNOT access Student 2's timeline (returns 404)" $crossTimelineDenied "Status 404 received"

    # Student 1 attempts to mark Student 2's notification as read
    $student2Notifs = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/notifications" -Method Get -Headers $student2Headers).data
    $student2NotifId = $student2Notifs[0].notificationId

    $crossNotifDenied = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/notifications/$student2NotifId/read" -Method Post -Headers $student1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) {
            $crossNotifDenied = $true
        }
    }
    Report-Result "Student 1 CANNOT mark Student 2's notification as read (returns 404)" $crossNotifDenied "Status 404 received"

    # Student attempts Staff endpoint
    $studentStaffForbidden = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Get -Headers $student1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) {
            $studentStaffForbidden = $true
        }
    }
    Report-Result "Student CANNOT access Staff endpoints (returns 403)" $studentStaffForbidden "Status 403 received"

    # Student attempts Admin endpoint
    $studentAdminForbidden = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers $student1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) {
            $studentAdminForbidden = $true
        }
    }
    Report-Result "Student CANNOT access Admin endpoints (returns 403)" $studentAdminForbidden "Status 403 received"

    # Missing JWT
    $missingJwtUnauthorized = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses" -Method Get -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) {
            $missingJwtUnauthorized = $true
        }
    }
    Report-Result "Missing JWT returns 401 Unauthorized" $missingJwtUnauthorized "Status 401 received"

    # Invalid JWT
    $invalidJwtUnauthorized = $false
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses" -Method Get -Headers @{ Authorization = "Bearer BadToken12345.Invalid.JWT" } -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) {
            $invalidJwtUnauthorized = $true
        }
    }
    Report-Result "Invalid JWT returns 401 Unauthorized" $invalidJwtUnauthorized "Status 401 received"

    # ------------------------------------------------------------------------------
    # 9. REAL DATABASE VALUE CHANGE & DYNAMIC REFRESH VERIFICATION (SECTION 34)
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 9. REAL DATABASE VALUE CHANGE & DYNAMIC REFRESH ---" -ForegroundColor Yellow

    # Admin Login to obtain Admin Token
    $adminLoginBody = @{ adminId = "ADM-CSE-01"; password = "Admin@Nptel2026" } | ConvertTo-Json
    $adminToken = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/admin/login" -Method Post -Body $adminLoginBody -ContentType "application/json").data.accessToken

    # Change real exam date in database
    $newExamDate = (Get-Date).AddDays(45).ToUniversalTime().ToString("o")
    $patchBody = @{ examDate = $newExamDate } | ConvertTo-Json
    $patchResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/registrations/$reg1Id/exam-date" -Method Patch -Body $patchBody -ContentType "application/json" -Headers @{ Authorization = "Bearer $adminToken" }
    Report-Result "Real DB Value Update Triggered" ($patchResp.success -eq $true) "Updated examDate in database to $newExamDate"

    # Refresh Student Course Details and verify the new value appears dynamically
    $refreshedDetails = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/student/courses/$reg1Id" -Method Get -Headers $student1Headers).data
    $refreshedExamDate = [DateTime]::Parse($refreshedDetails.exam.examDate).ToUniversalTime()
    $expectedDate = [DateTime]::Parse($newExamDate).ToUniversalTime()
    $dateMatches = ([Math]::Abs(($refreshedExamDate - $expectedDate).TotalSeconds) -lt 5)

    Report-Result "Student Refreshed Data Reflects Real Database Change" $dateMatches "New DB ExamDate reflected dynamically: $($refreshedDetails.exam.examDate)"

    # ------------------------------------------------------------------------------
    # 10. PHASE 1 REGRESSION SUITE
    # ------------------------------------------------------------------------------
    Write-Host "`n--- 10. PHASE 1 REGRESSION VERIFICATION ---" -ForegroundColor Yellow

    # Staff Login
    $staffLoginBody = @{ staffId = "CSE-STF-01"; password = "Staff@Nptel2026" } | ConvertTo-Json
    $staffToken = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/staff/login" -Method Post -Body $staffLoginBody -ContentType "application/json").data.accessToken
    Report-Result "Staff Login (CSE-STF-01)" ($null -ne $staffToken) "Token received"

    # Staff Assigned Students
    $staffStudents = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/staff/students" -Method Get -Headers @{ Authorization = "Bearer $staffToken" }).data
    Report-Result "Staff Assigned Students" ($staffStudents.Count -ge 1) "Students in scope: $($staffStudents.Count)"

    # Admin Live Counts
    $adminData = (Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/admin/me" -Method Get -Headers @{ Authorization = "Bearer $adminToken" }).data
    $adminCountsValid = ($adminData.totalStudents -ge 2 -and $adminData.totalCourses -ge 2)
    Report-Result "Admin Live Counts" $adminCountsValid "Students: $($adminData.totalStudents), Courses: $($adminData.totalCourses)"

    # Logout
    $logoutResp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/v1/auth/logout" -Method Post -Headers @{ Authorization = "Bearer $student1Token" }
    Report-Result "Logout Endpoint" ($logoutResp.success -eq $true) "Session cleared"

}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Write-Host "`nStopping API process (PID: $($apiProcess.Id))..." -ForegroundColor Gray
        $apiProcess.Kill()
        $apiProcess.WaitForExit()
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "     ALL PHASE 2 STUDENT WORKFLOW & SECURITY TESTS PASSED!       " -ForegroundColor Green
} else {
    Write-Host "     SOME TESTS FAILED! CHECK OUTPUT ABOVE.                      " -ForegroundColor Red
}
Write-Host "=================================================================" -ForegroundColor Cyan

if (-not $allPassed) { exit 1 } else { exit 0 }
