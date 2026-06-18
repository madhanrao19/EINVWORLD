document.addEventListener("DOMContentLoaded", function () {
    console.log("📌 DOM fully loaded and ready!");

    // Cancel Invoice button functionality - InvoiceDetails
    document.getElementById("cancelInvoiceBtn")?.addEventListener("click", function () {
        let uuid = this.dataset.uuid;
        let invoiceNo = this.dataset.invoiceno;
        console.log(`📌 Cancel Invoice clicked: ${invoiceNo}, UUID: ${uuid}`);
        showCancelModal([{ uuid, invoiceNo }]);
    });

    // Select All functionality
    let selectAllCheckbox = document.getElementById("selectAll");
    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener("click", function () {
            let isChecked = this.checked;
            console.log(`🔄 Select All clicked, setting all checkboxes to: ${isChecked}`);

            document.querySelectorAll(".invoice-checkbox").forEach(checkbox => {
                checkbox.checked = isChecked;
            });
        });
    }

    // Individual Cancel button functionality
    document.querySelectorAll(".cancel-btn").forEach(button => {
        button.addEventListener("click", function () {
            let uuid = this.dataset.uuid;
            let invoiceNo = this.dataset.invoiceno;
            let tin = this.dataset.tin;
            console.log(`📌 Cancel button clicked for Invoice No: ${invoiceNo}, UUID: ${uuid}, TIN: ${tin}`);
            showCancelModal([{ uuid, invoiceNo, tin }]);
        });
    });

    // Bulk Cancel button functionality
    document.addEventListener("click", function (event) {
        if (event.target && event.target.id === "bulkCancelBtn") {
            let selectedInvoices = Array.from(document.querySelectorAll(".invoice-checkbox:checked"))
                .map(checkbox => ({ uuid: checkbox.value, invoiceNo: checkbox.dataset.invoiceno, tin: checkbox.dataset.tin }));

            console.log(`📌 Bulk Cancel clicked. Selected invoices:`, selectedInvoices);

            if (selectedInvoices.length === 0) {
                Swal.fire("No Selection", "Please select at least one invoice.", "warning");
                return;
            }

            showCancelModal(selectedInvoices);
        }
    });

    let cancellationReasons = [];
    try {
        let reasonsJson = document.getElementById("rejectionReasons")?.value || "[]";
        cancellationReasons = JSON.parse(reasonsJson);
        console.log("🚀 Loaded cancellation reasons:", cancellationReasons);
    } catch (error) {
        console.error("❌ Error parsing cancellation reasons:", error);
        cancellationReasons = ["Duplicate invoice", "Wrong details", "Customer request", "Others"]; // Fallback
    }

    window.cancellationReasons = cancellationReasons;

    function showCancelModal(invoices) {
        console.log(`📌 Showing cancel modal for invoices:`, invoices);

        let invoiceListHtml = invoices.map(inv => `<li><strong>${inv.invoiceNo}</strong> (UUID: ${inv.uuid})</li>`).join('');
        let reasonOptions = { "": "Select a reason" };
        cancellationReasons.forEach(reason => {
            reasonOptions[reason] = reason;
        });

        Swal.fire({
            title: "Cancel Invoice(s)",
            html: `
            <div style="display: flex; flex-direction: column; gap: 10px;">
                <select class="swal2-select">
                    ${Object.entries(reasonOptions).map(([value, text]) =>
                `<option value="${value}">${text}</option>`).join('')}
                </select>
                <textarea class="swal2-textarea" style="display:none;" placeholder="Please provide details for 'Others'..." disabled></textarea>
            </div>`,
            showCancelButton: true,
            confirmButtonText: "Cancel",
            cancelButtonText: "Close",
            preConfirm: () => {
                const reason = document.querySelector(".swal2-select").value;
                const details = document.querySelector(".swal2-textarea").value.trim();

                if (!reason) {
                    Swal.showValidationMessage("Please select a valid cancellation reason");
                    return false;
                }

                if (reason === "Others" && !details) {
                    Swal.showValidationMessage("Please provide cancellation details for 'Others'.");
                    return false;
                }

                return { reason, details };
            },
            didOpen: () => {
                const selectElement = document.querySelector(".swal2-select");
                const textareaElement = document.querySelector(".swal2-textarea");

                selectElement.addEventListener('change', function () {
                    if (this.value === "Others") {
                        textareaElement.style.display = "block";
                        textareaElement.disabled = false;
                    } else {
                        textareaElement.style.display = "none";
                        textareaElement.disabled = true;
                    }
                });
            }
        }).then((result) => {
            if (result.isConfirmed) {
                let status = 'cancelled';
                let cancellationReason = result.value.reason;
                let cancellationDetails = result.value.details;
                console.log(`✅ Cancellation confirmed. Reason: ${cancellationReason}, Details: ${cancellationDetails}`);
                cancelInvoices(invoices, status, cancellationReason, cancellationDetails);
            } else {
                console.log("❌ Cancellation cancelled by user.");
            }
        });
    }


    async function cancelInvoices(invoices, status, reason, details) {
        console.log("🚀 Cancel Invoices Triggered!");
        console.log(`📌 Selected Invoices:`, invoices);

        if (!invoices || invoices.length === 0) {
            console.warn("⚠️ No invoices provided for cancellation.");
            return;
        }

        let successCount = 0;
        let errorMessages = [];

        for (let invoice of invoices) {
            try {
                let response = await fetch(`/Invoices/InvoiceLists?handler=CancelDocument&documentId=${invoice.uuid}&cancellationReason=${reason}&tin=${invoice.tin}`, {
                    method: "PUT",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
                    }
                });

                console.log(`📩 Response received for Invoice: ${invoice.invoiceNo} - Status: ${response.status}`);

                if (!response.ok) {
                    console.error(`❌ Error cancelling Invoice ${invoice.invoiceNo}. Status: ${response.status}`);
                    let errorText = await response.text(); // Get raw text if it's not JSON
                    errorMessages.push("Error", `Failed to cancel invoice ${invoice.invoiceNo}.\n${errorText}`, "error");
                    throw new Error(errorText);
                }

                let data;
                try {
                    data = await response.json();
                } catch (jsonError) {
                    console.error(`❌ Invalid JSON response for Invoice ${invoice.invoiceNo}:`, jsonError);
                    let errorText = await response.text();
                    errorMessages.push("Error", `Failed to cancel invoice ${invoice.invoiceNo}. Invalid JSON response.\n${errorText}`, "error");
                    throw new Error("Invalid JSON response");
                }

                console.log(`✅ Server response for Invoice: ${invoice.invoiceNo}:`, data);

                if (!data) {
                    console.error(`🚨 Server response is null or undefined for Invoice ${invoice.invoiceNo}`);
                    errorMessages.push("Error", `Failed to cancel invoice ${invoice.invoiceNo}.\nServer response is empty.`, "error");
                    throw new Error("Server response is empty.");
                }
                else if (!data.message) {
                    console.error(`🚨 Server response does not contain a message for Invoice ${invoice.invoiceNo}:`, data);
                    errorMessages.push("Error", `Failed to cancel invoice ${invoice.invoiceNo}.\nServer response is missing message field.`, "error");
                    throw new Error("Server response is missing message field.");
                }
                else if (!data.message.includes("Document cancellation successfully processed.")) {
                    console.error(`🚨 Server message did not match expected text for Invoice ${invoice.invoiceNo}:`, data.message);
                    errorMessages.push("Error", `Failed to cancel invoice ${invoice.invoiceNo}.\nUnexpected response: ${data.message}`, "error");
                    throw new Error(`Unexpected response: ${data.message}`);
                }
                else {
                    console.log(`✅ Invoice ${invoice.invoiceNo} successfully cancelled.`);
                    successCount++;
                }
            } catch (error) {
                console.error(`🚨 Request failed for Invoice ${invoice.invoiceNo}:`, error.message);

                let errorMsg = error.message || "Unknown error occurred";

                if (!errorMessages.some(err => err.invoiceNo === invoice.invoiceNo)) {
                    errorMessages.push({
                        invoiceNo: invoice.invoiceNo || "Unknown",
                        message: errorMsg,
                    });
                }
            }
        }
        let errorMap = new Map();

        errorMessages.forEach(err => {
            if (typeof err.message === "string" && !err.message.includes("undefined")) {
                let key = err.message; // Group by error message
                if (!errorMap.has(key)) {
                    errorMap.set(key, { count: 1, invoiceNos: [err.invoiceNo] });
                } else {
                    let existing = errorMap.get(key);
                    existing.count += 1;
                    existing.invoiceNos.push(err.invoiceNo);
                    errorMap.set(key, existing);
                }
            }
        });
        if (successCount > 0 || errorMessages.length > 0) {
            let formattedErrors = errorMessages.length ?
                Array.from(errorMap.entries()).map(([message, data]) => {
                    let invoiceText = data.invoiceNos.length > 3
                        ? `${data.invoiceNos.slice(0, 3).join(", ")} and ${data.invoiceNos.length - 3} more`
                        : data.invoiceNos.join(", ");
                    return `<div style="background: rgba(255, 77, 79, 0.1); padding: 12px; border-radius: 6px; 
                    margin-bottom: 6px; border-left: 4px solid #ff4d4f;">
                <small style="color: #ff4d4f; font-weight: bold;">Invoice(s): ${invoiceText}</small><br>
                <span style="font-size: 13px; color: #555;">${message}</span>
            </div>`;
                }).join("") : "";

            let title = successCount > 0 && errorMessages.length > 0 ? "Partial Success"
                : successCount > 0 ? "Cancelled!"
                    : "Invoice(s) could not be cancelled";

            let icon = successCount > 0 && errorMessages.length > 0 ? "warning"
                : successCount > 0 ? "success"
                    : "error";

            let message = successCount > 0 && errorMessages.length > 0
                ? `<p>${successCount} invoice(s) have been cancelled successfully.</p><hr>
           <p>However, the following invoice(s) failed to cancel:</p>${formattedErrors}`
                : successCount > 0
                    ? `${successCount} invoice(s) have been cancelled successfully.`
                    : formattedErrors;

            Swal.fire({ title, icon, html: message, confirmButtonText: "OK" })
                .then(() => { if (successCount > 0) location.reload(); });
        }

        console.log("✅ All selected invoices have been processed.");
    }
});
