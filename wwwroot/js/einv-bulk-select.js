// Shared row selection + bulk-action bar for list tables (same floating .einv-bulk-bar as the invoice list).
//
// Markup (per page):
//   <form method="post" asp-page-handler="Bulk" id="bulkForm" class="einv-bulk-bar d-none" data-einv-bulk>
//     <span class="einv-bulk-count"><span data-einv-bulk-count>0</span> selected</span>
//     <button name="bulkAction" value="delete" class="btn btn-danger btn-sm" data-confirm="Delete the selected rows?">Delete</button>
//     <button type="button" class="btn btn-link btn-sm" data-einv-bulk-clear>Clear</button>
//   </form>
//   header: <input type="checkbox" class="form-check-input einv-bulk-all" data-einv-bulk-form="bulkForm">
//   row:    <input type="checkbox" class="form-check-input einv-bulk-row" form="bulkForm" name="ids" value="@id">
//
// Row checkboxes join the bulk form through the HTML `form` attribute, so they can sit inside tables that
// already contain per-row <form>s. The server handler re-checks ownership of every id; this is only UX.
(function () {
    "use strict";

    function rowsFor(formId) {
        return Array.prototype.slice.call(document.querySelectorAll('.einv-bulk-row[form="' + formId + '"]'));
    }

    function ask(message) {
        if (window.Swal && typeof window.Swal.fire === "function") {
            return window.Swal.fire({
                icon: "warning",
                title: "Are you sure?",
                text: message,
                showCancelButton: true,
                confirmButtonText: "Yes, continue",
                cancelButtonText: "Cancel"
            }).then(function (r) { return !!r.isConfirmed; });
        }
        return Promise.resolve(window.confirm(message));
    }

    function init(form) {
        var id = form.id;
        var countEl = form.querySelector("[data-einv-bulk-count]");
        var allBoxes = Array.prototype.slice.call(document.querySelectorAll('.einv-bulk-all[data-einv-bulk-form="' + id + '"]'));

        function refresh() {
            var rows = rowsFor(id);
            var checked = rows.filter(function (cb) { return cb.checked; }).length;
            if (countEl) countEl.textContent = checked;
            form.classList.toggle("d-none", checked === 0);
            allBoxes.forEach(function (all) {
                all.checked = rows.length > 0 && checked === rows.length;
                all.indeterminate = checked > 0 && checked < rows.length;
            });
        }

        document.addEventListener("change", function (e) {
            var t = e.target;
            if (!t || !t.classList) return;
            if (t.classList.contains("einv-bulk-all") && t.getAttribute("data-einv-bulk-form") === id) {
                // Only rows currently shown (client-side filters hide <tr>s) are toggled.
                rowsFor(id).forEach(function (cb) { if (!cb.disabled && cb.offsetParent !== null) cb.checked = t.checked; });
                refresh();
            } else if (t.classList.contains("einv-bulk-row") && t.getAttribute("form") === id) {
                refresh();
            }
        });

        var clear = form.querySelector("[data-einv-bulk-clear]");
        if (clear) clear.addEventListener("click", function () {
            rowsFor(id).forEach(function (cb) { cb.checked = false; });
            refresh();
        });

        form.addEventListener("submit", function (e) {
            var btn = e.submitter;
            var message = btn && btn.getAttribute("data-confirm");
            if (!message || form.dataset.confirmed === "1") {
                delete form.dataset.confirmed;
                return;
            }
            e.preventDefault();
            ask(message.replace("{count}", String(rowsFor(id).filter(function (cb) { return cb.checked; }).length))).then(function (ok) {
                if (!ok) return;
                form.dataset.confirmed = "1";
                form.requestSubmit(btn);
            });
        });

        refresh();
    }

    function start() {
        document.querySelectorAll("form[data-einv-bulk][id]").forEach(init);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();
