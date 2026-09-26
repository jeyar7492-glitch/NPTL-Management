(() => {
  const state = {
    role: "Student",
    token: sessionStorage.getItem("nptel_token"),
    user: JSON.parse(sessionStorage.getItem("nptel_user") || "null"),
    deferredInstall: null
  };

  const el = id => document.getElementById(id);
  const loginView = el("loginView");
  const dashboardView = el("dashboardView");
  const loadingView = el("loadingView");
  const loginMessage = el("loginMessage");
  const roleTabs = [...document.querySelectorAll(".role-tab")];

  const baseUrl = window.NPTEL_API_URL || "";

  window.addEventListener("beforeinstallprompt", e => {
    e.preventDefault();
    state.deferredInstall = e;
    el("installBtn").hidden = false;
  });
  el("installBtn").addEventListener("click", async () => {
    if (!state.deferredInstall) return;
    state.deferredInstall.prompt();
    await state.deferredInstall.userChoice;
    state.deferredInstall = null;
    el("installBtn").hidden = true;
  });

  roleTabs.forEach(tab => {
    tab.addEventListener("click", () => {
      roleTabs.forEach(t => t.classList.remove("active"));
      tab.classList.add("active");
      state.role = tab.dataset.role;
      el("identifierLabel").textContent =
        state.role === "Student" ? "Register Number" :
        state.role === "Staff" ? "Staff ID" : "Admin ID";
      el("identifier").placeholder =
        state.role === "Student" ? "e.g. 24CSE001" :
        state.role === "Staff" ? "e.g. CSE-STF-101" : "e.g. ADM-001";
      loginMessage.textContent = "";
    });
  });

  el("loginForm").addEventListener("submit", async e => {
    e.preventDefault();
    loginMessage.className = "form-message";
    loginMessage.textContent = "";
    showLoading(true);

    const identifier = el("identifier").value.trim();
    const password = el("password").value;

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
      await openDashboard();
    } catch (err) {
      loginMessage.textContent = err.message || "Unable to sign in.";
    } finally {
      showLoading(false);
    }
  });

  el("logoutBtn").addEventListener("click", async () => {
    try {
      if (state.token) await request("/api/v1/auth/logout", { method: "POST" });
    } catch (_) {}
    sessionStorage.removeItem("nptel_token");
    sessionStorage.removeItem("nptel_user");
    state.token = null;
    state.user = null;
    showLogin();
  });

  el("refreshBtn").addEventListener("click", () => openDashboard());

  async function request(path, opts = {}) {
    const headers = { "Content-Type": "application/json", ...(opts.headers || {}) };
    if (opts.auth !== false && state.token) headers.Authorization = "Bearer " + state.token;
    const response = await fetch(baseUrl + path, {
      method: opts.method || "GET",
      headers,
      body: opts.body && !(opts.body instanceof FormData) ? JSON.stringify(opts.body) : opts.body
    });
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

  async function openDashboard() {
    if (!state.token) return showLogin();
    showLoading(true);
    try {
      let profile, summary, primary;
      if (state.role === "Student") {
        profile = await request("/api/v1/student/me");
        summary = await request("/api/v1/student/dashboard-summary");
        primary = await request("/api/v1/student/courses");
      } else if (state.role === "Staff") {
        profile = await request("/api/v1/staff/me");
        summary = await request("/api/v1/staff/dashboard-summary");
        primary = await request("/api/v1/staff/students?page=1&pageSize=20");
      } else {
        profile = await request("/api/v1/admin/me");
        summary = await request("/api/v1/admin/dashboard-metrics");
        primary = await request("/api/v1/admin/students?page=1&pageSize=20");
      }

      renderShell();
      renderWelcome();
      renderStats(summary?.data || {});
      renderProfile(profile?.data || {});
      renderPrimary(primary?.data || []);
    } catch (err) {
      if (!state.token) showLogin();
      else {
        renderShell();
        el("primaryContent").innerHTML = '<div class="empty">' + escapeHtml(err.message || "Unable to load dashboard.") + '</div>';
      }
    } finally {
      showLoading(false);
    }
  }

  function renderShell() {
    loginView.hidden = true;
    dashboardView.hidden = false;
    el("logoutBtn").hidden = false;
    el("roleBadge").textContent = state.role;
    el("pageEyebrow").textContent = state.role + " portal";
    el("pageTitle").textContent = state.user?.name || state.user?.identifier || "Dashboard";
    el("nav").innerHTML = [
      ["Dashboard", "active"],
      ["Courses", state.role === "Student" ? "" : "hidden"],
      ["Students", state.role === "Student" ? "hidden" : ""],
      ["Reports", state.role === "Admin" || state.role === "Staff" ? "" : "hidden"]
    ].map(([label, cls]) => cls === "hidden" ? "" : '<button class="nav-item ' + cls + '">' + label + '</button>').join("");
  }

  function renderWelcome() {
    const name = state.user?.name || state.user?.identifier || "User";
    el("welcomeBanner").innerHTML =
      '<div class="big">Welcome back, ' + escapeHtml(name) + ' 👋</div>' +
      '<div class="small">Role: ' + escapeHtml(state.role) + ' · Secure session active</div>';
  }

  function renderStats(payload) {
    const candidates = [
      ["Total", findValue(payload, ["total", "totalCount", "totalStudents", "totalCourses"])],
      ["Completed", findValue(payload, ["completed", "completedCourses", "completedCount"])],
      ["In Progress", findValue(payload, ["inProgress", "inProgressCourses", "activeCourses"])],
      ["Certificates", findValue(payload, ["certificates", "certificateCount", "verifiedCertificates"])]
    ];
    el("statsGrid").innerHTML = candidates.map(([label, value]) =>
      '<div class="stat"><div class="stat-label">' + label + '</div><div class="stat-value">' +
      escapeHtml(value == null ? "—" : String(value)) + '</div></div>').join("");
  }

  function renderProfile(profile) {
    const entries = Object.entries(profile || {})
      .filter(([_, v]) => v !== null && v !== undefined && typeof v !== "object")
      .slice(0, 10);
    el("profileContent").innerHTML = entries.length
      ? entries.map(([k, v]) => '<div class="profile-row"><span>' + prettyKey(k) + '</span><span>' + escapeHtml(String(v)) + '</span></div>').join("")
      : '<div class="empty">Profile information unavailable.</div>';
  }

  function renderPrimary(data) {
    const items = Array.isArray(data) ? data : (data?.items || []);
    el("primaryHeading").textContent =
      state.role === "Student" ? "My NPTEL Courses" :
      state.role === "Staff" ? "Scoped Students" : "Student Management";
    if (!items.length) {
      el("primaryContent").innerHTML = '<div class="empty">No records available.</div>';
      return;
    }
    el("primaryContent").innerHTML =
      '<div class="data-list">' +
      items.slice(0, 12).map(item => {
        const title = item.courseName || item.name || item.studentName || item.registerNumber || item.courseCode || "Record";
        const meta = [
          item.courseCode,
          item.status,
          item.classSection,
          item.department,
          item.year
        ].filter(v => v !== null && v !== undefined && v !== "").join(" · ");
        return '<div class="data-item"><div class="data-title">' + escapeHtml(String(title)) + '</div>' +
          '<div class="data-meta">' + escapeHtml(meta || "NPTEL Management record") + '</div></div>';
      }).join("") +
      '</div>';
  }

  function findValue(obj, keys) {
    for (const key of keys) {
      const hit = Object.keys(obj || {}).find(k => k.toLowerCase() === key.toLowerCase());
      if (hit != null && obj[hit] !== null && obj[hit] !== undefined) return obj[hit];
    }
    return null;
  }

  function prettyKey(key) {
    return key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, c => c.toUpperCase());
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, c => ({ "&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#039;" }[c]));
  }

  function showLogin() {
    loginView.hidden = false;
    dashboardView.hidden = true;
    el("logoutBtn").hidden = true;
    el("installBtn").hidden = !state.deferredInstall;
    el("roleBadge").textContent = "Guest";
    el("pageEyebrow").textContent = "Academic portal";
    el("pageTitle").textContent = "Welcome";
  }

  function showLoading(visible) {
    loadingView.hidden = !visible;
  }

  async function checkApi() {
    try {
      const res = await fetch(baseUrl + "/api/v1/health/live");
      el("apiStatus").textContent = res.ok ? "API: online" : "API: unavailable";
    } catch (_) {
      el("apiStatus").textContent = "API: unavailable";
    }
  }

  if ("serviceWorker" in navigator) {
    window.addEventListener("load", () => navigator.serviceWorker.register("/sw.js").catch(() => {}));
  }

  checkApi();
  if (state.token) {
    openDashboard();
  } else {
    showLogin();
  }
})();