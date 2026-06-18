// 📦 File: wwwroot/js/invoice-submit-core.js
// 📦 File: wwwroot/js/invoice-submit-core.js

let isSubmitting = false;

export async function handleSubmit(submitButton) {
    if (!submitButton || isSubmitting) return;

    isSubmitting = true;
    submitButton.disabled = true;
    const originalText = submitButton.innerHTML;
    submitButton.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span> Submitting...`;

    try {
        const result = await Swal.fire({
            title: "Submit Invoice?",
            text: "Are you sure you want to submit this invoice to LHDN?",
            icon: "warning",
            showCancelButton: true,
            confirmButtonText: "Yes, Submit!",
            cancelButtonText: "No, Cancel",
            customClass: {
                confirmButton: "btn btn-primary w-xs me-2 mt-2",
                cancelButton: "btn btn-danger w-xs mt-2"
            },
            buttonsStyling: false,
            showCloseButton: true,
        });

        if (!result.isConfirmed) throw "Cancelled";

        // ✅ Show loading indicator after confirmation (Notice there is NO 'await' here)
        Swal.fire({
            title: "Submitting...",
            html: "Your invoice is being sent to LHDN. Please wait.",
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        const invoiceNo = document.getElementById("invoiceNo")?.value;
        if (!invoiceNo) throw new Error("Invoice number is missing.");

        const formData = new FormData();
        formData.append("invoiceNo", invoiceNo);
        formData.append("isAjax", "true");

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) formData.append("__RequestVerificationToken", token);

        const res = await fetch(`/Invoices/CreateInvoice?handler=SubmitDocuments`, {
            method: "POST",
            body: formData
        });

        if (!res.ok) throw new Error(await res.text());

        const json = await res.json();
        console.log("📨 LHDN Submission Response:", json);

        if (json.success) {
            const currentDateTime = new Date().toLocaleString('en-MY', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit',
                hour12: false
            });

            // This new Swal.fire will automatically replace the loading spinner
            Swal.fire({
                title: "✅ Successfully Submitted to LHDN!",
                html: `
                    <div class="text-start">
                        <div class="alert alert-success mb-3">
                            <i class="fas fa-check-circle me-2"></i>
                            Your invoice has been successfully submitted to LHDN (Lembaga Hasil Dalam Negeri Malaysia).
                        </div>
                        
                        <div class="row">
                            <div class="col-12 mb-2">
                                <strong><i class="fas fa-receipt me-2"></i>Invoice Number:</strong> 
                                <span class="text-primary">${invoiceNo}</span>
                            </div>
                            
                            <div class="col-12 mb-2">
                                <strong><i class="fas fa-fingerprint me-2"></i>Document UUID:</strong>
                                <div class="input-group input-group-sm mt-1">
                                    <input type="text" class="form-control" id="uuidText" value="${json.uuid || "N/A"}" readonly>
                                    <button onclick="copyUUID()" class="btn btn-outline-primary" type="button">
                                        <i class="fas fa-copy"></i> Copy
                                    </button>
                                </div>
                            </div>
                            
                            <div class="col-12 mb-2">
                                <strong><i class="fas fa-paper-plane me-2"></i>Submission UID:</strong> 
                                <span class="text-info">${json.submissionUid || "N/A"}</span>
                            </div>
                            
                            <div class="col-12 mb-2">
                                <strong><i class="fas fa-clock me-2"></i>Submitted On:</strong> 
                                <span class="text-muted">${currentDateTime}</span>
                            </div>
                        </div>
                        
                        <div class="alert alert-info mt-3 mb-0">
                            <small><i class="fas fa-info-circle me-2"></i>You will be redirected to the invoice list in a few seconds...</small>
                        </div>
                    </div>
                `,
                icon: "success",
                timer: 6000,
                timerProgressBar: true,
                showConfirmButton: false,
                width: '500px',
                willClose: () => {
                    window.location.href = `/Invoices/InvoiceLists?refresh=true&invoiceDirection=Sent&timestamp=${Date.now()}`;
                }
            });
        } else {
            throw new Error(json.message || "Submission failed.");
        }

    } catch (err) {
        if (err !== "Cancelled") {
            console.error("❌ Submission error:", err);
            Swal.fire("Error!", err.message || err, "error");
        }
    } finally {
        isSubmitting = false;
        submitButton.innerHTML = originalText;
        submitButton.disabled = false;
    }
}

// Ensure copyUUID is attached to the window so it can be called from the inline HTML onclick
window.copyUUID = function () {
    const uuidInput = document.getElementById("uuidText");
    const uuid = uuidInput?.value || uuidInput?.innerText;

    if (!uuid || uuid === "N/A") {
        Swal.fire({
            title: "Error!",
            text: "UUID not found or invalid.",
            icon: "error",
            timer: 2000,
            showConfirmButton: false
        });
        return;
    }

    navigator.clipboard.writeText(uuid).then(() => {
        // We use a small toast here so we don't accidentally close the main success popup
        Swal.fire({
            toast: true,
            position: 'top-end',
            title: "Copied!",
            text: "UUID copied to clipboard.",
            icon: "success",
            timer: 2000,
            showConfirmButton: false
        });
    }).catch(err => {
        console.error("Clipboard error:", err);
        uuidInput.select();
        document.execCommand('copy');
        Swal.fire({
            toast: true,
            position: 'top-end',
            title: "Copied!",
            text: "UUID copied to clipboard.",
            icon: "success",
            timer: 2000,
            showConfirmButton: false
        });
    });
}