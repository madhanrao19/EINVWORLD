/* EINVWORLD shared calendar (v1.35.0): one Flatpickr look and one date format everywhere.
 *
 * 1. Global Flatpickr defaults, so every page's own flatpickr() call shows the same calendar:
 *    the visible field reads "14 Oct 2026" (altInput) while the real input keeps posting the exact
 *    value format its page always used, and phones get the same calendar instead of the native one.
 * 2. Every <input type="date"> and <input type="month"> is upgraded to Flatpickr (value "Y-m-d" /
 *    "Y-m", the format Razor writes for those types and the server binds). Markup stays type="date",
 *    so without this script the browser's own picker still works.
 *    Opt out with data-einv-date="native". Content added later: window.einvworld.initDates(root).
 */
(function () {
  "use strict";
  if (typeof window.flatpickr !== "function") return;

  window.flatpickr.setDefaults({
    altInput: true,
    altFormat: "j M Y",
    disableMobile: true,
    monthSelectorType: "static"
  });

  // Flatpickr's visible input gets only "form-control input"; give it the field's own classes
  // (sizes, borders) plus a marker, and point the field's label at it. Done here rather than in a
  // default onReady hook, because a page passing its own onReady would replace a default one.
  function decorate(input) {
    var fp = input._flatpickr;
    if (!fp || !fp.altInput || fp.altInput.classList.contains("einv-date-display")) return;
    fp.altInput.className = input.className.replace(/flatpickr-input/g, "").trim();
    fp.altInput.classList.add("einv-date-display");
    // Optional fields need a way back to empty (the visible field is read-only text).
    if (!input.required) {
      var clear = document.createElement("button");
      clear.type = "button";
      clear.className = "einv-date-clear";
      clear.textContent = "Clear";
      clear.addEventListener("click", function () { fp.clear(); fp.close(); });
      fp.calendarContainer.appendChild(clear);
      fp.altInput.addEventListener("keydown", function (e) {
        if (e.key === "Backspace" || e.key === "Delete") { e.preventDefault(); fp.clear(); }
      });
    }
    if (!input.id) return;
    fp.altInput.id = input.id + "-display";
    document.querySelectorAll('label[for="' + CSS.escape(input.id) + '"]').forEach(function (l) { l.htmlFor = fp.altInput.id; });
  }

  function upgrade(input) {
    if (input._flatpickr || input.getAttribute("data-einv-date") === "native") return;
    var month = input.getAttribute("type") === "month"; // attribute: browsers without month support report .type "text"
    if (month && typeof window.monthSelectPlugin !== "function") return;
    var opts = month
      ? { plugins: [new window.monthSelectPlugin({ shorthand: true, dateFormat: "Y-m", altFormat: "M Y" })] }
      : { dateFormat: "Y-m-d" };
    // Inside a Bootstrap modal the focus trap would steal focus from a calendar attached to <body>.
    if (input.closest(".modal")) opts.static = true;
    if (input.min) opts.minDate = input.min;
    if (input.max) opts.maxDate = input.max;
    window.flatpickr(input, opts);
  }

  function init(root) {
    var scope = root || document;
    scope.querySelectorAll('input[type="date"], input[type="month"]').forEach(upgrade);
    scope.querySelectorAll("input.flatpickr-input").forEach(decorate);
  }

  window.einvworld = window.einvworld || {};
  window.einvworld.initDates = init;
  // After the pages' own DOMContentLoaded handlers, so a page that sets up its own picker wins.
  function later() { setTimeout(function () { init(); }, 0); }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", later);
  else later();
})();
