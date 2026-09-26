(() => {
  const state = {
    role: "Student",
    token: sessionStorage.getItem("nptel_token"),
    user: JSON.parse(sessionStorage.getItem("nptel_user") || "null"),
    deferredInstall: null,
    currentPage: "dashboard",
    cache: new Map()
  };

  const $ = id => document.getElementById(id);
  const loginView = $("loginView");
  const dashboardView = $("dashboardView");
  const loadingView = $("loadingView");
  const loginMessage = $("loginMessage");
  const roleTabs = [...document.querySelectorAll(".role-tab")];

  const baseUrl = window.NPTEL_API_URL || "";
  const isLocal = ["localhost", "127.0.0.1"].includes(window.location.hostname);

  if (isLocal && "serviceWorker" in navigator) {
    window.addEventListener("load", async () => {
      try {
        const registrations = await navigator.serviceWorker.getRegistrations();
        await Promise.all(registrations.map(r => r.unregister()));
        const keys = await caches.keys();
        await Promise.all(keys.map(k => caches.delete(k)));
      } catch (_) {}
    });
  }

  window.addEventListener("beforeinstallprompt", e => {
    e.preventDefault();
    state.deferredInstall = e;
    $("installBtn").hidden = false;
  });

  $("installBtn").addEventListener("click", async () => {
    if (!state.deferredInstall) return;
    state.deferredInstall.prompt();
    await state.deferredInstall.userChoice;
    state.deferredInstall = null;
    $("installBtn").hidden = true;
  });

  roleTabs.forEach(tab => {
    tab.addEventListener("click", () => {
      roleTabs.forEach(t => t.classList.remove("active"));
      tab.classList.add("active");
      state.role = tab.dataset.role;
      $("identifierLabel").textContent =
        state.role === "Student" ? "Register Number" :
        state.role === "Staff" ? "Staff ID" : "Admin ID";
      $("identifier").placeholder =
        state.role === "Student" ? "e.g. 951021104001" :
        state.role === "Staff" ? "e.g. CSE-STF-01" : "e.g. ADM-CSE-01";
      loginMessage.textContent = "";
    });
  });

  $("loginForm").addEventListener("submit", async e => {
    e.preventDefault();
    loginMessage.className = "form-message";
    loginMessage.textContent = "";
    showLoading(true);

    const identifier = $("identifier").value.trim();
    const password = $("password").value;

    const endpoint =
      state.role === "Student" ? "/api/v1/auth/student/login" :
      state.role === "Staff" ? "/api/v1/auth/staff/login" :
      "/api/v1/auth/admin/login";

    const payload =
      state.role === "Student"
        ? { registerNumber: identifier, password }
        : state.role === "Staff"
          ? { staffId: identifier, password }
          : { adminId: identifier, password };

    try {
      const result = await request(endpoint, { method: "POST", body: payload, auth: false });
      const data = result?.data;
      if (!data?.accessToken) throw new Error(result?.message || "Login failed.");
      state.token = data.accessToken;
      state.user = data;
      sessionStorage.setItem("nptel_token", state.token);
      sessionStorage.setItem("nptel_user", JSON.stringify(state.user));
      state.cache.clear();
      await openDashboard();
    } catch (err) {
      loginMessage.textContent = err.message || "Unable to sign in.";
    } finally {
      showLoading(false);
    }
  });

  $("logoutBtn").addEventListener("click", async () => {
    try {
      if (state.token) await request("/api/v1/auth/logout", { method: "POST" });
    } catch (_) {}
    sessionStorage.removeItem("nptel_token");
    sessionStorage.removeItem("nptel_user");
    state.token = null;
    state.user = null;
    state.cache.clear();
    showLogin();
  });

  $("refreshBtn").addEventListener("click", () => loadPage(state.currentPage, true));

  async function request(path, opts = {}) {
    const headers = { "Content-Type": "application/json", ...(opts.headers || {}) };
    if (opts.auth !== false && state.token) headers.Authorization = "Bearer " + state.token;

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), opts.timeoutMs || 12000);

    let response;
    try {
      response = await fetch(baseUrl + path, {
        method: opts.method || "GET",
        headers,
        body: opts.body && !(opts.body instanceof FormData) ? JSON.stringify(opts.body) : opts.body,
        signal: controller.signal
      });
    } catch (error) {
      if (error?.name === "AbortError") {
        throw new Error("API request timed out: " + path);
      }
      throw new Error("Could not connect to the NPTEL API: " + (error?.message || "network error"));
    } finally {
      clearTimeout(timeout);
    }

    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch (_) {}

    if (!response.ok) {
      if (response.status === 401) {
        sessionStorage.removeItem("nptel_token");
        state.token = null;
      }
      throw new Error(json?.message || json?.errors?.[0] || "Request failed (" + response.status + ")");
    }
    return json;
  }

  async function cached(path, force = false) {
    if (!force && state.cache.has(path)) return state.cache.get(path);
    const value = await request(path);
    state.cache.set(path, value);
    return value;
  }

  async function openDashboard(force = false) {
    await loadPage("dashboard", force);
  }

  async function loadPage(page, force = false) {
    if (!state.token) return showLogin();

    state.currentPage = page;
    showLoading(true);

    try {
      renderShell();

      if (page === "dashboard") {
        await loadDashboard(force);
      } else if (page === "courses" && state.role === "Student") {
        await loadStudentCourses(force);
      } else if (page === "notifications" && state.role === "Student") {
        await loadStudentNotifications(force);
      } else if (page === "students" && state.role === "Staff") {
        await loadStaffStudents(force);
      } else if (page === "reports" && state.role === "Staff") {
        await loadStaffReports(force);
      } else if (page === "students" && state.role === "Admin") {
        await loadAdminStudents(force);
      } else if (page === "staff" && state.role === "Admin") {
        await loadAdminStaff(force);
      } else if (page === "courses" && state.role === "Admin") {
        await loadAdminCourses(force);
      } else if (page === "registrations" && state.role === "Admin") {
        await loadAdminRegistrations(force);
      } else if (page === "exams" && state.role === "Admin") {
        await loadAdminExams(force);
      } else if (page === "certificates" && state.role === "Admin") {
        await loadAdminCertificates(force);
      } else if (page === "notifications" && state.role === "Admin") {
        await loadAdminNotifications(force);
      } else if (page === "reports" && state.role === "Admin") {
        await loadAdminReports(force);
      } else if (page === "audit" && state.role === "Admin") {
        await loadAdminAudit(force);
      } else {
        await loadDashboard(force);
      }
    } catch (err) {
      if (!state.token) return showLogin();
      showPageMessage(err.message || "Unable to load this section.");
    } finally {
      showLoading(false);
    }
  }

  async function loadDashboard(force) {
    let profile, summary, primary;
    if (state.role === "Student") {
      profile = await cached("/api/v1/student/me", force);
      summary = await cached("/api/v1/student/dashboard-summary", force);
      primary = await cached("/api/v1/student/courses", force);
    } else if (state.role === "Staff") {
      profile = await cached("/api/v1/staff/me", force);
      summary = await cached("/api/v1/staff/dashboard-summary", force);
      primary = await cached("/api/v1/staff/students?page=1&pageSize=20", force);
    } else {
      profile = await cached("/api/v1/admin/me", force);
      summary = await cached("/api/v1/admin/dashboard-metrics", force);
      primary = await cached("/api/v1/admin/students?page=1&pageSize=20", force);
    }

    setPageTitle("Dashboard", state.role + " portal");
    renderWelcome();
    renderStats(summary?.data || {});
    renderProfile(profile?.data || {});
    renderDashboardList(primary?.data || []);
    $("refreshBtn").hidden = false;
  }

  async function loadStudentCourses(force) {
    const result = await cached("/api/v1/student/courses", force);
    setPageTitle("My Courses", "Student portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Your NPTEL learning journey", "Open any course to view timeline, exam and certificate details.");
    const courses = Array.isArray(result?.data) ? result.data : [];
    $("primaryHeading").textContent = "Registered Courses";
    $("primaryContent").innerHTML = courseCards(courses);
    bindCourseButtons(courses);
    $("profileContent").innerHTML = '<div class="empty">Select a course to view full details.</div>';
  }

  async function openStudentCourse(registrationId) {
    showLoading(true);
    try {
      const result = await cached("/api/v1/student/courses/" + registrationId, false);
      const d = result?.data;
      setPageTitle(d?.courseName || "Course Details", "Student portal");
      $("statsGrid").innerHTML = "";
      $("welcomeBanner").innerHTML = banner(
        d?.courseName || "Course",
        (d?.courseCode || "") + " · " + (d?.registrationStatus || "")
      );
      $("primaryHeading").textContent = "Course Timeline";
      $("primaryContent").innerHTML = courseDetailView(d);
      $("profileContent").innerHTML = examAndCertificateView(d, true);
    } catch (err) {
      showPageMessage(err.message);
    } finally {
      showLoading(false);
    }
  }

  async function loadStudentNotifications(force) {
    const result = await cached("/api/v1/student/notifications", force);
    setPageTitle("Notifications", "Student portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Notifications", "Updates related to your NPTEL activity.");
    $("primaryHeading").textContent = "Recent Notifications";
    const items = Array.isArray(result?.data) ? result.data : [];
    $("primaryContent").innerHTML = notificationCards(items);
    $("profileContent").innerHTML = '<div class="empty">Unread items can be marked as read.</div>';
    document.querySelectorAll("[data-read]").forEach(btn => {
      btn.addEventListener("click", async () => {
        try {
          await request("/api/v1/student/notifications/" + btn.dataset.read + "/read", { method: "POST", body: {} });
          state.cache.delete("/api/v1/student/notifications");
          await loadStudentNotifications(true);
        } catch (e) {
          alert(e.message);
        }
      });
    });
  }

  async function loadStaffStudents(force) {
    const result = await cached("/api/v1/staff/students?page=1&pageSize=50", force);
    setPageTitle("Students", "Staff portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Assigned Students", "Read-only student view within your Year/Class scope.");
    const items = result?.data?.items || [];
    $("primaryHeading").textContent = "Student List";
    $("primaryContent").innerHTML = tableCards(items, "staff");
    $("profileContent").innerHTML = '<div class="empty">Click View to inspect a student and their NPTEL progress.</div>';
    bindDetailButtons();
  }

  async function openStaffStudent(studentId) {
    showLoading(true);
    try {
      const result = await request("/api/v1/staff/students/" + studentId);
      const d = result?.data;
      setPageTitle(d?.name || "Student", "Staff portal");
      $("statsGrid").innerHTML = "";
      $("welcomeBanner").innerHTML = banner(d?.name || "Student", [d?.registerNumber, d?.department, d?.year ? "Year " + d.year : null, d?.classSection ? "Class " + d.classSection : null].filter(Boolean).join(" · "));
      $("primaryHeading").textContent = "NPTEL Courses";
      $("primaryContent").innerHTML = tableCards(d?.courses || [], "staff-course");
      $("profileContent").innerHTML = profileRows(d, ["studentId","name","registerNumber","department","classSection","year","batch","email","phone"]);
    } catch (err) {
      showPageMessage(err.message);
    } finally {
      showLoading(false);
    }
  }

  async function loadStaffReports(force) {
    const result = await cached("/api/v1/staff/reports/preview?reportType=student-registration", force);
    const d = result?.data;
    setPageTitle("Reports", "Staff portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner(d?.reportTitle || "Scoped Report", "Read-only report preview for your assigned scope.");
    $("primaryHeading").textContent = "Report Preview";
    $("primaryContent").innerHTML = tableCards(d?.items || [], "report");
    $("profileContent").innerHTML = profileRows(d, ["department","year","classSection","totalRecords","generatedAt"]);
  }

  async function loadAdminStudents(force) {
    const result = await cached("/api/v1/admin/students?page=1&pageSize=50", force);
    setPageTitle("Students", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Student Management", "View and manage student records.");
    const items = result?.data?.items || [];
    $("primaryHeading").textContent = "Students";
    $("primaryContent").innerHTML = tableCards(items, "admin-student");
    $("profileContent").innerHTML = '<div class="empty">Use the dashboard for summary metrics.</div>';
    bindDetailButtons();
  }

  async function loadAdminStaff(force) {
    const result = await cached("/api/v1/admin/staff?page=1&pageSize=50", force);
    setPageTitle("Staff", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Staff Management", "View department in-charge assignments.");
    $("primaryHeading").textContent = "Staff Members";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "admin-staff");
    $("profileContent").innerHTML = '<div class="empty">Staff creation/edit actions can be added here next.</div>';
  }

  async function loadAdminCourses(force) {
    const result = await cached("/api/v1/admin/courses?page=1&pageSize=50", force);
    setPageTitle("Courses", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Course Management", "NPTEL courses currently configured in the system.");
    $("primaryHeading").textContent = "Courses";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "admin-course");
    $("profileContent").innerHTML = '<div class="empty">Course CRUD actions can be added here next.</div>';
  }

  async function loadAdminRegistrations(force) {
    const result = await cached("/api/v1/admin/registrations?page=1&pageSize=50", force);
    setPageTitle("Registrations", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("NPTEL Registrations", "Registration and course-status overview.");
    $("primaryHeading").textContent = "Registrations";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "admin-registration");
    $("profileContent").innerHTML = '<div class="empty">Registration status management can be added here next.</div>';
  }

  async function loadAdminExams(force) {
    const result = await cached("/api/v1/admin/exams?page=1&pageSize=50", force);
    setPageTitle("Exams", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Exam Management", "Track application, schedule, score and pass status.");
    $("primaryHeading").textContent = "Exam Records";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "admin-exam");
    $("profileContent").innerHTML = '<div class="empty">Exam update actions can be added here next.</div>';
  }

  async function loadAdminCertificates(force) {
    const result = await cached("/api/v1/admin/certificates?page=1&pageSize=50", force);
    setPageTitle("Certificates", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Certificate Management", "Verification and private-storage access.");
    $("primaryHeading").textContent = "Certificates";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "admin-certificate");
    $("profileContent").innerHTML = '<div class="empty">Use the signed access action to open a certificate when available.</div>';
    bindCertificateAccessButtons();
  }

  async function loadAdminNotifications(force) {
    const result = await cached("/api/v1/admin/notifications?page=1&pageSize=50", force);
    setPageTitle("Notifications", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Notifications", "View dispatched academic notifications.");
    $("primaryHeading").textContent = "Notification History";
    $("primaryContent").innerHTML = notificationCards(result?.data?.items || []);
    $("profileContent").innerHTML = '<div class="empty">Notification creation is available through the API and can be added to this UI next.</div>';
  }

  async function loadAdminReports(force) {
    const path = "/api/v1/admin/reports/preview";
    const result = force || !state.cache.has(path)
      ? await request(path, { method: "POST", body: { reportType: "student-registration", department: "CSE" } })
      : state.cache.get(path);
    state.cache.set(path, result);
    const d = result?.data;
    setPageTitle("Reports", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner(d?.reportTitle || "Academic Report", "Preview data from the administrator report engine.");
    $("primaryHeading").textContent = "Report Preview";
    $("primaryContent").innerHTML = tableCards(d?.items || [], "report");
    $("profileContent").innerHTML = profileRows(d, ["department","totalRecords","generatedAt"]);
  }

  async function loadAdminAudit(force) {
    const result = await cached("/api/v1/admin/audit-logs?page=1&pageSize=50", force);
    setPageTitle("Audit Logs", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Audit Trail", "Recent administrative and authentication activity.");
    $("primaryHeading").textContent = "Audit Logs";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "audit");
    $("profileContent").innerHTML = '<div class="empty">Audit records are read-only.</div>';
  }

  function renderShell() {
    loginView.hidden = true;
    dashboardView.hidden = false;
    $("logoutBtn").hidden = false;
    $("roleBadge").textContent = state.role;

    const menus = state.role === "Student"
      ? [["Dashboard","dashboard"],["Courses","courses"],["Notifications","notifications"]]
      : state.role === "Staff"
        ? [["Dashboard","dashboard"],["Students","students"],["Reports","reports"]]
        : [["Dashboard","dashboard"],["Students","students"],["Staff","staff"],["Courses","courses"],["Registrations","registrations"],["Exams","exams"],["Certificates","certificates"],["Notifications","notifications"],["Reports","reports"],["Audit Logs","audit"]];

    $("nav").innerHTML = menus.map(([label,key]) =>
      '<button class="nav-item ' + (key === state.currentPage ? "active" : "") + '" data-nav="' + key + '">' + label + '</button>'
    ).join("");

    document.querySelectorAll("[data-nav]").forEach(btn => {
      btn.addEventListener("click", () => loadPage(btn.dataset.nav));
    });
  }

  function setPageTitle(title, eyebrow) {
    $("pageTitle").textContent = title;
    $("pageEyebrow").textContent = eyebrow;
  }

  function renderWelcome() {
    const name = state.user?.name || state.user?.identifier || "User";
    $("welcomeBanner").innerHTML = banner("Welcome back, " + escapeHtml(name) + " 👋", "Role: " + escapeHtml(state.role) + " · Secure session active");
  }

  function renderStats(payload) {
    const configs =
      state.role === "Student"
        ? [["Registered",payload.registeredCourses],["In Progress",payload.inProgressCourses],["Completed",payload.completedCourses],["Certificates Verified",payload.certificatesVerified]]
        : state.role === "Staff"
          ? [["Students",payload.totalStudents],["Registered",payload.registeredStudents],["In Progress",payload.inProgressCourses],["Certificates",payload.certificateVerified]]
          : [["Students",payload.totalStudents],["Staff",payload.totalStaff],["Courses",payload.totalCourses],["Certificates",payload.totalCertificates]];

    $("statsGrid").innerHTML = configs.map(([label,value]) =>
      '<div class="stat"><div class="stat-label">' + escapeHtml(label) + '</div><div class="stat-value">' + escapeHtml(value ?? "0") + '</div></div>'
    ).join("");
  }

  function renderProfile(profile) {
    $("profileContent").innerHTML = profileRows(profile, Object.keys(profile || {}).filter(k => typeof profile[k] !== "object").slice(0, 10));
  }

  function renderDashboardList(result) {
    $("primaryHeading").textContent =
      state.role === "Student" ? "My NPTEL Courses" :
      state.role === "Staff" ? "Scoped Students" : "Student Management";

    const items = Array.isArray(result) ? result : (result?.items || result?.Items || []);
    $("primaryContent").innerHTML = state.role === "Student"
      ? courseCards(items)
      : tableCards(items, state.role === "Staff" ? "staff" : "admin-student");

    if (state.role === "Student") bindCourseButtons(items);
    else bindDetailButtons();
  }

  function courseCards(items) {
    if (!items.length) return '<div class="empty">No courses found.</div>';
    return '<div class="data-list">' + items.map(item =>
      '<div class="data-item">' +
      '<div><div class="data-title">' + escapeHtml(item.courseName || "Course") + '</div>' +
      '<div class="data-meta">' + escapeHtml([item.courseCode, item.registrationStatus, item.durationWeeks ? item.durationWeeks + " weeks" : ""].filter(Boolean).join(" · ")) + '</div></div>' +
      '<div class="row-actions"><button class="ghost-btn small" data-course="' + escapeHtml(item.registrationId) + '">Open</button></div>' +
      '</div>'
    ).join("") + '</div>';
  }

  function tableCards(items, type) {
    if (!items?.length) return '<div class="empty">No records found.</div>';
    return '<div class="data-list">' + items.map(item => {
      let title = "Record";
      let meta = [];
      if (type.includes("student")) {
        title = item.name || item.studentName || "Student";
        meta = [item.registerNumber, item.department, item.classSection, item.year ? "Year " + item.year : null].filter(Boolean);
      } else if (type === "staff") {
        title = item.staffName || "Staff";
        meta = [item.staffIdentifier, item.department, item.assignedYear ? "Year " + item.assignedYear : null, item.assignedClass ? "Class " + item.assignedClass : null].filter(Boolean);
      } else if (type.includes("course")) {
        title = item.courseName || "Course";
        meta = [item.courseCode, item.status, item.durationWeeks ? item.durationWeeks + " weeks" : null].filter(Boolean);
      } else if (type.includes("registration")) {
        title = item.courseName || "Registration";
        meta = [item.studentName, item.registerNumber, item.status, item.examApplicationStatus, item.certificateStatus].filter(Boolean);
      } else if (type.includes("exam")) {
        title = item.studentName || "Exam";
        meta = [item.registerNumber, item.courseName, item.examStatus, item.passStatus, item.score != null ? "Score " + item.score : null].filter(Boolean);
      } else if (type.includes("certificate")) {
        title = item.studentName || "Certificate";
        meta = [item.registerNumber, item.courseName, item.verifiedStatus].filter(Boolean);
      } else if (type === "report") {
        title = item.studentName || "Report row";
        meta = [item.registerNumber, item.courseName, item.registrationStatus, item.examStatus, item.certificateStatus || item.verificationStatus].filter(Boolean);
      } else if (type === "audit") {
        title = item.action || "Audit event";
        meta = [item.username, item.role, item.timestamp ? formatDate(item.timestamp) : null, item.details].filter(Boolean);
      }
      let action = "";
      if (type === "staff" || type.includes("student")) {
        const id = item.studentId || item.StudentId;
        if (id) action = '<button class="ghost-btn small" data-student="' + id + '">View</button>';
      }
      if (type.includes("certificate") && item.certificateId && item.hasFile) {
        action = '<button class="ghost-btn small" data-cert="' + item.certificateId + '">Open</button>';
      }
      return '<div class="data-item"><div><div class="data-title">' + escapeHtml(title) + '</div><div class="data-meta">' + escapeHtml(meta.join(" · ")) + '</div></div>' +
        (action ? '<div class="row-actions">' + action + '</div>' : "") + '</div>';
    }).join("") + '</div>';
  }

  function examAndCertificateView(d, allowStudent) {
    const e = d?.exam || {};
    const c = d?.certificate || {};
    const certBtn = allowStudent && c.certificateId
      ? '<button class="ghost-btn small" data-cert="' + c.certificateId + '">Open Certificate</button>'
      : "";
    return '<div class="profile-list">' +
      '<div class="profile-row"><span>Exam</span><span>' + escapeHtml([e.examStatus,e.hallTicketStatus,e.passStatus,e.score != null ? "Score " + e.score : null].filter(Boolean).join(" · ") || "Not available") + '</span></div>' +
      '<div class="profile-row"><span>Certificate</span><span>' + escapeHtml(c.verifiedStatus || "Not available") + '</span></div>' +
      '<div class="row-actions">' + certBtn + '</div></div>';
  }

  function courseDetailView(d) {
    const items = d?.timeline || [];
    if (!items.length) return '<div class="empty">No timeline records.</div>';
    return '<div class="data-list">' + items.map(x =>
      '<div class="data-item"><div class="data-title">' + escapeHtml(x.title) + '</div><div class="data-meta">' +
      escapeHtml([x.status, x.eventDate ? formatDate(x.eventDate) : null, x.description].filter(Boolean).join(" · ")) + '</div></div>'
    ).join("") + '</div>';
  }

  function notificationCards(items) {
    if (!items.length) return '<div class="empty">No notifications found.</div>';
    return '<div class="data-list">' + items.map(x =>
      '<div class="data-item"><div class="data-title">' + escapeHtml(x.title) + '</div>' +
      '<div class="data-meta">' + escapeHtml([formatDate(x.createdAt), x.message].filter(Boolean).join(" · ")) + '</div>' +
      (!x.isRead && x.notificationId ? '<div class="row-actions"><button class="ghost-btn small" data-read="' + x.notificationId + '">Mark read</button></div>' : '') +
      '</div>'
    ).join("") + '</div>';
  }

  function profileRows(obj, keys) {
    const rows = (keys || []).filter(k => obj && obj[k] !== null && obj[k] !== undefined && typeof obj[k] !== "object");
    if (!rows.length) return '<div class="empty">No profile data.</div>';
    return '<div class="profile-list">' + rows.map(k =>
      '<div class="profile-row"><span>' + prettyKey(k) + '</span><span>' + escapeHtml(formatValue(obj[k])) + '</span></div>'
    ).join("") + '</div>';
  }

  function bindCourseButtons(items) {
    document.querySelectorAll("[data-course]").forEach(btn => btn.addEventListener("click", () => openStudentCourse(btn.dataset.course)));
  }

  function bindDetailButtons() {
    document.querySelectorAll("[data-student]").forEach(btn => btn.addEventListener("click", async () => {
      if (state.role === "Staff") await openStaffStudent(btn.dataset.student);
      else await openAdminStudent(btn.dataset.student);
    }));
  }

  async function openAdminStudent(studentId) {
    showLoading(true);
    try {
      const result = await request("/api/v1/admin/students/" + studentId);
      const d = result?.data;
      setPageTitle(d?.name || "Student", "Admin portal");
      $("statsGrid").innerHTML = "";
      $("welcomeBanner").innerHTML = banner(d?.name || "Student", [d?.registerNumber, d?.department, d?.year ? "Year " + d.year : null, d?.classSection ? "Class " + d.classSection : null].filter(Boolean).join(" · "));
      $("primaryHeading").textContent = "Registrations";
      $("primaryContent").innerHTML = tableCards(d?.registrations || [], "admin-registration");
      $("profileContent").innerHTML = profileRows(d, ["studentId","name","registerNumber","department","classSection","year","batch","email","phone","isActive"]);
    } catch (err) {
      showPageMessage(err.message);
    } finally {
      showLoading(false);
    }
  }

  function bindCertificateAccessButtons() {
    document.querySelectorAll("[data-cert]").forEach(btn => btn.addEventListener("click", async () => {
      try {
        const endpoint = state.role === "Student"
          ? "/api/v1/student/certificates/" + btn.dataset.cert + "/access"
          : state.role === "Staff"
            ? "/api/v1/staff/certificates/" + btn.dataset.cert + "/access"
            : "/api/v1/admin/certificates/" + btn.dataset.cert + "/access";
        const result = await request(endpoint);
        const url = result?.data?.accessUrl;
        if (url) window.open(url, "_blank", "noopener");
        else alert("Certificate URL was not returned.");
      } catch (err) {
        alert(err.message);
      }
    }));
  }

  function showPageMessage(message) {
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Unable to load this section", message);
    $("primaryHeading").textContent = "Error";
    $("primaryContent").innerHTML = '<div class="empty">' + escapeHtml(message) + '</div>';
    $("profileContent").innerHTML = "";
  }

  function banner(title, subtitle) {
    return '<div class="big">' + escapeHtml(title) + '</div><div class="small">' + escapeHtml(subtitle || "") + '</div>';
  }

  function formatValue(v) {
    if (typeof v === "boolean") return v ? "Yes" : "No";
    if (typeof v === "string" && v.includes("T") && !Number.isNaN(Date.parse(v))) return formatDate(v);
    return String(v);
  }

  function formatDate(v) {
    try { return new Date(v).toLocaleString(); } catch (_) { return String(v); }
  }

  function prettyKey(key) {
    return key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, c => c.toUpperCase());
  }

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, c => ({ "&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#039;" }[c]));
  }

  function showLogin() {
    loginView.hidden = false;
    dashboardView.hidden = true;
    $("logoutBtn").hidden = true;
    $("installBtn").hidden = !state.deferredInstall;
    $("roleBadge").textContent = "Guest";
    $("pageEyebrow").textContent = "Academic portal";
    $("pageTitle").textContent = "Welcome";
    $("nav").innerHTML = "";
  }

  function showLoading(_visible) {
    // Navigation must remain clickable even while API requests are running.
    loadingView.hidden = true;
  }

  async function checkApi() {
    try {
      const res = await fetch(baseUrl + "/api/v1/health/live");
      $("apiStatus").textContent = res.ok ? "API: online" : "API: unavailable";
    } catch (_) {
      $("apiStatus").textContent = "API: unavailable";
    }
  }

  if ("serviceWorker" in navigator && !isLocal) {
    window.addEventListener("load", () => navigator.serviceWorker.register("/sw.js").catch(() => {}));
  }

  checkApi();
  if (state.token) openDashboard();
  else showLogin();
})();