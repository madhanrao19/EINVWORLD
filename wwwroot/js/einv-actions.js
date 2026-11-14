/*
 * Event wiring without inline handlers. The Content-Security-Policy refuses onclick="…", onchange="…" and
 * javascript: links, so markup declares behaviour with data- attributes and this file handles them by delegation:
 *
 *   data-on-click / data-on-change / data-on-input / data-on-submit = "functionName"
 *       Calls a page function, but only one the page has allowed: einvActions.allow(['functionName', …]).
 *       data-args = JSON array of arguments; "$this" = the element, "$event" = the event, "$value" = element.value,
 *       "$data:name" = element.dataset.name. A function returning false cancels the event (like "return f()").
 *   data-confirm="message"   on a form: asks before submitting (replaces onsubmit="return confirm(…)").
 *   data-autosubmit          on a field: submits its form when changed.
 *   data-reload / data-print on a button: reloads / prints the page.
 *   data-no-nav              on a link: never navigates (was href="javascript:void(0)").
 *   data-show="id"           on a button: shows element #id (data-show-display, default block).
 *   data-busy-disable / data-busy-show = "id"   on a form: when it submits, disables a button / shows a spinner.
 *   data-no-paste / data-no-typing   on a field: refuses pasting / key presses.
 *   data-no-submit           on a form: never submits natively (search boxes driven by script).
 *   data-file-label="id"     on a file input: shows the chosen file name(s) in element #id
 *       (data-file-empty = text when none, data-file-many = suffix when several, e.g. " files selected").
 *
 * Loaded in <head> of every layout, before any page script. No eval: only allowed, named functions are called.
 */
(function () {
    'use strict';
    var allowed = Object.create(null);

    window.einvActions = {
        /** Lets markup call these global functions. Call from the page's own script or view. */
        allow: function (names) { (names || []).forEach(function (n) { allowed[n] = true; }); }
    };

    function args(el, ev) {
        var raw = el.getAttribute('data-args'), list = [];
        if (raw) {
            try { list = JSON.parse(raw); } catch (e) { list = []; }
            if (!Array.isArray(list)) list = [list];
        }
        return list.map(function (a) {
            if (a === '$this') return el;
            if (a === '$event') return ev;
            if (a === '$value') return el.value;
            if (typeof a === 'string' && a.indexOf('$data:') === 0) return el.dataset[a.slice(6)];
            return a;
        });
    }

    function dispatch(type) {
        var attr = 'data-on-' + type;
        document.addEventListener(type, function (ev) {
            var el = ev.target && ev.target.closest ? ev.target.closest('[' + attr + ']') : null;
            if (!el || el.disabled) return;
            if (el.tagName === 'A' && (el.getAttribute('href') || '#') === '#') ev.preventDefault();
            var name = el.getAttribute(attr), fn = allowed[name] ? window[name] : null;
            if (typeof fn !== 'function') { console.warn('einvActions: "' + name + '" is not allowed or not defined'); return; }
            if (fn.apply(el, args(el, ev)) === false) ev.preventDefault();
        });
    }
    ['click', 'change', 'input', 'submit'].forEach(dispatch);

    // Capture phase: the confirmation runs before any other submit handler, as onsubmit did.
    document.addEventListener('submit', function (ev) {
        var form = ev.target;
        if (!form || !form.getAttribute) return;
        if (form.hasAttribute('data-no-submit')) { ev.preventDefault(); return; } // JS-driven forms (was onsubmit="return false")
        var msg = form.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) { ev.preventDefault(); ev.stopImmediatePropagation(); }
    }, true);

    document.addEventListener('change', function (ev) {
        var el = ev.target;
        if (el && el.hasAttribute && el.hasAttribute('data-autosubmit') && el.form) el.form.submit();
        if (el && el.hasAttribute && el.hasAttribute('data-file-label')) {
            var out = document.getElementById(el.getAttribute('data-file-label')), n = el.files ? el.files.length : 0;
            if (out) out.textContent = n === 0 ? (el.getAttribute('data-file-empty') || '')
                : n === 1 ? el.files[0].name : n + (el.getAttribute('data-file-many') || ' files');
        }
    });

    document.addEventListener('click', function (ev) {
        var t = ev.target && ev.target.closest ? ev.target : null;
        if (!t) return;
        // data-no-nav: a link that only triggers script (was href="javascript:void(0)")
        if (t.closest('a[data-no-nav]')) ev.preventDefault();
        // data-show="id" (+ data-show-display, default block): reveal an element, e.g. a loading overlay
        var show = t.closest('[data-show]');
        if (show) {
            var target = document.getElementById(show.getAttribute('data-show'));
            if (target) target.style.display = show.getAttribute('data-show-display') || 'block';
        }
        var el = t.closest('[data-reload],[data-print]');
        if (!el) return;
        ev.preventDefault();
        if (el.hasAttribute('data-reload')) window.location.reload(); else window.print();
    });

    // data-busy-disable="id" / data-busy-show="id" on a form: once it really submits, disable a button and show a spinner.
    document.addEventListener('submit', function (ev) {
        var form = ev.target;
        if (ev.defaultPrevented || !form || !form.getAttribute) return;
        var btn = document.getElementById(form.getAttribute('data-busy-disable') || '');
        var spin = document.getElementById(form.getAttribute('data-busy-show') || '');
        if (btn) btn.disabled = true;
        if (spin) spin.style.display = 'inline-block';
    });

    // data-no-paste: pasting refused (was onpaste="return false"); data-no-typing: keys refused (was onkeydown="return false").
    document.addEventListener('paste', function (ev) {
        if (ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-no-paste')) ev.preventDefault();
    });
    document.addEventListener('keydown', function (ev) {
        if (ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-no-typing')) ev.preventDefault();
    });
})();
