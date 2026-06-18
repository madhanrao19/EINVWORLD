document.addEventListener("DOMContentLoaded", function () {
    console.log("📌 DOM fully loaded and ready!");

    // Reject Invoice button functionality - InvoiceDetails
    document.getElementById("rejectInvoiceBtn")?.addEventListener("click", function () {
        let uuid = this.dataset.uuid;
        let invoiceNo = this.dataset.invoiceno;
        console.log(`📌 Reject Invoice clicked: ${invoiceNo}, UUID: ${uuid}`);
        showRejectModal([{ uuid, invoiceNo }]);
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

    // Individual Reject button functionality
    document.querySelectorAll(".reject-btn").forEach(button => {
        button.addEventListener("click", function () {
            let uuid = this.dataset.uuid;
            let invoiceNo = this.dataset.invoiceno;
            let tin = this.dataset.tin;
            console.log(`📌 Reject button clicked for Invoice: ${uuid}, TIN: ${tin}`);
            showRejectModal([{ uuid: uuid, invoiceNo: invoiceNo, tin: tin }]);
        });
    });

    // Bulk Reject button functionality
    document.addEventListener("click", function (event) {
        if (event.target && event.target.id === "bulkRejectBtn") {
            let selectedInvoices = Array.from(document.querySelectorAll(".invoice-checkbox:checked"))
                .map(checkbox => ({
                    uuid: checkbox.value,
                    invoiceNo: checkbox.dataset.invoiceno,
                    tin: checkbox.dataset.tin
                }));

            console.log(`📌 Bulk Reject clicked. Selected invoices: ${selectedInvoices}`);

            if (selectedInvoices.length === 0) {
                Swal.fire("No Selection", "Please select at least one invoice.", "warning");
                return;
            }

            showRejectModal(selectedInvoices);
        }
    });


    let rejectionReasons = [];
    try {
        let reasonsJson = document.getElementById("rejectionReasons")?.value || "[]";
        rejectionReasons = JSON.parse(reasonsJson);
        console.log("🚀 Loaded rejection reasons:", rejectionReasons);
    } catch (error) {
        console.error("❌ Error parsing rejection reasons:", error);
        rejectionReasons = ["Wrong supplier details", "Wrong buyer details", "Wrong invoice details", "Others"]; // Fallback
    }

    window.rejectionReasons = rejectionReasons;

    function showRejectModal(selectedInvoices) {
        console.log(`📌 Showing rejection modal for Invoices:`, selectedInvoices);

        let reasonOptions = { "": "Select a reason" };
        rejectionReasons.forEach(reason => {
            reasonOptions[reason] = reason;
        });

        Swal.fire({
            title: "Reject Invoice(s)",
            html: `
            <div style="display: flex; flex-direction: column; gap: 10px;">
                <select class="swal2-select">
                    ${Object.entries(reasonOptions).map(([value, text]) =>
                `<option value="${value}">${text}</option>`).join('')}
                </select>
                <textarea class="swal2-textarea" style="display:none;" placeholder="Please provide details for 'Others'..." disabled></textarea>
            </div>
        `,
            showCancelButton: true,
            confirmButtonText: "Reject",
            cancelButtonText: "Close",
            preConfirm: () => {
                const reason = document.querySelector(".swal2-select").value;
                const details = document.querySelector(".swal2-textarea").value.trim();

                if (!reason) {
                    Swal.showValidationMessage("Please select a valid rejection reason");
                    return false;
                }

                if (reason === "Others" && !details) {
                    Swal.showValidationMessage("Please provide rejection details for 'Others'.");
                    return false;
                }

                return { reason, details };
            },
            didOpen: () => {
                const selectElement = document.querySelector(".swal2-select");
                const textareaElement = document.querySelector(".swal2-textarea");

                selectElement.addEventListener('change', function () {
                    textareaElement.style.display = this.value === "Others" ? "block" : "none";
                    textareaElement.disabled = this.value !== "Others";
                });
            }
        }).then((result) => {
            if (result.isConfirmed) {
                console.log(`✅ Rejection confirmed. Reason: ${result.value.reason}, Details: ${result.value.details}`);
                rejectInvoices(selectedInvoices, "rejected", result.value.reason, result.value.details);
            } else {
                console.log("❌ Rejection cancelled by user.");
            }
        });
    }


    // Function to reject invoices
    async function rejectInvoices(selectedInvoices, status, reason, details) {
        console.log("🚀 Reject Invoices Triggered!");
        console.log(`📌 Selected Invoices:`, selectedInvoices);

        if (!selectedInvoices || selectedInvoices.length === 0) {
            console.warn("⚠️ No invoices selected for rejection.");
            return;
        }

        console.log(`🔄 Starting rejection process for ${selectedInvoices.length} invoice(s)...`);
        let successCount = 0;
        let errorMessages = [];

        for (let invoice of selectedInvoices) {
            console.log(`➡️ Sending rejection request for Invoice: ${invoice.invoiceNo} (UUID: ${invoice.uuid})`);

            try {
                // Add timeout to prevent hanging
                const controller = new AbortController();
                const timeoutId = setTimeout(() => controller.abort(), 30000); // 30 second timeout

                let response = await fetch(`/Invoices/InvoiceLists?handler=RejectDocument&documentId=${invoice.uuid}&rejectionReason=${reason}&tin=${invoice.tin}`, {
                    method: "PUT",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
                    },
                    signal: controller.signal
                });

                clearTimeout(timeoutId);

                console.log(`📩 Response received for Invoice: ${invoice.invoiceNo} - Status: ${response.status}`);

                if (!response.ok) {
                    console.error(`❌ Error rejecting Invoice ${invoice.invoiceNo}. Status: ${response.status}`);
                    let errorText = await response.text(); // Get raw text if it's not JSON
                    errorMessages.push("Error", `Failed to reject invoice ${invoice.invoiceNo}.\n${errorText}`, "error");
                    throw new Error(errorText);
                }

                // Try parsing the response as JSON
                let data;
                try {
                    data = await response.json();
                } catch (jsonError) {
                    console.error(`❌ Invalid JSON response for Invoice ${invoice.invoiceNo}:`, jsonError);
                    let errorText = await response.text();
                    errorMessages.push("Error", `Failed to reject invoice ${invoice.invoiceNo}. Invalid JSON response.\n${errorText}`, "error");
                    throw new Error("Invalid JSON response");
                }

                console.log(`✅ Server response for Invoice: ${invoice.invoiceNo}:`, data);

                if (!data) {
                    console.error(`🚨 Server response is null or undefined for Invoice ${invoice.invoiceNo}`);
                    errorMessages.push("Error", `Failed to reject invoice ${invoice.invoiceNo}.\nServer response is empty.`, "error");
                    throw new Error("Server response is empty.");
                }
                else if (!data.message) {
                    console.error(`🚨 Server response does not contain a message for Invoice ${invoice.invoiceNo}:`, data);
                    errorMessages.push("Error", `Failed to reject invoice ${invoice.invoiceNo}.\nServer response is missing message field.`, "error");
                    throw new Error("Server response is missing message field.");
                }
                else if (!data.message.includes("Document rejection successfully processed.")) {
                    console.error(`🚨 Server message did not match expected text for Invoice ${invoice.invoiceNo}:`, data.message);
                    errorMessages.push("Error", `Failed to reject invoice ${invoice.invoiceNo}.\nUnexpected response: ${data.message}`, "error");
                    throw new Error(`Unexpected response: ${data.message}`);
                }
                else {
                    console.log(`✅ Invoice ${invoice.invoiceNo} successfully rejected.`);
                    successCount++;
                }


            }
            catch (error) {
                console.error(`🚨 Request failed for Invoice ${invoice.invoiceNo}:`, error.message);
                
                // Handle specific error types
                if (error.name === 'AbortError') {
                    console.error(`⏰ Request timeout for Invoice ${invoice.invoiceNo} (30 seconds)`);
                    errorMessages.push({
                        invoiceNo: invoice.invoiceNo || "Unknown",
                        message: "Request timeout - the server took too long to respond (30 seconds)"
                    });
                    continue;
                }

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
                    let invoiceNos = Array.isArray(data.invoiceNos) ? data.invoiceNos : [];
                    let invoiceText = invoiceNos.length > 3
                        ? `${invoiceNos.slice(0, 3).join(", ")} and ${invoiceNos.length - 3} more`
                        : invoiceNos.join(", ");

                    return `<div style="background: rgba(255, 77, 79, 0.1); padding: 12px; border-radius: 6px; 
                margin-bottom: 6px; border-left: 4px solid #ff4d4f;">
                <small style="color: #ff4d4f; font-weight: bold;">Invoice(s): ${invoiceText}</small><br>
                <span style="font-size: 13px; color: #555;">${message}</span>
            </div>`;
                }).join("") : "";

            let title = successCount > 0 && errorMessages.length > 0 ? "Partial Success"
                : successCount > 0 ? "Rejected!"
                    : "Invoice(s) could not be rejected";

            let icon = successCount > 0 && errorMessages.length > 0 ? "warning"
                : successCount > 0 ? "success"
                    : "error";

            let message = successCount > 0 && errorMessages.length > 0
                ? `<p>${successCount} invoice(s) have been rejected successfully.</p><hr>
           <p>However, the following invoice(s) failed to reject:</p>${formattedErrors}`
                : successCount > 0
                    ? `${successCount} invoice(s) have been rejected successfully.`
                    : formattedErrors;

            Swal.fire({ title, icon, html: message, confirmButtonText: "OK" })
                .then(() => { if (successCount > 0) location.reload(); });
        }
        console.log("✅ All selected invoices have been processed.");
    }

});