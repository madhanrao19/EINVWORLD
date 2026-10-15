/* EINVWORLD shared data-table behaviour (Ledger component layer; reorder + account saving v1.36.0).
 * Opt-in per table:
 *
 *   <table data-einv-table="items">            → "Columns" menu: show/hide columns and drag (or ↑/↓ on the handle)
 *                                                 to reorder them. Saved to the user's account
 *                                                 (api/table-preferences), so it follows them to any
 *                                                 browser; cached per user in localStorage for first paint.
 *   <table data-einv-table="..." data-einv-sort="client">
 *                                              → also click-to-sort on headers. Only for tables that
 *                                                 hold their whole list on the page; paged lists sort on
 *                                                 the server (their existing header links are kept).
 *
 * Header opt-outs: a <th> with no text or the text "Action(s)", or class col-action / col-checkbox, is a
 * utility column: never hidden, sorted or moved (it keeps its position); data-col-data on a <th> reading
 * "Action" makes it an ordinary data column instead. data-col-lock keeps a column
 * always visible (it can still move); data-sort="none" disables sorting on it.
 * Cells may carry data-sort-value (e.g. an ISO date or raw number) when the shown text doesn't sort.
 * Hidden-by-default columns: data-col-default="hidden" on the <th>. data-col-id gives a column a stable
 * saved name that survives header text changes (otherwise the header text is the name).
 * A page moving from an older column system can set table.einvLegacyColumns = async () => ({ hidden, order })
 * (column ids); it's read once, only when the account has nothing saved for that table yet.
 * Rows whose cell count doesn't match the header (e.g. a colspan "no records" row) are left alone.
 * Rows added later by page scripts get the same layout (MutationObserver).
 * No dependencies; ids, names and form fields are never changed — only cell visibility and order.
 */
