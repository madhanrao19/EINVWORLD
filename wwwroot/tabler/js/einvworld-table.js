/* EINVWORLD shared data-table behaviour (Ledger component layer, v1.31.0). Opt-in per table:
 *
 *   <table data-einv-table="items">            → "Columns" menu: show/hide columns, remembered per
 *                                                 browser (localStorage "einv-cols:items").
 *   <table data-einv-table="..." data-einv-sort="client">
 *                                              → also click-to-sort on headers. Only for tables that
 *                                                 hold their whole list on the page; paged lists sort on
 *                                                 the server (their existing header links are kept).
 *
 * Header opt-outs: a <th> with no text or the text "Action(s)", or class col-action / col-checkbox, is never hideable or
 * sortable; data-col-lock keeps a column always visible; data-sort="none" disables sorting on it.
 * Cells may carry data-sort-value (e.g. an ISO date or raw number) when the shown text doesn't sort.
 * Hidden-by-default columns: data-col-default="hidden" on the <th>.
 * Rows whose cell count doesn't match the header (e.g. a colspan "no records" row) are left alone.
 * No dependencies; markup, ids and form fields are never changed — only cell visibility and row order.
 */
(function () {
  "use strict";

  var STORE_PREFIX = "einv-cols:";
  var uid = 0;

  function store(key, value) {
    try {
      if (value === undefined) return JSON.parse(localStorage.getItem(STORE_PREFIX + key) || "null");
      if (value === null) localStorage.removeItem(STORE_PREFIX + key);
      else localStorage.setItem(STORE_PREFIX + key, JSON.stringify(value));
    } catch (e) { return null; } // private mode / blocked storage: fall back to defaults
  }

  function headerCells(table) {
    var row = table.tHead && table.tHead.rows[table.tHead.rows.length - 1];
    return row ? Array.prototype.slice.call(row.cells) : [];
  }

  function label(th) {
    return (th.getAttribute("data-col-label") || th.textContent || "").replace(/\s+/g, " ").trim();
  }

  function isUtility(th) {
    return !label(th) || /^actions?$/i.test(label(th)) ||
      th.classList.contains("col-action") || th.classList.contains("col-checkbox");
  }

  function bodyRows(table, width) {
    var rows = [];
    Array.prototype.forEach.call(table.tBodies, function (tb) {
      Array.prototype.forEach.call(tb.rows, function (r) { if (r.cells.length === width) rows.push(r); });
    });
    return rows;
  }

  // ---- Customisable columns -------------------------------------------------------------------
  function initColumns(table, key, ths) {
    var cols = ths.map(function (th, i) {
      return { index: i, th: th, name: label(th), hideable: !isUtility(th) && !th.hasAttribute("data-col-lock"),
               defaultHidden: th.getAttribute("data-col-default") === "hidden" };
    }).filter(function (c) { return !isUtility(c.th); });
    if (cols.length < 2) return;

    function defaults() { return cols.filter(function (c) { return c.defaultHidden; }).map(function (c) { return c.name; }); }
    var hidden = store(key) || defaults();

    function apply() {
      var rows = bodyRows(table, ths.length);
      cols.forEach(function (c) {
        var hide = c.hideable && hidden.indexOf(c.name) !== -1;
        c.th.classList.toggle("einv-col-hidden", hide);
        rows.forEach(function (r) { r.cells[c.index].classList.toggle("einv-col-hidden", hide); });
      });
    }

    var menuId = "einv-cols-" + (++uid);
    var wrap = document.createElement("div");
    wrap.className = "einv-table-tools";
    wrap.innerHTML =
      '<div class="dropdown">' +
      '<button type="button" class="btn btn-sm btn-outline-secondary" data-bs-toggle="dropdown" data-bs-auto-close="outside" aria-expanded="false" aria-controls="' + menuId + '">' +
      '<i class="ri-layout-column-line" aria-hidden="true"></i> Columns</button>' +
      '<div class="dropdown-menu dropdown-menu-end einv-cols-menu" id="' + menuId + '">' +
      '<div class="einv-cols-title">Show columns</div><div class="einv-cols-list"></div>' +
      '<div class="einv-cols-foot"><button type="button" class="btn btn-sm btn-link px-0" data-einv-cols-reset>Reset to default</button></div>' +
      "</div></div>";
    var list = wrap.querySelector(".einv-cols-list");
    cols.forEach(function (c) {
      var id = menuId + "-" + c.index;
      var row = document.createElement("label");
      row.className = "dropdown-item einv-cols-item";
      row.setAttribute("for", id);
      var cb = document.createElement("input");
      cb.type = "checkbox";
      cb.className = "form-check-input m-0";
      cb.id = id;
      cb.disabled = !c.hideable;
      var text = document.createElement("span");
      text.textContent = c.name + (c.hideable ? "" : " (always shown)");
      row.appendChild(cb);
      row.appendChild(text);
      list.appendChild(row);
      cb.addEventListener("change", function () {
        hidden = hidden.filter(function (n) { return n !== c.name; });
        if (!cb.checked) hidden.push(c.name);
        store(key, hidden);
        apply();
      });
      c.checkbox = cb;
    });
    function sync() { cols.forEach(function (c) { c.checkbox.checked = !c.hideable || hidden.indexOf(c.name) === -1; }); }
    wrap.querySelector("[data-einv-cols-reset]").addEventListener("click", function () {
      hidden = defaults();
      store(key, null);
      sync();
      apply();
    });

    var host = document.querySelector('[data-einv-columns-for="' + key + '"]');
    if (host) host.appendChild(wrap);
    else {
      var anchor = table.closest(".table-responsive, .einv-table-scroll-x") || table;
      anchor.parentNode.insertBefore(wrap, anchor);
    }
    sync();
    apply();
  }

  // ---- Client-side sorting ---------------------------------------------------------------------
  function sortValue(cell) {
    var raw = cell.hasAttribute("data-sort-value") ? cell.getAttribute("data-sort-value") : cell.textContent;
    raw = (raw || "").replace(/\s+/g, " ").trim();
    var num = raw.replace(/^(RM|MYR)\s*/i, "").replace(/,/g, "");
    if (num !== "" && /^-?\d+(\.\d+)?%?$/.test(num)) return { n: parseFloat(num) };
    if (/^\d{4}-\d{2}-\d{2}/.test(raw)) { var t = Date.parse(raw.replace(" ", "T")); if (!isNaN(t)) return { n: t }; }
    return { s: raw.toLowerCase() };
  }

  function compare(a, b) {
    if (a.n !== undefined && b.n !== undefined) return a.n - b.n;
    if (a.n !== undefined) return -1; // numbers before text (e.g. "–" placeholders last)
    if (b.n !== undefined) return 1;
    return a.s.localeCompare(b.s, undefined, { numeric: true });
  }

  function initSort(table, ths) {
    ths.forEach(function (th, index) {
      if (isUtility(th) || th.getAttribute("data-sort") === "none" || th.querySelector("a[href]")) return;
      var btn = document.createElement("button");
      btn.type = "button";
      btn.className = "einv-sort";
      while (th.firstChild) btn.appendChild(th.firstChild);
      var icon = document.createElement("i");
      icon.className = "einv-sort-icon ri-arrow-up-down-line";
      icon.setAttribute("aria-hidden", "true");
      btn.appendChild(icon);
      th.appendChild(btn);
      th.setAttribute("aria-sort", "none");

      btn.addEventListener("click", function () {
        var dir = th.getAttribute("aria-sort") === "ascending" ? "descending" : "ascending";
        ths.forEach(function (o) { if (o.hasAttribute("aria-sort")) o.setAttribute("aria-sort", "none"); });
        th.setAttribute("aria-sort", dir);
        ths.forEach(function (o) {
          var ic = o.querySelector(".einv-sort-icon");
          if (ic) ic.className = "einv-sort-icon " + (o === th ? (dir === "ascending" ? "ri-arrow-up-line" : "ri-arrow-down-line") : "ri-arrow-up-down-line");
        });
        var sign = dir === "ascending" ? 1 : -1;
        Array.prototype.forEach.call(table.tBodies, function (tb) {
          var rows = Array.prototype.filter.call(tb.rows, function (r) { return r.cells.length === ths.length; });
          if (rows.length < 2) return;
          var anchor = rows[rows.length - 1].nextSibling; // keep e.g. a colspan footer row below
          rows.map(function (r, i) { return { r: r, v: sortValue(r.cells[index]), i: i }; })
            .sort(function (a, b) { return compare(a.v, b.v) * sign || a.i - b.i; })
            .forEach(function (x) { tb.insertBefore(x.r, anchor); });
        });
      });
    });
  }

  function init(root) {
    (root || document).querySelectorAll("table[data-einv-table]").forEach(function (table) {
      if (table.hasAttribute("data-einv-table-ready")) return;
      table.setAttribute("data-einv-table-ready", "");
      var ths = headerCells(table);
      if (!ths.length) return;
      if (table.getAttribute("data-einv-sort") === "client") initSort(table, ths);
      initColumns(table, table.getAttribute("data-einv-table"), ths);
    });
  }

  window.einvworld = window.einvworld || {};
  window.einvworld.initTables = init;
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { init(); });
  else init();
})();
