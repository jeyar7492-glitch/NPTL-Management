(() => {
  const state = {
    role: "Student",
    token: sessionStorage.getItem("nptel_token"),
    user: JSON.parse(sessionStorage.getItem("nptel_user") || "null"),
    deferredInstall: null,
    currentPage: "dashboard",
    cache: new Map()
  };

  let notificationWatcher = null;
  let notificationSnapshot = new Set();

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
      toggleStudentRegistration(false);
    });
  });

  const newStudentLink = $("newStudentLink");
  const studentRegisterPanel = $("studentRegisterPanel");
  const backToLoginBtn = $("backToLoginBtn");

  function toggleStudentRegistration(show) {
    const isStudent = state.role === "Student";
    if (newStudentLink) newStudentLink.hidden = !isStudent || show;
    if (studentRegisterPanel) studentRegisterPanel.hidden = !isStudent || !show;
    $("loginForm").hidden = show;
    if (!show) {
      $("registerMessage").textContent = "";
      return;
    }
    $("regName")?.focus();
  }

  newStudentLink?.addEventListener("click", () => toggleStudentRegistration(true));
  backToLoginBtn?.addEventListener("click", () => toggleStudentRegistration(false));

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
      if (state.role === "Student" && "Notification" in window && Notification.permission === "default") {
        try { await Notification.requestPermission(); } catch (_) {}
      }
      await openDashboard();
    } catch (err) {
      loginMessage.textContent = err.message || "Unable to sign in.";
    } finally {
      showLoading(false);
    }
  });

  $("createStudentAccountBtn")?.addEventListener("click", async () => {
    const message = $("registerMessage");
    const payload = {
      name: $("regName")?.value?.trim(),
      registerNumber: $("regNumber")?.value?.trim(),
      year: Number($("regYear")?.value || 1),
      semester: Number($("regSemester")?.value || 1),
      department: $("regDepartment")?.value?.trim() || "CSE",
      classSection: $("regSection")?.value?.trim() || "A",
      academicYear: $("regAcademicYear")?.value?.trim() || "2026-27",
      email: $("regEmail")?.value?.trim(),
      phone: $("regPhone")?.value?.trim(),
      password: $("regPassword")?.value || "",
      confirmPassword: $("regConfirmPassword")?.value || ""
    };

    if (!payload.name || !payload.registerNumber || !payload.email || !payload.phone) {
      message.textContent = "Please fill all required student details.";
      return;
    }
    if (payload.password.length < 8) {
      message.textContent = "Password must be at least 8 characters.";
      return;
    }
    if (payload.password !== payload.confirmPassword) {
      message.textContent = "Passwords do not match.";
      return;
    }

    message.textContent = "Creating student account…";
    try {
      const result = await request("/api/v1/auth/student/register", {
        method: "POST",
        body: payload,
        auth: false,
        timeoutMs: 15000
      });
      const data = result?.data;
      if (!data?.accessToken) throw new Error(result?.message || "Account creation failed.");

      state.token = data.accessToken;
      state.role = "Student";
      state.user = data;
      sessionStorage.setItem("nptel_token", state.token);
      sessionStorage.setItem("nptel_user", JSON.stringify(state.user));
      state.cache.clear();

      message.className = "form-message success";
      message.textContent = result?.message || "Account created successfully.";
      if ("Notification" in window && Notification.permission === "default") {
        try { await Notification.requestPermission(); } catch (_) {}
      }
      await openDashboard(true);
    } catch (err) {
      message.className = "form-message";
      message.textContent = err.message || "Account creation failed.";
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
    stopNotificationWatcher();
    showLogin();
  });

  $("refreshBtn").addEventListener("click", () => loadPage(state.currentPage, true));

  async function request(path, opts = {}) {
    const isFormData = opts.body instanceof FormData;
    const headers = { ...(isFormData ? {} : { "Content-Type": "application/json" }), ...(opts.headers || {}) };
    if (opts.auth !== false && state.token) headers.Authorization = "Bearer " + state.token;

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), opts.timeoutMs || 12000);

    let response;
    try {
      response = await fetch(baseUrl + path, {
        method: opts.method || "GET",
        headers,
        body: opts.body && !isFormData ? JSON.stringify(opts.body) : opts.body,
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
    if (state.role === "Student") startNotificationWatcher();
    else stopNotificationWatcher();
  }

  function stopNotificationWatcher() {
    if (notificationWatcher) {
      clearInterval(notificationWatcher);
      notificationWatcher = null;
    }
    notificationSnapshot = new Set();
  }

  async function startNotificationWatcher() {
    stopNotificationWatcher();
    if (!state.token || state.role !== "Student") return;

    try {
      const initial = await request("/api/v1/student/notifications");
      notificationSnapshot = new Set((initial?.data || []).map(n => n.notificationId).filter(Boolean));
    } catch (_) {}

    notificationWatcher = setInterval(async () => {
      if (!state.token || state.role !== "Student") return;
      try {
        const result = await request("/api/v1/student/notifications", { timeoutMs: 8000 });
        const items = Array.isArray(result?.data) ? result.data : [];
        for (const item of items) {
          if (!item.notificationId || notificationSnapshot.has(item.notificationId)) continue;
          notificationSnapshot.add(item.notificationId);
          if ("Notification" in window && Notification.permission === "granted") {
            try {
              new Notification(item.title || "NPTEL Notification", {
                body: item.message || "You have a new NPTEL notification."
              });
            } catch (_) {}
          }
        }
        if (state.currentPage === "notifications") {
          state.cache.delete("/api/v1/student/notifications");
        }
      } catch (_) {}
    }, 60000);
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
      bindCertificateAccessButtons();
      bindStudentUploadButton(registrationId);
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
      bindCertificateAccessButtons();
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
    $("primaryContent").innerHTML =
      '<div class="report-toolbar">' +
      '<button class="primary-btn small" id="downloadStaffXlsx">Download Excel</button>' +
      '<button class="ghost-btn small" id="downloadStaffCsv">Download CSV</button>' +
      '</div>' +
      tableCards(d?.items || [], "report");
    $("profileContent").innerHTML = profileRows(d, ["department","year","classSection","totalRecords","generatedAt"]);
    bindStaffExportButtons();
  }

  async function loadAdminStudents(force) {
    const result = await cached("/api/v1/admin/students?page=1&pageSize=50", force);
    setPageTitle("Students", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Student Management", "Create a new student account with academic and contact details.");
    const items = result?.data?.items || [];
    $("primaryHeading").textContent = "Students";
    $("primaryContent").innerHTML =
      adminStudentCreateForm() +
      tableCards(items, "admin-student");
    $("profileContent").innerHTML = '<div class="empty">New account login ID is the Register Number. Default temporary password is Student@Nptel2026.</div>';
    bindAdminStudentCreate();
    bindDetailButtons();
  }

  async function loadAdminStaff(force) {
    const result = await cached("/api/v1/admin/staff?page=1&pageSize=50", force);
    setPageTitle("Staff", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Staff Management", "Create staff accounts and assign them to a Year/Class scope.");
    $("primaryHeading").textContent = "Staff Members";
    $("primaryContent").innerHTML =
      adminStaffCreateForm() +
      tableCards(result?.data?.items || [], "admin-staff");
    $("profileContent").innerHTML = '<div class="empty">New staff login ID is the Staff ID. Default temporary password is Staff@Nptel2026.</div>';
    bindAdminStaffCreate();
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
    $("profileContent").innerHTML = '<div class="empty">Use Set Exam on a registration to schedule an exam and start automatic result reminders.</div>';
    bindAdminExamRegistrationButtons();
  }

  async function loadAdminExams(force) {
    const result = await cached("/api/v1/admin/exams?page=1&pageSize=50", force);
    setPageTitle("Exams", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Exam Management", "Set the exam date. After the exam, an automatic result-pending notification is sent every 3 days until the score/result is entered.");
    $("primaryHeading").textContent = "Exam Records";
    $("primaryContent").innerHTML = tableCards(result?.data?.items || [], "admin-exam");
    $("profileContent").innerHTML = '<div class="empty">Result reminder rule: first reminder 3 days after the exam, then every 3 days until Score or Pass Status is recorded.</div>';
    bindAdminExamButtons();
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
        meta = [item.registerNumber, item.department, item.classSection, item.year ? "Year " + item.year : null, item.semester ? "Sem " + item.semester : null, item.academicYear].filter(Boolean);
      } else if (type === "staff") {
        title = item.staffName || "Staff";
        meta = [item.staffIdentifier, item.department, item.assignedYear ? "Year " + item.assignedYear : null, item.assignedClass ? "Class " + item.assignedClass : null].filter(Boolean);
      } else if (type === "staff-course") {
        title = item.courseName || "Course";
        meta = [item.courseCode, item.registrationStatus, item.currentTimelineStatus, item.exam?.examStatus, item.certificate?.verifiedStatus].filter(Boolean);
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
      if (type === "admin-registration" && item.registrationId) {
        action = '<button class="ghost-btn small" data-exam-reg="' + item.registrationId + '">Set Exam</button>';
      }
      if (type === "admin-exam" && item.registrationId) {
        action = '<button class="ghost-btn small" data-exam-edit="' + item.registrationId + '">Edit Exam</button>';
      }
      if (type === "staff-course" && item.certificate?.certificateId && item.certificate?.storagePath) {
        action = '<button class="ghost-btn small" data-cert="' + item.certificate.certificateId + '">Download Certificate</button>';
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
    const certificate = d?.certificate || {};
    const certStatus = certificate.verifiedStatus || "Pending";
    const reminderEnabled = !!certificate.reminderEnabled;
    const defaultReminderDate = certificate.reminderDate
      ? new Date(certificate.reminderDate).toISOString().slice(0, 16)
      : "";

    const existingAccess =
      certificate.certificateId && certificate.storagePath
        ? '<button class="ghost-btn small" data-cert="' + certificate.certificateId + '">Open Submitted Certificate</button>'
        : "";

    let uploadBlock = "";
    if (allowStudent && certificate.certificateId && !["Verified", "Received"].includes(certStatus)) {
      uploadBlock =
        '<div class="certificate-box">' +
        '<div class="box-title">Submit NPTEL Certificate</div>' +
        '<div class="data-meta">Enter certificate details and upload the official PDF. Maximum 10 MB.</div>' +
        '<div class="form-grid">' +
        '<div><label>Certificate Number</label><input id="certificateNumber" class="form-input" value="' + escapeHtml(certificate.certificateNumber || "") + '" placeholder="e.g. NPTEL-2026-001"></div>' +
        '<div><label>Score</label><input id="certificateScore" class="form-input" type="number" min="0" max="100" step="0.01" value="' + escapeHtml(certificate.score ?? "") + '" placeholder="e.g. 78.5"></div>' +
        '<div><label>Pass Status</label><select id="certificatePassStatus" class="form-input">' +
        ['Pass','Elite','Elite + Silver','Elite + Gold','Successfully Completed',''].map(v => {
          const selected = (certificate.passStatus || "") === v ? " selected" : "";
          return '<option value="' + escapeHtml(v) + '"' + selected + '>' + escapeHtml(v || "Select status") + '</option>';
        }).join("") +
        '</select></div>' +
        '<div><label>Issued Date</label><input id="certificateIssuedDate" class="form-input" type="date" value="' + (certificate.issuedDate ? String(certificate.issuedDate).slice(0,10) : "") + '"></div>' +
        '</div>' +
        '<div class="upload-row">' +
        '<input id="certificateFile" class="form-input" type="file" accept=".pdf,application/pdf">' +
        '<button class="primary-btn small" id="uploadCertificateBtn" data-registration="' + escapeHtml(d.registrationId) + '">Upload Certificate</button>' +
        '</div>' +
        '<div id="uploadMessage" class="form-message"></div>' +
        '</div>';
    }

    const reminderBlock = allowStudent && certificate.certificateId
      ? '<div class="certificate-box reminder-box">' +
        '<div class="box-title">Certificate Reminder</div>' +
        '<div class="data-meta">Enable an in-app reminder so you do not forget to submit or update your certificate.</div>' +
        '<div class="reminder-row">' +
        '<label class="switch-line"><input id="certificateReminderEnabled" type="checkbox"' + (reminderEnabled ? " checked" : "") + '><span>Enable reminder</span></label>' +
        '<input id="certificateReminderDate" class="form-input reminder-date" type="datetime-local" value="' + escapeHtml(defaultReminderDate) + '">' +
        '<button class="ghost-btn small" id="saveCertificateReminder" data-registration="' + escapeHtml(d.registrationId) + '">Save Reminder</button>' +
        '</div>' +
        '<div id="reminderMessage" class="form-message"></div>' +
        '</div>' : "";

    return '<div class="profile-list">' +
      '<div class="profile-row"><span>Exam</span><span>' +
      escapeHtml([e.examStatus,e.hallTicketStatus,e.passStatus,e.score != null ? "Score " + e.score : null].filter(Boolean).join(" · ") || "Not available") +
      '</span></div>' +
      '<div class="profile-row"><span>Certificate Status</span><span>' + escapeHtml(certStatus) + '</span></div>' +
      '<div class="profile-row"><span>Certificate Number</span><span>' + escapeHtml(certificate.certificateNumber || "—") + '</span></div>' +
      '<div class="profile-row"><span>Certificate Score</span><span>' + escapeHtml(certificate.score ?? "—") + '</span></div>' +
      '<div class="profile-row"><span>Pass Status</span><span>' + escapeHtml(certificate.passStatus || "—") + '</span></div>' +
      '<div class="row-actions">' + existingAccess + '</div>' +
      uploadBlock + reminderBlock +
      '</div>';
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

  function bindStudentUploadButton(registrationId) {
    const btn = $("uploadCertificateBtn");
    if (btn) {
      btn.addEventListener("click", async () => {
        const fileInput = $("certificateFile");
        const message = $("uploadMessage");
        const file = fileInput?.files?.[0];
        if (!file) {
          message.textContent = "Please select a PDF certificate.";
          return;
        }

        const form = new FormData();
        form.append("file", file, file.name);
        form.append("certificateNumber", $("certificateNumber")?.value?.trim() || "");
        if ($("certificateScore")?.value) form.append("score", $("certificateScore").value);
        form.append("passStatus", $("certificatePassStatus")?.value || "");
        if ($("certificateIssuedDate")?.value) form.append("issuedDate", $("certificateIssuedDate").value);

        message.textContent = "Uploading certificate…";
        try {
          const result = await request("/api/v1/student/courses/" + registrationId + "/certificate/upload", {
            method: "POST",
            body: form,
            timeoutMs: 30000
          });
          message.className = "form-message success";
          message.textContent = result?.message || "Certificate uploaded successfully.";
          state.cache.delete("/api/v1/student/courses/" + registrationId);
          await openStudentCourse(registrationId);
        } catch (err) {
          message.className = "form-message";
          message.textContent = err.message || "Certificate upload failed.";
        }
      });
    }

    const reminderBtn = $("saveCertificateReminder");
    if (reminderBtn) {
      reminderBtn.addEventListener("click", async () => {
        const enabled = !!$("certificateReminderEnabled")?.checked;
        const dateValue = $("certificateReminderDate")?.value || "";
        const message = $("reminderMessage");
        message.textContent = "Saving reminder…";
        try {
          const result = await request("/api/v1/student/courses/" + registrationId + "/certificate/reminder", {
            method: "POST",
            body: {
              enabled,
              reminderDate: enabled && dateValue ? new Date(dateValue).toISOString() : null
            }
          });
          message.className = "form-message success";
          message.textContent = result?.message || "Reminder updated.";
          state.cache.delete("/api/v1/student/courses/" + registrationId);
          await openStudentCourse(registrationId);
        } catch (err) {
          message.className = "form-message";
          message.textContent = err.message || "Could not save reminder.";
        }
      });
    }
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
      $("profileContent").innerHTML = profileRows(d, ["studentId","name","registerNumber","department","classSection","year","semester","academicYear","email","phone","isActive"]);
      bindAdminExamRegistrationButtons();
    } catch (err) {
      showPageMessage(err.message);
    } finally {
      showLoading(false);
    }
  }

  function adminStudentCreateForm() {
    return '<div class="certificate-box admin-form-box">' +
      '<div class="box-title">Create New Student Account</div>' +
      '<div class="data-meta">Required account details. Register Number becomes the Student login ID.</div>' +
      '<div class="form-grid">' +
      '<div><label>Name</label><input id="newStudentName" class="form-input" placeholder="Student full name" required></div>' +
      '<div><label>Register Number</label><input id="newStudentReg" class="form-input" placeholder="e.g. 951021104006" required></div>' +
      '<div><label>Year</label><select id="newStudentYear" class="form-input"><option value="1">Year 1</option><option value="2">Year 2</option><option value="3" selected>Year 3</option><option value="4">Year 4</option></select></div>' +
      '<div><label>Semester</label><select id="newStudentSemester" class="form-input">' + Array.from({length:8},(_,i)=>'<option value="' + (i+1) + '"' + (i===0?' selected':'') + '>Semester ' + (i+1) + '</option>').join("") + '</select></div>' +
      '<div><label>Department</label><input id="newStudentDept" class="form-input" value="CSE" placeholder="e.g. CSE"></div>' +
      '<div><label>Section</label><input id="newStudentSection" class="form-input" value="A" placeholder="e.g. A"></div>' +
      '<div><label>Academic Year</label><input id="newStudentAcademicYear" class="form-input" value="2026-27" placeholder="e.g. 2026-27"></div>' +
      '<div><label>Email</label><input id="newStudentEmail" class="form-input" type="email" placeholder="student@college.edu"></div>' +
      '<div><label>Phone</label><input id="newStudentPhone" class="form-input" type="tel" placeholder="10-digit mobile number"></div>' +
      '</div>' +
      '<div class="row-actions"><button class="primary-btn small" id="createStudentBtn">Create Student Account</button></div>' +
      '<div id="createStudentMessage" class="form-message"></div>' +
      '</div>';
  }

  function adminStaffCreateForm() {
    return '<div class="certificate-box admin-form-box">' +
      '<div class="box-title">Create New Staff Account</div>' +
      '<div class="data-meta">Staff ID becomes the login ID. Assign the Year/Class scope for read-only student access.</div>' +
      '<div class="form-grid">' +
      '<div><label>Staff Name</label><input id="newStaffName" class="form-input" placeholder="Faculty name" required></div>' +
      '<div><label>Staff ID</label><input id="newStaffId" class="form-input" placeholder="e.g. CSE-STF-105" required></div>' +
      '<div><label>Department</label><input id="newStaffDept" class="form-input" value="CSE" placeholder="e.g. CSE"></div>' +
      '<div><label>Assigned Year</label><select id="newStaffYear" class="form-input"><option value="1">Year 1</option><option value="2">Year 2</option><option value="3" selected>Year 3</option><option value="4">Year 4</option></select></div>' +
      '<div><label>Assigned Section</label><input id="newStaffSection" class="form-input" value="A" placeholder="e.g. A"></div>' +
      '<div><label>Email</label><input id="newStaffEmail" class="form-input" type="email" placeholder="faculty@college.edu"></div>' +
      '</div>' +
      '<div class="row-actions"><button class="primary-btn small" id="createStaffBtn">Create Staff Account</button></div>' +
      '<div id="createStaffMessage" class="form-message"></div>' +
      '</div>';
  }

  function bindAdminStudentCreate() {
    const btn = $("createStudentBtn");
    if (!btn) return;
    btn.addEventListener("click", async () => {
      const message = $("createStudentMessage");
      const name = $("newStudentName")?.value?.trim();
      const registerNumber = $("newStudentReg")?.value?.trim();
      if (!name || !registerNumber) {
        message.textContent = "Name and Register Number are required.";
        return;
      }
      message.textContent = "Creating student account…";
      try {
        const result = await request("/api/v1/admin/students", {
          method: "POST",
          body: {
            name,
            registerNumber,
            year: Number($("newStudentYear")?.value || 1),
            semester: Number($("newStudentSemester")?.value || 1),
            department: $("newStudentDept")?.value?.trim() || "CSE",
            classSection: $("newStudentSection")?.value?.trim() || "A",
            academicYear: $("newStudentAcademicYear")?.value?.trim() || "2026-27",
            email: $("newStudentEmail")?.value?.trim() || null,
            phone: $("newStudentPhone")?.value?.trim() || null
          }
        });
        message.className = "form-message success";
        message.textContent = "Student account created. Login: " + registerNumber + " | Temporary password: Student@Nptel2026";
        state.cache.delete("/api/v1/admin/students?page=1&pageSize=50");
        await loadAdminStudents(true);
        $("welcomeBanner").innerHTML = banner("Student account created", "Login ID: " + registerNumber + " · Temporary password: Student@Nptel2026");
      } catch (err) {
        message.className = "form-message";
        message.textContent = err.message || "Student creation failed.";
      }
    });
  }

  function bindAdminStaffCreate() {
    const btn = $("createStaffBtn");
    if (!btn) return;
    btn.addEventListener("click", async () => {
      const message = $("createStaffMessage");
      const staffName = $("newStaffName")?.value?.trim();
      const staffIdentifier = $("newStaffId")?.value?.trim();
      if (!staffName || !staffIdentifier) {
        message.textContent = "Staff Name and Staff ID are required.";
        return;
      }
      message.textContent = "Creating staff account…";
      try {
        await request("/api/v1/admin/staff", {
          method: "POST",
          body: {
            staffName,
            staffIdentifier,
            department: $("newStaffDept")?.value?.trim() || "CSE",
            assignedYear: Number($("newStaffYear")?.value || 1),
            assignedClass: $("newStaffSection")?.value?.trim() || "A",
            email: $("newStaffEmail")?.value?.trim() || null
          }
        });
        message.className = "form-message success";
        message.textContent = "Staff account created. Login: " + staffIdentifier + " | Temporary password: Staff@Nptel2026";
        state.cache.delete("/api/v1/admin/staff?page=1&pageSize=50");
        await loadAdminStaff(true);
        $("welcomeBanner").innerHTML = banner("Staff account created", "Login ID: " + staffIdentifier + " · Temporary password: Staff@Nptel2026");
      } catch (err) {
        message.className = "form-message";
        message.textContent = err.message || "Staff creation failed.";
      }
    });
  }

  async function openAdminExamEditor(registrationId) {
    showLoading(true);
    let exam = {
      registrationId,
      examApplicationStatus: "NotStarted",
      examApplicationDate: "",
      examApplicationDeadline: "",
      examDate: "",
      hallTicketStatus: "",
      examStatus: "NotStarted",
      score: "",
      passStatus: ""
    };
    try {
      const result = await request("/api/v1/admin/exams/registration/" + registrationId);
      exam = result?.data || exam;
    } catch (_) {
      // No exam row yet: UpdateExamAsync will create it.
    }

    setPageTitle("Set Exam", "Admin portal");
    $("statsGrid").innerHTML = "";
    $("welcomeBanner").innerHTML = banner("Exam Schedule & Result Automation", "Save the Exam Date. The system will notify the student 3 days after the exam, then every 3 days until the result is entered.");
    $("primaryHeading").textContent = "Exam Details";
    $("primaryContent").innerHTML =
      '<div class="certificate-box admin-form-box">' +
      '<div class="box-title">Exam / Result Settings</div>' +
      '<div class="form-grid">' +
      '<div><label>Application Status</label><select id="examApplicationStatus" class="form-input"><option>NotStarted</option><option>Pending</option><option>Applied</option><option>Closed</option></select></div>' +
      '<div><label>Application Date</label><input id="examApplicationDate" class="form-input" type="datetime-local"></div>' +
      '<div><label>Application Deadline</label><input id="examApplicationDeadline" class="form-input" type="datetime-local"></div>' +
      '<div><label>Exam Date</label><input id="examDate" class="form-input" type="datetime-local"></div>' +
      '<div><label>Hall Ticket Status</label><input id="hallTicketStatus" class="form-input" placeholder="Available / Downloaded"></div>' +
      '<div><label>Exam Status</label><select id="examStatus" class="form-input"><option>NotStarted</option><option>Scheduled</option><option>Completed</option></select></div>' +
      '<div><label>Score</label><input id="examScore" class="form-input" type="number" min="0" max="100" step="0.01"></div>' +
      '<div><label>Pass Status</label><select id="examPassStatus" class="form-input"><option value="">Pending</option><option>Pass</option><option>Fail</option></select></div>' +
      '</div>' +
      '<div class="row-actions"><button class="primary-btn small" id="saveAdminExam">Save Exam Details</button></div>' +
      '<div id="adminExamMessage" class="form-message"></div>' +
      '</div>';

    const setDate = (id, value) => { if (value) $(id).value = new Date(value).toISOString().slice(0,16); };
    $("examApplicationStatus").value = exam.examApplicationStatus || "NotStarted";
    setDate("examApplicationDate", exam.examApplicationDate);
    setDate("examApplicationDeadline", exam.examApplicationDeadline);
    setDate("examDate", exam.examDate);
    $("hallTicketStatus").value = exam.hallTicketStatus || "";
    $("examStatus").value = exam.examStatus || "NotStarted";
    $("examScore").value = exam.score ?? "";
    $("examPassStatus").value = exam.passStatus || "";

    $("saveAdminExam").addEventListener("click", async () => {
      const message = $("adminExamMessage");
      const toIso = id => $(id)?.value ? new Date($(id).value).toISOString() : null;
      message.textContent = "Saving exam details…";
      try {
        const result = await request("/api/v1/admin/exams/registration/" + registrationId, {
          method: "PUT",
          body: {
            examApplicationStatus: $("examApplicationStatus").value,
            examApplicationDate: toIso("examApplicationDate"),
            examApplicationDeadline: toIso("examApplicationDeadline"),
            examDate: toIso("examDate"),
            hallTicketStatus: $("hallTicketStatus").value.trim() || null,
            examStatus: $("examStatus").value,
            score: $("examScore").value ? Number($("examScore").value) : null,
            passStatus: $("examPassStatus").value || null
          }
        });
        message.className = "form-message success";
        message.textContent = result?.message || "Exam details saved. Automatic reminders are active.";
        state.cache.delete("/api/v1/admin/exams?page=1&pageSize=50");
        await loadAdminExams(true);
      } catch (err) {
        message.className = "form-message";
        message.textContent = err.message || "Could not save exam details.";
      }
    });

    $("profileContent").innerHTML = profileRows(exam, ["studentName","registerNumber","courseName","examDate","examStatus","score","passStatus"]);
    showLoading(false);
  }

  function bindAdminExamButtons() {
    document.querySelectorAll("[data-exam-edit]").forEach(btn => {
      btn.addEventListener("click", () => openAdminExamEditor(btn.dataset.examEdit));
    });
  }

  function bindAdminExamRegistrationButtons() {
    document.querySelectorAll("[data-exam-reg]").forEach(btn => {
      btn.addEventListener("click", () => openAdminExamEditor(btn.dataset.examReg));
    });
  }

  function bindStaffExportButtons() {
    const download = async (format) => {
      try {
        const url = "/api/v1/staff/reports/export-" + format + "?reportType=student-registration";
        const headers = state.token ? { Authorization: "Bearer " + state.token } : {};
        const response = await fetch(baseUrl + url, { headers });
        if (!response.ok) {
          const text = await response.text();
          throw new Error(text || "Download failed (" + response.status + ")");
        }
        const blob = await response.blob();
        const disposition = response.headers.get("Content-Disposition") || "";
        const match = disposition.match(/filename="?([^"]+)"?/i);
        const filename = match ? match[1] : ("NPTEL_Staff_Details." + (format === "xlsx" ? "xlsx" : "csv"));
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        URL.revokeObjectURL(link.href);
        link.remove();
      } catch (err) {
        alert(err.message || "Download failed.");
      }
    };
    const xlsx = $("downloadStaffXlsx");
    const csv = $("downloadStaffCsv");
    if (xlsx) xlsx.addEventListener("click", () => download("xlsx"));
    if (csv) csv.addEventListener("click", () => download("csv"));
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
    toggleStudentRegistration(false);
    $("newStudentLink").hidden = state.role !== "Student";
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