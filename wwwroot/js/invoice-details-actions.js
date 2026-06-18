document.addEventListener("DOMContentLoaded", function () {
    console.log("📌 InvoiceDetails2 Actions - DOM fully loaded and ready!");

    // Reject Invoice button functionality - InvoiceDetails2
    document.getElementById("rejectInvoiceBtn")?.addEventListener("click", function () {
        let uuid = this.dataset.uuid;
        let invoiceNo = this.dataset.invoiceno;
        let tin = this.dataset.tin;
        console.log(`📌 Reject Invoice clicked: ${invoiceNo}, UUID: ${uuid}, TIN: ${tin}`);
        showRejectModal([{ uuid, invoiceNo, tin }]);
    });

    // Cancel Invoice button functionality - InvoiceDetails2
    document.getElementById("cancelInvoiceBtn")?.addEventListener("click", function () {
        let uuid = this.dataset.uuid;
        let invoiceNo = this.dataset.invoiceno;
        let tin = this.dataset.tin;
        console.log(`📌 Cancel Invoice clicked: ${invoiceNo}, UUID: ${uuid}, TIN: ${tin}`);
        showCancelModal([{ uuid, invoiceNo, tin }]);
    });

    // Load rejection reasons
    let rejectionReasons = [];
    try {
        let reasonsJson = document.getElementById("rejectionReasons")?.value || "[]";
        rejectionReasons = JSON.parse(reasonsJson);
        console.log("🚀 Loaded rejection reasons:", rejectionReasons);
    } catch (error) {
        console.error("❌ Error parsing rejection reasons:", error);
        rejectionReasons = ["Wrong supplier details", "Wrong buyer details", "Wrong invoice details", "Others"];
    }

    // Show rejection modal
    function showRejectModal(selectedInvoices) {
        console.log(`📌 Showing rejection modal for Invoices:`, selectedInvoices);

        let reasonOptions = { "": "Select a reason" };
        rejectionReasons.forEach(reason => {
            reasonOptions[reason] = reason;
        });

        Swal.fire({
            title: "Reject Invoice",
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
                rejectInvoice(selectedInvoices[0], result.value.reason, result.value.details);
            } else {
                console.log("❌ Rejection cancelled by user.");
            }
        });
    }

    // Load cancellation reasons
    let cancellationReasons = [];
    try {
        let reasonsJson = document.getElementById("rejectionReasons")?.value || "[]";
        cancellationReasons = JSON.parse(reasonsJson);
        console.log("🚀 Loaded cancellation reasons:", cancellationReasons);
    } catch (error) {
        console.error("❌ Error parsing cancellation reasons:", error);
        cancellationReasons = ["Duplicate invoice", "Wrong details", "Customer request", "Others"];
    }

    // Show cancellation modal
    function showCancelModal(selectedInvoices) {
        console.log(`📌 Showing cancel modal for invoices:`, selectedInvoices);

        let reasonOptions = { "": "Select a reason" };
        cancellationReasons.forEach(reason => {
            reasonOptions[reason] = reason;
        });

        Swal.fire({
            title: "Cancel Invoice",
            html: `
            <div style="display: flex; flex-direction: column; gap: 10px;">
                <select class="swal2-select">
                    ${Object.entries(reasonOptions).map(([value, text]) =>
                `<option value="${value}">${text}</option>`).join('')}
                </select>
                <textarea class="swal2-textarea" style="display:none;" placeholder="Please provide details for 'Others'..." disabled></textarea>
            </div>`,
            showCancelButton: true,
            confirmButtonText: "Cancel Invoice",
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
                console.log(`✅ Cancellation confirmed. Reason: ${result.value.reason}, Details: ${result.value.details}`);
                cancelInvoice(selectedInvoices[0], result.value.reason, result.value.details);
            } else {
                console.log("❌ Cancellation cancelled by user.");
            }
        });
    }

    // Reject single invoice
    async function rejectInvoice(invoice, reason, details) {
        console.log("🚀 Reject Invoice Triggered for InvoiceDetails2!");
        console.log(`📌 Invoice:`, invoice);

        try {
            // Show loading
            Swal.fire({
                title: 'Processing...',
                html: 'Please wait while we reject the invoice.',
                allowOutsideClick: false,
                didOpen: () => {
                    Swal.showLoading();
                }
            });

            let response = await fetch(`?handler=RejectDocument&documentId=${invoice.uuid}&rejectionReason=${reason}&tin=${invoice.tin}`, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
                }
            });

            console.log(`📩 Response received - Status: ${response.status}`);

            if (!response.ok) {
                console.error(`❌ Error rejecting Invoice ${invoice.invoiceNo}. Status: ${response.status}`);
                let errorText = await response.text();
                throw new Error(errorText);
            }

            let data = await response.json();
            console.log(`✅ Server response:`, data);

            if (data && data.message && data.message.includes("Document rejection successfully processed.")) {
                // Direct operation success
                Swal.fire({
                    title: "Success!",
                    text: "Invoice has been rejected successfully.",
                    icon: "success",
                    confirmButtonText: "OK"
                }).then(() => {
                    // Refresh the current page to show updated status
                    window.location.reload();
                });
            } else {
                throw new Error(data.message || "Unexpected response from server");
            }

        } catch (error) {
            console.error(`🚨 Request failed:`, error.message);
            Swal.fire({
                title: "Error",
                text: `Failed to reject invoice: ${error.message}`,
                icon: "error",
                confirmButtonText: "OK"
            });
        }
    }

    // Cancel single invoice
    async function cancelInvoice(invoice, reason, details) {
        console.log("🚀 Cancel Invoice Triggered for InvoiceDetails2!");
        console.log(`📌 Invoice:`, invoice);

        try {
            // Show loading
            Swal.fire({
                title: 'Processing...',
                html: 'Please wait while we cancel the invoice.',
                allowOutsideClick: false,
                didOpen: () => {
                    Swal.showLoading();
                }
            });

            let response = await fetch(`?handler=CancelDocument&documentId=${invoice.uuid}&cancellationReason=${reason}&tin=${invoice.tin}`, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
                }
            });

            console.log(`📩 Response received - Status: ${response.status}`);

            if (!response.ok) {
                console.error(`❌ Error cancelling Invoice ${invoice.invoiceNo}. Status: ${response.status}`);
                let errorText = await response.text();
                throw new Error(errorText);
            }

            let data = await response.json();
            console.log(`✅ Server response:`, data);

            if (data && data.message && data.message.includes("Document cancellation successfully processed.")) {
                // Direct operation success
                Swal.fire({
                    title: "Success!",
                    text: "Invoice has been cancelled successfully.",
                    icon: "success",
                    confirmButtonText: "OK"
                }).then(() => {
                    // Refresh the current page to show updated status
                    window.location.reload();
                });
            } else {
                throw new Error(data.message || "Unexpected response from server");
            }

        } catch (error) {
            console.error(`🚨 Request failed:`, error.message);
            Swal.fire({
                title: "Error",
                text: `Failed to cancel invoice: ${error.message}`,
                icon: "error",
                confirmButtonText: "OK"
            });
        }
    }

});