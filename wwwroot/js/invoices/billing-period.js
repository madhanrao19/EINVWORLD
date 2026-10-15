/* Billing period (Create Invoice / Invoice Edit), decided 2026-10-14:
 * - Start and End date are hidden while Billing frequency is "Not_Applicable" (or not chosen), unless the
 *   draft/template/copy already carries dates — then they stay visible so nothing is silently dropped.
 * - With a frequency chosen, both dates are required and End must not be before Start (inline error,
 *   checked when leaving step 1 and before Submit to LHDN).
 * - Switching back to Not_Applicable clears both dates (the mapper sends an invoice period whenever any
 *   of the three is set).
 * Display/validation only: field names, posted value formats and the calculation code are unchanged.
 * Known limit: this is a browser-side check of the fields on screen. Submit to LHDN sends the saved draft,
 * and other routes (Invoice Lists / bulk submit, CSV import) don't apply this rule; LHDN itself accepts an
 * invoice period without dates, so it's a data-quality rule, not a compliance gate.
 */
(function () {
  "use strict";

  function init() {
    var freq = document.querySelector('select[name="Invoice.InvoicePeriod"]');
    var start = document.getElementById("startDate");
    var end = document.getElementById("endDate");
    if (!freq || !start || !end) return;

    var cols = [start, end].map(function (el) { return el.closest(".col-md-4") || el.parentNode; });
    var error = document.createElement("div");
    error.className = "invalid-feedback d-block einv-period-error";
    error.id = "billingPeriodError";
    error.setAttribute("role", "alert");
    error.hidden = true;
    cols[1].appendChild(error);

    function notApplicable() { return !freq.value || freq.value === "Not_Applicable"; }
    function picker(el) { return el._flatpickr; }
    function dateOf(el) { var fp = picker(el); return fp && fp.selectedDates[0] ? fp.selectedDates[0] : null; }
    function display(el) { var fp = picker(el); return fp && fp.altInput ? fp.altInput : el; }
    function clear(el) { var fp = picker(el); if (fp) fp.clear(); else el.value = ""; }

    function showError(message) {
      error.textContent = message || "";
      error.hidden = !message;
      [start, end].forEach(function (el) {
        var field = display(el), bad = !!message && (!el.value || el === end);
        field.classList.toggle("is-invalid", bad);
        if (bad) { field.setAttribute("aria-invalid", "true"); field.setAttribute("aria-describedby", error.id); }
        else { field.removeAttribute("aria-invalid"); field.removeAttribute("aria-describedby"); }
      });
    }

    function update(fromUser) {
      var hasDates = !!(start.value || end.value);
      if (notApplicable() && fromUser && hasDates) { clear(start); clear(end); hasDates = false; }
      var visible = !notApplicable() || hasDates;
      cols.forEach(function (c) { c.hidden = !visible; });
      if (!visible) showError("");
    }

    // Returns true when the period is fine to continue with; otherwise shows why.
    function check() {
      if (notApplicable()) { showError(""); return true; }
      var s = dateOf(start), e = dateOf(end);
      if (!s || !e) { showError("Choose both a start and an end date for this billing frequency."); return false; }
      if (e < s) { showError("The end date can't be before the start date."); return false; }
      showError("");
      return true;
    }

    if (window.jQuery) window.jQuery(freq).on("change", function () { update(true); }); // Select2 fires jQuery events
    else freq.addEventListener("change", function () { update(true); });
    [start, end].forEach(function (el) { el.addEventListener("change", function () { if (!error.hidden) check(); }); });
    update(false);

    var step1 = document.getElementById("step1");
    var originalNext = window.nextStep;
    if (typeof originalNext === "function") {
      window.nextStep = function () {
        if (step1 && !step1.classList.contains("d-none") && !check()) {
          display(start.value ? end : start).focus();
          return;
        }
        return originalNext.apply(this, arguments);
      };
    }

    // Submit to LHDN sends the saved draft from a click handler (AJAX), so guard the click itself,
    // ahead of that handler (window capture runs first). Drafts may still be saved incomplete.
    window.addEventListener("click", function (e) {
      var btn = e.target && e.target.closest && e.target.closest("#sa-success-submit-lhdn");
      if (btn && !check()) {
        e.preventDefault();
        e.stopImmediatePropagation();
        if (window.Swal) window.Swal.fire({ icon: "warning", title: "Check the billing period", text: error.textContent });
      }
    }, true);
  }

  // After the page's own setup (Select2, the shared calendar's setTimeout 0, create-invoice.js).
  function later() { setTimeout(init, 50); }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", later);
  else later();
})();
