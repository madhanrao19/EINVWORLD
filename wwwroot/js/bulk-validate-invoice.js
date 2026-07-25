// bulk-validate-invoice.js
//
// "Bulk Validate" — re-checks LHDN status for the selected Sent/Received invoices. Unlike
// bulk-submit-invoice.js this is a SINGLE server call (handler=BulkValidate): the server already
// loops the selected invoices behind a per-user single-flight gate and the same cooldown/429-abort
// rules the background sync and active-session poke use, so looping per-invoice client-side would
// just add redundant round-trips without any extra safety.

document.addEventListener("click", function (event) {
    const btn = event.target.closest("#bulkValidateBtn");
    if (!btn) return;

    const selected = Array.from(document.querySelectorAll(".invoice-checkbox:checked"))
        .map(cb => cb.value)
        .filter(Boolean);

    if (selected.length === 0) {
        Swal.fire("No Selection", "Please select at least one invoice to validate.", "warning");
        return;
    }

    Swal.fire({
        title: 'Re-check LHDN status?',
        html: `Re-check LHDN validation status for <b>${selected.length}</b> selected invoice(s).`,
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Yes, check now',
        cancelButtonText: 'Cancel',
        buttonsStyling: false,
        showCloseButton: true,
        customClass: {
            confirmButton: 'btn btn-primary w-xs me-2 mt-2',
            cancelButton: 'btn btn-outline-secondary w-xs mt-2'
        }
    }).then((result) => {
        if (result.isConfirmed) {
            executeBulkValidate(selected);
        }
    });
});

async function executeBulkValidate(uuids) {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenEl ? tokenEl.value : '';

    Swal.fire({
        title: 'Checking LHDN status...',
        html: `Checking ${uuids.length} invoice(s). Please do not close this page.`,
        allowOutsideClick: false,
        allowEscapeKey: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    try {
        const response = await fetch('/Invoices/InvoiceLists?handler=BulkValidate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(uuids)
        });

        const data = await response.json();

        if (!data.success) {
            Swal.fire('Could Not Check Status', data.message || 'Please try again.', 'warning');
            return;
        }

        if (data.checkedCount === 0) {
            Swal.fire('Nothing to Check', 'The selected invoices are all in a final status and cannot change.', 'info');
            return;
        }

        Swal.fire(
            'Checked!',
            `${data.checkedCount} invoice(s) checked — ${data.updatedCount} had a status change.`,
            'success'
        ).then(() => {
            if (data.updatedCount > 0) location.reload();
        });
    } catch (error) {
        console.error('Bulk validate error:', error);
        Swal.fire('Error', 'A network error occurred while checking status. Please try again.', 'error');
    }
}
