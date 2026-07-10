/* EINVWORLD shared UI helpers for the Tabler layout.
 * Vanilla JS, no jQuery dependency, self-hosted. Loaded by _LayoutTabler only.
 * Responsibilities:
 *   1. Current-route highlighting — mark the nav link matching the current path as active
 *      (accessible: aria-current="page") and open its parent dropdown so the user sees where
 *      they are. Robust to trailing slashes and case.
 *   2. window.einvworld.toast(message) — reuse the existing #toast-success Bootstrap toast.
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
    var links = document.querySelectorAll('.navbar-vertical a.nav-link[href], .navbar-vertical a.dropdown-item[href]');
    var best = null;
    var bestLen = -1;

    links.forEach(function (a) {
      var href = a.getAttribute("href");
      if (!href || href.charAt(0) === "#") return;
      var linkPath = normalizePath(href);
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

  window.einvworld = window.einvworld || {};
  window.einvworld.toast = makeToast;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", highlightCurrentRoute);
  } else {
    highlightCurrentRoute();
  }
})();
