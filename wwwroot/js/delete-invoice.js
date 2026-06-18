// delete-invoice.js

window.confirmDelete = function (invoiceNo) {
    Swal.fire({
        title: 'Are you sure?',
        html: `You are about to delete invoice <b>#${invoiceNo}</b>.<br>This action cannot be undone.`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Yes, delete it!',
        cancelButtonText: 'Cancel',
        buttonsStyling: false,
        showCloseButton: true,
        customClass: {
            confirmButton: 'btn btn-danger w-xs me-2 mt-2',
            cancelButton: 'btn btn-outline-secondary w-xs mt-2'
        }
    }).then((result) => {
        if (result.isConfirmed) {
            executeDelete([invoiceNo]);
        }
    });
};

// Bulk Delete Listener
document.addEventListener("click", function (event) {
    const bulkDeleteBtn = event.target.closest("#bulkDeleteBtn");

    if (bulkDeleteBtn) {
        // Collect all checked invoice numbers
        let selectedInvoices = Array.from(document.querySelectorAll(".invoice-checkbox:checked"))
            .map(checkbox => checkbox.dataset.invoiceno);

        if (selectedInvoices.length === 0) {
            Swal.fire("No Selection", "Please select at least one invoice to delete.", "warning");
            return;
        }

        Swal.fire({
            title: 'Delete Multiple Invoices?',
            html: `You are about to delete <b>${selectedInvoices.length}</b> invoice(s).<br>This action cannot be undone.`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, delete them!',
            cancelButtonText: 'Cancel',
            buttonsStyling: false,
            showCloseButton: true,
            customClass: {
                confirmButton: 'btn btn-danger w-xs me-2 mt-2',
                cancelButton: 'btn btn-outline-secondary w-xs mt-2'
            }
        }).then((result) => {
            if (result.isConfirmed) {
                executeDelete(selectedInvoices);
            }
        });
    }
});

// Reusable function to handle API calls for single or multiple deletions
async function executeDelete(invoiceNumbers) {
    let successCount = 0;
    let errorMessages = [];

    // Show loading spinner
    Swal.fire({
        title: 'Deleting...',
        text: 'Please wait while we delete the selected invoice(s).',
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    for (let invoiceNo of invoiceNumbers) {
        try {
            let response = await fetch('/Invoices/InvoiceLists?handler=DeleteFromList', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ invoiceNo: invoiceNo })
            });

            let data = await response.json();
            if (data.success) {
                successCount++;
                const row = document.getElementById(`row-${invoiceNo}`);
                if (row) row.remove(); // Remove from UI
            } else {
                errorMessages.push(`Invoice #${invoiceNo}: ${data.message || 'Failed to delete.'}`);
            }
        } catch (error) {
            console.error(`Delete error for ${invoiceNo}:`, error);
            errorMessages.push(`Invoice #${invoiceNo}: An unexpected network error occurred.`);
        }
    }

    // Resolve results
    if (errorMessages.length > 0) {
        let errorHtml = errorMessages.join('<br>');
        Swal.fire({
            title: 'Partial Success',
            html: `${successCount} invoice(s) deleted successfully.<br><br><b style="color:red">Errors:</b><br><small>${errorHtml}</small>`,
            icon: 'warning'
        }).then(() => location.reload()); // Reload to reset table states
    } else {
        Swal.fire('Deleted!', `${successCount} invoice(s) deleted successfully.`, 'success').then(() => {
            // Uncheck 'Select All' if checked
            const selectAll = document.getElementById("selectAll");
            if (selectAll) selectAll.checked = false;

            // Reload page if all current rows were deleted
            const remainingRows = document.querySelectorAll("#invoiceTable tbody tr").length;
            if (remainingRows === 0) location.reload();
        });
    }
}