(function () {
  "use strict";

  var API = "/api/table-preferences";
  var LEGACY_PREFIX = "einv-cols:"; // browser-only hidden list from v1.31–v1.35, moved to the account once
  var uid = 0;

  function meta(name) {
    var el = document.querySelector('meta[name="' + name + '"]');
    return el ? el.getAttribute("content") || "" : "";
  }
  var userHash = meta("einv-uid");
  var cachePrefix = "einv-cols2:" + userHash + ":";

  function local(key, value) {
    try {
      if (value === undefined) return JSON.parse(localStorage.getItem(key) || "null");
      if (value === null) localStorage.removeItem(key);
      else localStorage.setItem(key, JSON.stringify(value));
    } catch (e) { return null; } // private mode / blocked storage: fall back to defaults
    return null;
  }

  function headerCells(table) {
    var row = table.tHead && table.tHead.rows[table.tHead.rows.length - 1];
    return row ? Array.prototype.slice.call(row.cells) : [];
  }

  function label(th) {
    return (th.getAttribute("data-col-label") || th.textContent || "").replace(/\s+/g, " ").trim();
  }

  // data-col-data marks a real data column whose header happens to read "Action" (e.g. an activity log).
  function isUtility(th) {
    if (th.hasAttribute("data-col-data")) return false;
    return !label(th) || /^actions?$/i.test(label(th)) ||
      th.classList.contains("col-action") || th.classList.contains("col-checkbox");
  }

  // ---- Saving: one request at a time (two tables on a page must not overwrite each other) --------
  var queue = Promise.resolve();
  function saveToAccount(key, state, done) {
    if (!userHash) { done(false); return; }
    queue = queue.then(function () {
      return fetch(API + "/" + encodeURIComponent(key), {
        method: "PUT",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": meta("einv-csrf") },
        body: JSON.stringify({ hidden: state.hidden, order: state.order })
      }).then(function (r) { done(r.ok); }, function () { done(false); });
    });
  }

  function loadFromAccount(keys) {
    if (!userHash || !keys.length || !window.fetch) return Promise.resolve(null);
    var qs = keys.map(function (k) { return "keys=" + encodeURIComponent(k); }).join("&");
    return fetch(API + "?" + qs, { credentials: "same-origin", headers: { "Accept": "application/json" } })
      .then(function (r) {
        var type = r.headers.get("content-type") || "";
        return r.ok && type.indexOf("json") !== -1 ? r.json() : null; // e.g. a login redirect: keep the cache
      })
      .catch(function () { return null; });
  }

  // ---- One table ------------------------------------------------------------------------------
  function Table(table, key, ths) {
    var self = this;
    var seen = {};
    this.table = table;
    this.key = key;
    this.width = ths.length;
    this.cols = ths.map(function (th, i) {
      var name = label(th);
      seen[name] = (seen[name] || 0) + 1;
      var utility = isUtility(th);
      return {
        index: i, th: th, name: name,
        // data-col-id is stable across header renames; repeated labels stay distinct
        id: th.getAttribute("data-col-id") || (seen[name] > 1 ? name + "#" + seen[name] : name),
        movable: !utility,
        hideable: !utility && !th.hasAttribute("data-col-lock"),
        defaultHidden: th.getAttribute("data-col-default") === "hidden"
      };
    });
    this.movable = this.cols.filter(function (c) { return c.movable; });
    this.dirty = false;
    this.state = this.defaults();
    ths.forEach(function (th, i) { th.setAttribute("data-einv-col", i); });

    var cached = local(cachePrefix + key);
    var legacy = local(LEGACY_PREFIX + key);
    if (cached) this.state = this.clean(cached);
    else if (Array.isArray(legacy)) this.state = this.clean({ hidden: legacy, order: [] });

    if (this.movable.length >= 2) this.buildMenu();
    this.apply();
    var observer = new MutationObserver(function () { self.apply(); });
    observer.observe(table, { childList: true });
    Array.prototype.forEach.call(table.tBodies, function (tb) { observer.observe(tb, { childList: true }); });
  }

  Table.prototype.defaults = function () {
    return { hidden: this.cols.filter(function (c) { return c.defaultHidden; }).map(function (c) { return c.id; }), order: [] };
  };

  // Keeps every saved name, including ones this table doesn't show: tabs that share a key can differ
  // (Invoice Lists' Draft tab has no UUID column), and a save from one must not drop the others' choice.
  // Unknown names are simply ignored when laying out this table (a renamed header gets its default).
  Table.prototype.clean = function (s) {
    function keep(list) {
      return (Array.isArray(list) ? list : []).filter(function (n, i, a) { return typeof n === "string" && n && a.indexOf(n) === i; });
    }
    return { hidden: keep(s.hidden), order: keep(s.order) };
  };

  // Movable columns in the chosen order; columns the saved order doesn't mention keep their default order after it.
  Table.prototype.ordered = function () {
    var order = this.state.order;
    var known = order.map(function (id) { return this.movable.filter(function (c) { return c.id === id; })[0]; }, this)
      .filter(Boolean);
    return known.concat(this.movable.filter(function (c) { return known.indexOf(c) === -1; }));
  };

  // Original column index for each position: utility columns stay where they were.
  Table.prototype.layout = function () {
    var ordered = this.ordered(), next = 0;
    return this.cols.map(function (c) { return c.movable ? ordered[next++].index : c.index; });
  };

  Table.prototype.rows = function () {
    var rows = [this.cols[0].th.parentNode], width = this.width;
    Array.prototype.forEach.call(this.table.tBodies, function (tb) {
      Array.prototype.forEach.call(tb.rows, function (r) { if (r.cells.length === width) rows.push(r); });
    });
    return rows;
  };

  // Idempotent: a row already in the wanted layout is left untouched (so our own moves and sorts
  // don't feed back through the MutationObserver).
  Table.prototype.apply = function () {
    var layout = this.layout(), sig = layout.join(",") + "|" + this.state.hidden.join("\u0001");
    var hiddenIdx = this.cols.filter(function (c) { return c.hideable && this.state.hidden.indexOf(c.id) !== -1; }, this)
      .map(function (c) { return c.index; });
    this.rows().forEach(function (row) {
      if (row.getAttribute("data-einv-layout") === sig) return;
      var cells = Array.prototype.slice.call(row.cells);
      if (!row.hasAttribute("data-einv-layout")) cells.forEach(function (td, i) { td.setAttribute("data-einv-col", i); });
      var byCol = {};
      cells.forEach(function (td) { byCol[td.getAttribute("data-einv-col")] = td; });
      var inPlace = layout.every(function (col, pos) { return cells[pos] === byCol[col]; });
      if (!inPlace) layout.forEach(function (col) { row.appendChild(byCol[col]); });
      cells.forEach(function (td) { td.classList.toggle("einv-col-hidden", hiddenIdx.indexOf(+td.getAttribute("data-einv-col")) !== -1); });
      row.setAttribute("data-einv-layout", sig);
    });
  };

  Table.prototype.changed = function () {
    var self = this;
    this.dirty = true;
    local(cachePrefix + this.key, this.state);
    local(LEGACY_PREFIX + this.key, null);
    this.apply();
    this.syncMenu();
    clearTimeout(this.saveTimer);
    this.setStatus("Saving…");
    this.saveTimer = setTimeout(function () {
      saveToAccount(self.key, self.state, function (ok) {
        self.setStatus(ok ? "Saved to your account" : "Couldn't save to your account. Try again.");
      });
    }, 600);
  };

  Table.prototype.fromAccount = function (saved) {
    if (this.dirty) return; // the user changed something before the account copy arrived: theirs wins
    if (saved) {
      this.state = this.clean(saved);
      local(cachePrefix + this.key, this.state);
    } else if (Array.isArray(local(LEGACY_PREFIX + this.key))) {
      this.changed(); // a choice from before account saving: move it up to the account (once)
      return;
    } else if (typeof this.table.einvLegacyColumns === "function") {
      // The page's previous column system may hold this user's choice: adopt it once, then save it here.
      var self = this, legacyLoad = this.table.einvLegacyColumns;
      this.table.einvLegacyColumns = null;
      Promise.resolve().then(legacyLoad).then(function (old) {
        if (self.dirty || !old) return;
        var state = self.clean(old);
        if (!state.hidden.length && !state.order.length) return;
        self.state = state;
        self.changed();
      }, function () { /* old store unreadable: keep the defaults */ });
      this.state = this.defaults();
      local(cachePrefix + this.key, null);
    } else {
      // The account is the source of truth: nothing saved (e.g. Reset on another device) = defaults.
      this.state = this.defaults();
      local(cachePrefix + this.key, null);
    }
    this.apply();
    this.syncMenu();
  };

  // ---- Columns menu ---------------------------------------------------------------------------
  Table.prototype.buildMenu = function () {
    var self = this, menuId = "einv-cols-" + (++uid);
    var host = document.querySelector('[data-einv-columns-for="' + CSS.escape(this.key) + '"]');
    // A host among full-size page-header buttons (data-einv-columns-size="md") gets a matching button.
    var btnSize = host && host.getAttribute("data-einv-columns-size") === "md" ? "" : " btn-sm";
    var wrap = document.createElement("div");
    wrap.className = "einv-table-tools";
    wrap.innerHTML =
      '<div class="dropdown">' +
      '<button type="button" class="btn' + btnSize + ' btn-outline-secondary" data-bs-toggle="dropdown" data-bs-auto-close="outside" aria-expanded="false" aria-controls="' + menuId + '">' +
      '<i class="ri-layout-column-line" aria-hidden="true"></i> Columns</button>' +
      '<div class="dropdown-menu dropdown-menu-end einv-cols-menu" id="' + menuId + '">' +
      '<div class="einv-cols-title">Columns</div>' +
      '<p class="einv-cols-hint">Choose which columns to show, and drag to reorder them.</p>' +
      '<ul class="einv-cols-list"></ul>' +
      '<div class="einv-cols-foot"><span class="einv-cols-status" role="status"></span>' +
      '<button type="button" class="btn btn-sm btn-link px-0" data-einv-cols-reset>Reset</button></div>' +
      "</div></div>";
    this.list = wrap.querySelector(".einv-cols-list");
    this.status = wrap.querySelector(".einv-cols-status");
    this.items = {};

    this.movable.forEach(function (c) {
      var li = document.createElement("li");
      li.className = "einv-cols-item";
      li.setAttribute("data-col", c.id);
      var handle = document.createElement("button");
      handle.type = "button";
      handle.className = "einv-cols-handle";
      handle.setAttribute("aria-label", "Move " + c.name + " (Up or Down arrow key)");
      handle.innerHTML = '<i class="ri-draggable" aria-hidden="true"></i>';
      var lab = document.createElement("label");
      var cb = document.createElement("input");
      cb.type = "checkbox";
      cb.className = "form-check-input m-0";
      cb.disabled = !c.hideable;
      var text = document.createElement("span");
      text.textContent = c.name + (c.hideable ? "" : " (always shown)");
      lab.appendChild(cb);
      lab.appendChild(text);
      li.appendChild(handle);
      li.appendChild(lab);
      self.list.appendChild(li);
      self.items[c.id] = { li: li, checkbox: cb, col: c };

      cb.addEventListener("change", function () {
        self.state.hidden = self.state.hidden.filter(function (n) { return n !== c.id; });
        if (!cb.checked) self.state.hidden.push(c.id);
        self.changed();
      });
      li.einvTable = self;
      self.dragHandle(handle, li);
    });

    wrap.querySelector("[data-einv-cols-reset]").addEventListener("click", function () {
      self.state = self.defaults();
      self.changed();
    });

    if (host) host.appendChild(wrap);
    else {
      var anchor = this.table.closest(".table-responsive, .einv-table-scroll-x, .einv-table-sticky") || this.table;
      anchor.parentNode.insertBefore(wrap, anchor);
    }
    this.syncMenu();
  };

  // Pointer drag (mouse, pen and touch alike) on the handle. Tracked on window, not with pointer
  // capture: moving the row in the list releases any capture the handle held.
  Table.prototype.dragHandle = function (handle, li) {
    var self = this;
    handle.addEventListener("pointerdown", function (e) {
      if (e.button !== 0) return;
      e.preventDefault();
      li.classList.add("is-dragging");
      function move(ev) {
        var over = document.elementFromPoint(ev.clientX, ev.clientY);
        var target = over && over.closest(".einv-cols-item");
        if (!target || target === li || target.parentNode !== self.list) return;
        var box = target.getBoundingClientRect();
        self.list.insertBefore(li, ev.clientY > box.top + box.height / 2 ? target.nextElementSibling : target);
      }
      function up() {
        window.removeEventListener("pointermove", move);
        window.removeEventListener("pointerup", up);
        window.removeEventListener("pointercancel", up);
        li.classList.remove("is-dragging");
        self.commitOrder();
      }
      window.addEventListener("pointermove", move);
      window.addEventListener("pointerup", up);
      window.addEventListener("pointercancel", up);
    });
  };

  Table.prototype.moveByKey = function (li, up) {
    var sib = up ? li.previousElementSibling : li.nextElementSibling;
    if (!sib) return;
    this.list.insertBefore(li, up ? sib : sib.nextElementSibling);
    li.querySelector(".einv-cols-handle").focus();
    this.commitOrder();
  };

  Table.prototype.commitOrder = function () {
    var order = Array.prototype.map.call(this.list.children, function (li) { return li.getAttribute("data-col"); });
    var current = this.ordered().map(function (c) { return c.id; });
    if (order.join("\u0001") === current.join("\u0001")) return;
    var shown = this.cols.map(function (c) { return c.id; });
    // Columns this table doesn't have keep their saved place for the tables that do.
    this.state.order = order.concat(this.state.order.filter(function (id) { return shown.indexOf(id) === -1; }));
    this.changed();
  };

  Table.prototype.syncMenu = function () {
    if (!this.list) return;
    var self = this;
    this.ordered().forEach(function (c) {
      var it = self.items[c.id];
      self.list.appendChild(it.li);
      it.checkbox.checked = !c.hideable || self.state.hidden.indexOf(c.id) === -1;
    });
    if (!this.status.textContent) this.setStatus(userHash ? "Saved to your account" : "Saved on this device");
  };

  Table.prototype.setStatus = function (text) { if (this.status) this.status.textContent = text; };

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
    ths.forEach(function (th) {
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
        var index = th.cellIndex; // read now: the user may have reordered columns since load
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
    var created = [];
    (root || document).querySelectorAll("table[data-einv-table]").forEach(function (table) {
      if (table.hasAttribute("data-einv-table-ready")) return;
      table.setAttribute("data-einv-table-ready", "");
      var ths = headerCells(table);
      if (!ths.length) return;
      if (table.getAttribute("data-einv-sort") === "client") initSort(table, ths);
      var key = table.getAttribute("data-einv-table");
      if (ths.some(function (th) { return !isUtility(th); })) created.push(new Table(table, key, ths));
    });
    var keys = created.map(function (t) { return t.key; }).filter(function (k, i, a) { return a.indexOf(k) === i; });
    loadFromAccount(keys).then(function (saved) {
      if (!saved) return;
      created.forEach(function (t) { t.fromAccount(saved[t.key] || null); });
    });
  }

  // Arrow keys on a drag handle move that column. Caught on window in the capture phase because
  // Bootstrap's dropdown handles arrow keys on document (capture) and stops them reaching the menu.
  window.addEventListener("keydown", function (e) {
    if (e.key !== "ArrowUp" && e.key !== "ArrowDown") return;
    var handle = e.target && e.target.closest && e.target.closest(".einv-cols-handle");
    var li = handle && handle.parentNode;
    if (!li || !li.einvTable) return;
    e.preventDefault();
    e.stopPropagation();
    li.einvTable.moveByKey(li, e.key === "ArrowUp");
  }, true);

  window.einvworld = window.einvworld || {};
  window.einvworld.initTables = init;
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { init(); });
  else init();
})();
