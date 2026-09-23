# ============================================================================
# NPTEL Management System - Live API Integration Test Suite
# Tests all 16 authentication, authorization, health, and security cases
# ============================================================================

$baseUrl = "http://127.0.0.1:5000"
$results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Record-Test($num, $name, $expected, $actual, $passed, $details) {
    $obj = [PSCustomObject]@{
        Test = $num
        Name = $name
        Expected = $expected
        Actual = $actual
        Passed = if ($passed) { "PASS" } else { "FAIL" }
        Details = $details
    }
    $results.Add($obj)
    Write-Host "[$($obj.Passed)] Test ${num}: $name -> Actual: $actual ($details)"
}

Write-Host "Waiting for API to respond at $baseUrl..."
$connected = $false
for ($i = 0; $i -lt 15; $i++) {
    try {
        $h = Invoke-RestMethod -Uri "$baseUrl/api/v1/health" -Method Get -TimeoutSec 3 -ErrorAction Stop
        if ($h -ne $null) { $connected = $true; break }
    } catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $connected) {
    Write-Error "API did not start in time. Aborting tests."
    exit 1
}

# ----------------------------------------------------------------------------
# Test 16: Health Endpoint
# ----------------------------------------------------------------------------
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/api/v1/health" -Method Get
    $pass = ($health.status -ne $null) -and ($health.database -ne $null)
    Record-Test 16 "Health endpoint real DB status" "status & database present" "$($health.status)/$($health.database)" $pass "Status=$($health.status), DB=$($health.database)"
} catch {
    Record-Test 16 "Health endpoint real DB status" "200 OK" $_.Exception.Message $false $_.Exception.ToString()
}

# ----------------------------------------------------------------------------
# Test 1: Valid Student Login
# ----------------------------------------------------------------------------
$studentToken = $null
try {
    $body = @{ registerNumber = "951021104001"; password = "Student@Nptel2026" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/auth/student/login" -Method Post -Body $body -ContentType "application/json"
    $studentToken = $res.data.accessToken
    $pass = ($res.success -eq $true) -and (![string]::IsNullOrEmpty($studentToken))
    Record-Test 1 "Valid Student login" "200 OK + JWT" "200 OK" $pass "Token length: $($studentToken.Length)"
} catch {
    Record-Test 1 "Valid Student login" "200 OK + JWT" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 2: Invalid Student Password
# ----------------------------------------------------------------------------
try {
    $body = @{ registerNumber = "951021104001"; password = "WrongPassword123!" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/auth/student/login" -Method Post -Body $body -ContentType "application/json"
    Record-Test 2 "Invalid Student password" "401 Unauthorized" "200 OK" $false "Expected 401"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 401)
    Record-Test 2 "Invalid Student password" "401 Unauthorized" "$code" $pass "Correctly rejected bad password"
}

# ----------------------------------------------------------------------------
# Test 3: Valid Staff Login
# ----------------------------------------------------------------------------
$staffToken = $null
try {
    $body = @{ staffId = "CSE-STF-01"; password = "Staff@Nptel2026" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/auth/staff/login" -Method Post -Body $body -ContentType "application/json"
    $staffToken = $res.data.accessToken
    $pass = ($res.success -eq $true) -and (![string]::IsNullOrEmpty($staffToken))
    Record-Test 3 "Valid Staff login" "200 OK + JWT" "200 OK" $pass "Token length: $($staffToken.Length)"
} catch {
    Record-Test 3 "Valid Staff login" "200 OK + JWT" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 4: Invalid Staff Password
# ----------------------------------------------------------------------------
try {
    $body = @{ staffId = "CSE-STF-01"; password = "WrongPassword123!" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/auth/staff/login" -Method Post -Body $body -ContentType "application/json"
    Record-Test 4 "Invalid Staff password" "401 Unauthorized" "200 OK" $false ""
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 401)
    Record-Test 4 "Invalid Staff password" "401 Unauthorized" "$code" $pass "Correctly rejected bad password"
}

# ----------------------------------------------------------------------------
# Test 5: Valid Admin Login
# ----------------------------------------------------------------------------
$adminToken = $null
try {
    $body = @{ adminId = "ADM-CSE-01"; password = "Admin@Nptel2026" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/auth/admin/login" -Method Post -Body $body -ContentType "application/json"
    $adminToken = $res.data.accessToken
    $pass = ($res.success -eq $true) -and (![string]::IsNullOrEmpty($adminToken))
    Record-Test 5 "Valid Admin login" "200 OK + JWT" "200 OK" $pass "Token length: $($adminToken.Length)"
} catch {
    Record-Test 5 "Valid Admin login" "200 OK + JWT" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 6: Invalid Admin Password
# ----------------------------------------------------------------------------
try {
    $body = @{ adminId = "ADM-CSE-01"; password = "WrongPassword123!" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/auth/admin/login" -Method Post -Body $body -ContentType "application/json"
    Record-Test 6 "Invalid Admin password" "401 Unauthorized" "200 OK" $false ""
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 401)
    Record-Test 6 "Invalid Admin password" "401 Unauthorized" "$code" $pass "Correctly rejected bad password"
}

# ----------------------------------------------------------------------------
# Test 7: Student -> /student/me (Allowed)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $studentToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/student/me" -Method Get -Headers $headers
    $pass = ($res.success -eq $true) -and ($res.data.registerNumber -eq "951021104001")
    Record-Test 7 "Student -> /student/me" "200 OK (own data)" "200 OK" $pass "Name=$($res.data.name), Reg=$($res.data.registerNumber)"
} catch {
    Record-Test 7 "Student -> /student/me" "200 OK" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 8: Student -> /staff/students (Forbidden)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $studentToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/staff/students" -Method Get -Headers $headers
    Record-Test 8 "Student -> /staff/students" "403 Forbidden" "200 OK" $false "Student should not access staff endpoint"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 403)
    Record-Test 8 "Student -> /staff/students" "403 Forbidden" "$code" $pass "Correctly blocked cross-role access"
}

# ----------------------------------------------------------------------------
# Test 9: Student -> /admin/me (Forbidden)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $studentToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/admin/me" -Method Get -Headers $headers
    Record-Test 9 "Student -> /admin/me" "403 Forbidden" "200 OK" $false "Student should not access admin endpoint"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 403)
    Record-Test 9 "Student -> /admin/me" "403 Forbidden" "$code" $pass "Correctly blocked cross-role access"
}

# ----------------------------------------------------------------------------
# Test 10: Staff -> /staff/me (Allowed)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $staffToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/staff/me" -Method Get -Headers $headers
    $pass = ($res.success -eq $true) -and ($res.data.staffIdentifier -eq "CSE-STF-01")
    Record-Test 10 "Staff -> /staff/me" "200 OK" "200 OK" $pass "Staff=$($res.data.staffName), Year=$($res.data.assignedYear)"
} catch {
    Record-Test 10 "Staff -> /staff/me" "200 OK" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 11: Staff -> /staff/students (Allowed only within scope)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $staffToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/staff/students" -Method Get -Headers $headers
    # Staff is Year 3, Section A. Should see Student 1 (951021104001, Year 3) and NOT Student 2 (Year 1)
    $hasStudent1 = ($res.data | Where-Object { $_.registerNumber -eq "951021104001" }) -ne $null
    $hasStudent2 = ($res.data | Where-Object { $_.registerNumber -eq "951021104002" }) -ne $null
    $pass = ($res.success -eq $true) -and $hasStudent1 -and (-not $hasStudent2)
    Record-Test 11 "Staff -> /staff/students (Scoped)" "Only assigned students" "Count=$($res.data.Count)" $pass "Contains Year 3 ($hasStudent1), Excludes Year 1 ($((-not $hasStudent2)))"
} catch {
    Record-Test 11 "Staff -> /staff/students (Scoped)" "200 OK" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 12: Staff -> unassigned student /staff/students/{id} (Forbidden)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $staffToken" }
    # Student 2 ID (Year 1): ffffffff-ffff-ffff-ffff-ffffffffff02
    $unassignedStudentId = "ffffffff-ffff-ffff-ffff-ffffffffff02"
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/staff/students/$unassignedStudentId" -Method Get -Headers $headers
    Record-Test 12 "Staff -> unassigned student" "403 Forbidden" "200 OK" $false "Staff should not access unassigned student"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 403)
    Record-Test 12 "Staff -> unassigned student" "403 Forbidden" "$code" $pass "Cross-scope student access correctly blocked"
}

