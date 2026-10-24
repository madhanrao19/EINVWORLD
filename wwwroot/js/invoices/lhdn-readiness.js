// "Ready for LHDN" panel on Create e-Invoice and Edit Invoice (Step 3). Shows the server's check of the SAVED
// draft (?handler=Readiness, the same check every Submit runs), links each problem to its field, and keeps
// Submit to LHDN disabled while anything blocks. The server stays authoritative; this only explains it.
(function () {
    'use strict';

    const SECTION_LABELS = {
        Supplier: 'Supplier details', Buyer: 'Buyer details', Invoice: 'Invoice information',
        Items: 'Items', Tax: 'Tax', Totals: 'Totals'
    };

    function panel() { return document.getElementById('lhdnReadiness'); }

    // The check is of the SAVED draft, so after any edit it no longer describes what Submit would send.
    let checkedSnapshot = null;
    let lastResult = null;

    function formSnapshot() {
        const form = document.getElementById('invoice_form');
        if (!form) return null;
        return [...new FormData(form)]
            .filter(([k, v]) => typeof v === 'string' && k !== '__RequestVerificationToken')
            .map(([k, v]) => `${k}=${v}`)
            .join('&');
    }

    function saveDraftButton() { return document.querySelector('button[name="action"][value="saveDraft"]'); }

    function showChangesNotChecked() {
        const box = panel();
        if (!box || box.hidden || checkedSnapshot === null) return;
        const dirty = formSnapshot() !== checkedSnapshot;
        const status = box.querySelector('.js-readiness-status');
        const issues = box.querySelector('.js-readiness-issues');
        let saveCheck = box.querySelector('.js-readiness-savecheck');
        if (!saveCheck) {
            saveCheck = el('button', 'btn btn-sm btn-outline-primary mt-2 js-readiness-savecheck', 'Save & Check');
            saveCheck.type = 'button';
            saveCheck.addEventListener('click', () => saveDraftButton()?.click()); // the normal save refreshes this panel
            box.querySelector('.card-body').appendChild(saveCheck);
        }
        saveCheck.hidden = !dirty;
        if (issues) issues.style.opacity = dirty ? '0.5' : '';
        const submit = document.getElementById('sa-success-submit-lhdn');
        if (dirty) {
            status.textContent = 'Changes not checked';
            status.className = 'js-readiness-status badge bg-secondary';
            if (submit) submit.disabled = true; // Submit sends the saved draft, not these edits
        } else if (lastResult) {
            render(lastResult);
        }
    }

    function stepOf(field) {
        const step = field.closest('[id^="step"]');
        const n = step ? parseInt(step.id.replace('step', ''), 10) : NaN;
        return isNaN(n) ? null : n;
    }

    function focusField(name) {
        const field = document.querySelector(`[name="${CSS.escape(name)}"]`);
        if (!field) return false;
        const n = stepOf(field);
        if (n && typeof window.goToStep === 'function') window.goToStep(n);
        let target = field;
        if (window.jQuery && jQuery(field).hasClass('select2-hidden-accessible')) {
            target = jQuery(field).next('.select2-container').find('.select2-selection')[0] || field;
        }
        // Let the step become visible before scrolling to it.
        setTimeout(() => {
            target.scrollIntoView({ behavior: 'smooth', block: 'center' });
            target.focus({ preventScroll: true });
            field.classList.add('is-invalid');
        }, 50);
        return true;
    }

    function el(tag, className, text) {
        const e = document.createElement(tag);
        if (className) e.className = className;
        if (text != null) e.textContent = text; // text only: messages are never parsed as HTML
        return e;
    }

    function render(data) {
        const box = panel();
        const list = box.querySelector('.js-readiness-sections');
        const issues = box.querySelector('.js-readiness-issues');
        list.replaceChildren();
        issues.replaceChildren();

        const items = data.items || [];
        (data.sections || Object.keys(SECTION_LABELS)).forEach(section => {
            const mine = items.filter(i => i.section === section);
            const blocked = mine.some(i => i.blocking);
            const li = el('li', 'd-flex align-items-center gap-2 mb-1');
            li.appendChild(el('i', blocked ? 'ri-error-warning-fill text-danger' : mine.length ? 'ri-alert-line text-warning' : 'ri-checkbox-circle-fill text-success'));
            li.appendChild(el('span', null, SECTION_LABELS[section] || section));
            list.appendChild(li);
        });

        if (items.length) {
            const blocking = items.filter(i => i.blocking).length;
            issues.appendChild(el('div', 'fw-bold mt-2 mb-1', blocking ? `Needs attention (${blocking})` : 'Check before submitting'));
            items.forEach(item => {
                const row = el('div', 'd-flex align-items-start gap-2 mb-1');
                row.appendChild(el('i', item.blocking ? 'ri-error-warning-line text-danger' : 'ri-alert-line text-warning'));
                const body = el('div', 'flex-grow-1 small', item.message);
                if (item.field && document.querySelector(`[name="${CSS.escape(item.field)}"]`)) {
                    const fix = el('button', 'btn btn-link btn-sm p-0 ms-1 align-baseline', 'Fix');
                    fix.type = 'button';
                    fix.addEventListener('click', () => focusField(item.field));
                    body.appendChild(fix);
                }
                row.appendChild(body);
                issues.appendChild(row);
            });
        }

        const status = box.querySelector('.js-readiness-status');
        status.textContent = data.ready ? 'Ready for LHDN' : 'Not ready for LHDN';
        status.className = 'js-readiness-status badge ' + (data.ready ? 'bg-success' : 'bg-danger');

        const submit = document.getElementById('sa-success-submit-lhdn');
        if (submit) submit.disabled = !data.ready;
        const saveCheck = box.querySelector('.js-readiness-savecheck');
        if (saveCheck) saveCheck.hidden = true;
        if (issues) issues.style.opacity = '';
    }

    async function refresh() {
        const box = panel();
        const invoiceNo = document.getElementById('invoiceNo')?.value;
        const hasDraft = !!document.getElementById('draftFilePath')?.value;
        if (!box || !invoiceNo || !hasDraft) return;
        try {
            const url = `${window.location.pathname}?handler=Readiness&invoiceNo=${encodeURIComponent(invoiceNo)}`;
            const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' });
            const data = await response.json();
            if (!data.success) return;
            box.hidden = false;
            lastResult = data;
            checkedSnapshot = formSnapshot();
            render(data);
        } catch (error) {
            console.error('Ready for LHDN check failed:', error); // the Submit handler still runs the check
        }
    }

    window.LhdnReadiness = { refresh, focusField };
    document.addEventListener('DOMContentLoaded', () => {
        refresh(); // a reopened draft is checked straight away
        // Select2 changes arrive through jQuery's trigger('change'), which addEventListener never sees.
        const onEdit = () => setTimeout(showChangesNotChecked, 0);
        if (window.jQuery) jQuery(document).on('input change', '#invoice_form :input', onEdit);
        else document.addEventListener('input', onEdit, true);
        document.getElementById('invoice_form')?.addEventListener('click', onEdit); // add / remove item rows
    });
})();
