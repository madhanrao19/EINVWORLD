// bulk-send-to-buyers.js
//
// "Send to Buyers" — manually resends the validated-invoice email (existing template, existing PDF,
// existing EInvoiceNotificationService) for the selected Sent invoices. This sends REAL email — the
// confirmation step below is deliberate and must not be removed or auto-confirmed.

document.addEventListener("click", function (event) {
    const btn = event.target.closest("#bulkSendToBuyersBtn");
    if (!btn) return;

    const selected = Array.from(document.querySelectorAll(".invoice-checkbox:checked"))
        .map(cb => cb.value)
        .filter(Boolean);

    if (selected.length === 0) {
        Swal.fire("No Selection", "Please select at least one invoice to send.", "warning");
        return;
    }

    Swal.fire({
        title: 'Send invoice email to buyers?',
        html: `This will email the validated invoice (with PDF) to the buyer of each of the <b>${selected.length}</b> selected invoice(s). ` +
              `Only invoices already validated by LHDN will actually be sent.`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: `Yes, send ${selected.length}`,
        cancelButtonText: 'Cancel',
        buttonsStyling: false,
        showCloseButton: true,
        customClass: {
            confirmButton: 'btn btn-primary w-xs me-2 mt-2',
            cancelButton: 'btn btn-outline-secondary w-xs mt-2'
        }
    }).then((result) => {
        if (result.isConfirmed) {
            executeBulkSendToBuyers(selected);
        }
    });
});

async function executeBulkSendToBuyers(uuids) {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenEl ? tokenEl.value : '';

    Swal.fire({
        title: 'Sending emails...',
        html: `Sending to ${uuids.length} buyer(s). Please do not close this page.`,
        allowOutsideClick: false,
        allowEscapeKey: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    try {
        const response = await fetch('/Invoices/InvoiceLists?handler=BulkSendToBuyers', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(uuids)
        });

        const data = await response.json();

        if (!data.success) {
            Swal.fire('Could Not Send', 'Please try again.', 'warning');
            return;
        }

        if (data.skippedCount > 0) {
            const skippedHtml = (data.skipped || []).join('<br>');
            Swal.fire({
                title: 'Sent With Some Skipped',
                html: `${data.sentCount} email(s) sent successfully.<br><br>` +
                      `<b style="color:#dc2626">Skipped (${data.skippedCount}):</b><br><small>${skippedHtml}</small>`,
                icon: 'warning'
            });
        } else {
            Swal.fire('Sent!', `${data.sentCount} invoice email(s) sent to buyers.`, 'success');
        }
    } catch (error) {
        console.error('Bulk send to buyers error:', error);
        Swal.fire('Error', 'A network error occurred while sending. Please try again.', 'error');
    }
}