# ----------------------------------------------------------------------------
# Test 13: Staff -> /admin/me (Forbidden)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $staffToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/admin/me" -Method Get -Headers $headers
    Record-Test 13 "Staff -> /admin/me" "403 Forbidden" "200 OK" $false "Staff should not access admin endpoint"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 403)
    Record-Test 13 "Staff -> /admin/me" "403 Forbidden" "$code" $pass "Staff blocked from admin endpoint"
}

# ----------------------------------------------------------------------------
# Test 14: Admin -> /admin/me (Allowed, real counts)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer $adminToken" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/admin/me" -Method Get -Headers $headers
    $d = $res.data
    $pass = ($res.success -eq $true) -and ($d.totalStudents -ge 1) -and ($d.totalStaff -ge 1) -and ($d.totalCourses -ge 1)
    Record-Test 14 "Admin -> /admin/me" "200 OK (real counts)" "200 OK" $pass "Students=$($d.totalStudents), Staff=$($d.totalStaff), Courses=$($d.totalCourses), Regs=$($d.totalRegistrations), Certs=$($d.totalCertificates)"
} catch {
    Record-Test 14 "Admin -> /admin/me" "200 OK" $_.Exception.Message $false ""
}

# ----------------------------------------------------------------------------
# Test 15: Expired/Invalid JWT (Unauthorized)
# ----------------------------------------------------------------------------
try {
    $headers = @{ Authorization = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature" }
    $res = Invoke-RestMethod -Uri "$baseUrl/api/v1/student/me" -Method Get -Headers $headers
    Record-Test 15 "Invalid JWT" "401 Unauthorized" "200 OK" $false ""
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $pass = ($code -eq 401)
    Record-Test 15 "Invalid JWT" "401 Unauthorized" "$code" $pass "Malformed/invalid token rejected"
}

Write-Host "`n==========================================================="
Write-Host "TEST EXECUTION SUMMARY:"
Write-Host "==========================================================="
$results | Format-Table -AutoSize
$failed = ($results | Where-Object { $_.Passed -eq "FAIL" }).Count
if ($failed -eq 0) {
    Write-Host "ALL 16 TESTS PASSED SUCCESSFULLY!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "$failed TEST(S) FAILED!" -ForegroundColor Red
    exit 1
}
