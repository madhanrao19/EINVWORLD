// bulk-submit-invoice.js
//
// "Submit to LHDN" bulk action for Draft invoices. Mirrors delete-invoice.js: it loops the checked
// rows one request at a time so each invoice is submitted through the SAME server-side guarded path
// as the single-row action (handler=BulkSubmitOne -> SubmitDraftCoreAsync), i.e. per-invoice atomic
// double-submit claim, per-TIN ownership check and payload-hash idempotency. Failures don't abort the
// batch — the server marks TransmissionError + queues a background retry — and the user gets an
// aggregated per-invoice summary. Drafts only.

document.addEventListener("click", function (event) {
    const btn = event.target.closest("#bulkSubmitBtn");
    if (!btn) return;

    const selected = Array.from(document.querySelectorAll(".invoice-checkbox:checked"))
        .map(cb => cb.dataset.invoiceno)
        .filter(Boolean);

    if (selected.length === 0) {
        Swal.fire("No Selection", "Please select at least one draft invoice to submit.", "warning");
        return;
    }

    Swal.fire({
        title: 'Submit to LHDN?',
        html: `You are about to submit <b>${selected.length}</b> draft invoice(s) to LHDN MyInvois.<br>` +
              `Each is validated individually and, once accepted, <b>cannot be un-submitted</b> (only cancelled within 72 hours).`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: `Yes, submit ${selected.length}`,
        cancelButtonText: 'Cancel',
        buttonsStyling: false,
        showCloseButton: true,
        customClass: {
            confirmButton: 'btn btn-primary w-xs me-2 mt-2',
            cancelButton: 'btn btn-outline-secondary w-xs mt-2'
        }
    }).then((result) => {
        if (result.isConfirmed) {
            executeBulkSubmit(selected);
        }
    });
});

async function executeBulkSubmit(invoiceNumbers) {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenEl ? tokenEl.value : '';
    let successCount = 0;
    const errorMessages = [];

    Swal.fire({
        title: 'Submitting to LHDN...',
        html: `Processing <b id="bulkSubmitProgress">0</b> of ${invoiceNumbers.length}. Please do not close this page.`,
        allowOutsideClick: false,
        allowEscapeKey: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    for (let i = 0; i < invoiceNumbers.length; i++) {
        const invoiceNo = invoiceNumbers[i];
        try {
            const response = await fetch('/Invoices/InvoiceLists?handler=BulkSubmitOne', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ invoiceNo: invoiceNo })
            });

            const data = await response.json();
            if (data.success) {
                successCount++;
            } else {
                errorMessages.push(`Invoice #${invoiceNo}: ${data.message || 'Failed to submit.'}`);
            }
        } catch (error) {
            console.error(`Bulk submit error for ${invoiceNo}:`, error);
            errorMessages.push(`Invoice #${invoiceNo}: A network error occurred — a background retry may have been queued.`);
        }

        const progress = document.getElementById('bulkSubmitProgress');
        if (progress) progress.textContent = String(i + 1);
    }

    if (errorMessages.length > 0) {
        const errorHtml = errorMessages.join('<br>');
        Swal.fire({
            title: 'Submission Completed with Issues',
            html: `${successCount} invoice(s) submitted successfully.<br><br>` +
                  `<b style="color:#dc2626">Not submitted (${errorMessages.length}):</b><br><small>${errorHtml}</small>`,
            icon: 'warning'
        }).then(() => location.reload());
    } else {
        Swal.fire('Submitted!', `${successCount} invoice(s) submitted to LHDN successfully.`, 'success')
            .then(() => location.reload());
    }
}
