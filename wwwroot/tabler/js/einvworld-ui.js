/* EINVWORLD shared UI helpers for the Tabler layout.
 * Vanilla JS, no jQuery dependency, self-hosted. Loaded by _LayoutTabler only.
 * Responsibilities:
 *   1. Current-route highlighting — mark the nav link matching the current path as active
 *      (accessible: aria-current="page") and open its parent dropdown so the user sees where
 *      they are. Robust to trailing slashes and case.
 *   2. window.einvworld.toast(message) — reuse the existing #toast-success Bootstrap toast.
 *   3. Fixed-strategy positioning for row-action dropdowns inside `.einv-table-scroll-x` tables,
 *      so they aren't visually clipped by the table's horizontal-scroll container.
 */
(function () {
  "use strict";

  function normalizePath(p) {
    if (!p) return "/";
    try { p = new URL(p, window.location.origin).pathname; } catch (e) { /* relative already */ }
    p = p.toLowerCase().replace(/\/+$/, "");
    return p === "" ? "/" : p;
  }

  function highlightCurrentRoute() {
    var current = normalizePath(window.location.pathname);
    var currentSearch = window.location.search || "";
    var links = document.querySelectorAll('.navbar-vertical a.nav-link[href], .navbar-vertical a.dropdown-item[href]');
    var best = null;
    var bestLen = -1;
    var bestIsFullMatch = false;

    links.forEach(function (a) {
      var href = a.getAttribute("href");
      if (!href || href.charAt(0) === "#") return;
      var linkPath = normalizePath(href);
      var linkSearch = "";
      try { linkSearch = new URL(href, window.location.origin).search || ""; } catch (e) { /* relative already */ }

      if (linkSearch) {
        // A link with its own query string (e.g. "?type=SELF") only stands for that exact
        // page+query combo - two links can share a bare path (e.g. Credit/Debit/Refund Note and
        // View All e-Invoices both point at /Invoices/InvoiceLists), so a query-less link must
        // stay the fallback for everything except this link's own exact query.
        if (linkPath === current && linkSearch === currentSearch && !bestIsFullMatch) {
          best = a; bestLen = linkPath.length; bestIsFullMatch = true;
        }
        return;
      }

      if (bestIsFullMatch) return;
      // Exact match, or current path is a child of the link (e.g. /Items -> /Items/Edit).
      var isMatch = current === linkPath ||
        (linkPath !== "/" && current.indexOf(linkPath + "/") === 0);
      if (isMatch && linkPath.length > bestLen) { best = a; bestLen = linkPath.length; }
    });

    if (!best) return;

    best.classList.add("active");
    best.setAttribute("aria-current", "page");

    // If the match is a dropdown item, open + mark its parent nav-item.dropdown.
    var parentItem = best.closest(".nav-item.dropdown");
    if (parentItem) {
      var toggle = parentItem.querySelector(":scope > .nav-link.dropdown-toggle");
      var menu = parentItem.querySelector(":scope > .dropdown-menu");
      parentItem.classList.add("show");
      if (toggle) { toggle.classList.add("active"); toggle.setAttribute("aria-expanded", "true"); }
      if (menu) { menu.classList.add("show"); }
    }
  }

  function makeToast(message) {
    var el = document.getElementById("toast-success");
    var body = document.getElementById("toast-success-message");
    if (!el || !body || !window.bootstrap) return;
    body.textContent = message;
    window.bootstrap.Toast.getOrCreateInstance(el).show();
  }

  // Desktop sidebar collapse-to-icons. The collapsed class itself is applied synchronously by
  // an inline script in _LayoutTabler (avoids a flash of the expanded sidebar); this just wires
  // the toggle button and persists the choice. Mobile has its own Bootstrap collapse already.
  function initSidebarCollapse() {
    var STORAGE_KEY = "einv-sidebar-collapsed";
    var btn = document.getElementById("einv-sidebar-collapse-toggle");
    if (!btn) return;
    btn.setAttribute("aria-pressed", String(document.body.classList.contains("einv-sidebar-collapsed")));
    btn.addEventListener("click", function () {
      var collapsed = document.body.classList.toggle("einv-sidebar-collapsed");
      btn.setAttribute("aria-pressed", String(collapsed));
      localStorage.setItem(STORAGE_KEY, collapsed ? "1" : "0");
    });
  }

  // Row-action dropdowns (the "..." button) inside a `.einv-table-scroll-x` wrapper — used on
  // Buyer Directory, Items, Suppliers Audit/Security/Users, Assistant Processing History — sit in
  // a container that must keep `overflow-x: auto` for horizontal scrolling on desktop widths (see
  // the CSS comment on `.einv-table-scroll-x` in einvworld-tokens.css). Bootstrap's dropdown menu
  // is `position: absolute` by default, so it gets visually clipped/trapped inside that scrolling
  // ancestor instead of overlaying the page — it renders as an empty box near the toggle button
  // instead of showing its items. Explicitly initializing these dropdowns with Popper's `fixed`
  // strategy makes the menu position itself relative to the viewport, escaping the ancestor's
  // overflow clipping. Must run before Bootstrap's own data-api lazily creates a default-config
  // instance on first click, so this only helps dropdowns present at DOMContentLoaded time
  // (matches every current usage — none of these tables add rows without a full page reload).
  function initScrollableTableDropdowns() {
    if (!window.bootstrap || !window.bootstrap.Dropdown) return;
    document.querySelectorAll('.einv-table-scroll-x [data-bs-toggle="dropdown"]').forEach(function (el) {
      new window.bootstrap.Dropdown(el, { popperConfig: { strategy: "fixed" } });
    });
  }

  // Dark mode toggle. The initial data-bs-theme attribute is already set before this script runs
  // (server-side from the cookie for returning visitors, or a blocking inline script in <head> for
  // first-time visitors following the OS preference) — this only handles the click, persisting the
  // explicit choice as a cookie so the next server render already knows it (see _LayoutTabler).
  function initThemeToggle() {
    var COOKIE_NAME = "einv-theme";
    var btn = document.getElementById("einv-theme-toggle");
    if (!btn) return;

    function currentTheme() {
      return document.documentElement.getAttribute("data-bs-theme") === "dark" ? "dark" : "light";
    }
    function reflectState() {
      var isDark = currentTheme() === "dark";
      btn.setAttribute("aria-pressed", String(isDark));
      btn.setAttribute("title", isDark ? "Switch to light mode" : "Switch to dark mode");
    }

    reflectState();
    btn.addEventListener("click", function () {
      var next = currentTheme() === "dark" ? "light" : "dark";
      document.documentElement.setAttribute("data-bs-theme", next);
      // 1 year, site-wide, readable/writable by JS (no HttpOnly) since only this script uses it;
      // no Secure flag forced — the app is also served over plain HTTP behind the Cloudflare tunnel.
      document.cookie = COOKIE_NAME + "=" + next + "; Path=/; Max-Age=31536000; SameSite=Lax";
      reflectState();
    });
  }

  window.einvworld = window.einvworld || {};
  window.einvworld.toast = makeToast;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      highlightCurrentRoute();
      initSidebarCollapse();
      initThemeToggle();
      initScrollableTableDropdowns();
    });
  } else {
    highlightCurrentRoute();
    initSidebarCollapse();
    initThemeToggle();
    initScrollableTableDropdowns();
  }
})();
