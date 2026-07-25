document.addEventListener("DOMContentLoaded", function () {
    let exportCsvButton = document.getElementById("exportCsvButton");
    let exportXlsxButton = document.getElementById("exportXlsxButton");
    let loadingOverlay = document.getElementById("loadingOverlay");

    function getInvoiceDirection() {
        let selectedRadio = document.querySelector('input[name="invoiceDirection"]:checked');
        return selectedRadio ? selectedRadio.value : "Sent"; // Default to Sent
    }

    if (!exportCsvButton || !exportXlsxButton || !loadingOverlay) {
        console.error("❌ Export buttons or loading overlay not found!");
        return;
    }

    // Export Scope: "Selected Items" exports exactly the checked rows from the list page instead of
    // the date/type/status filters below. Kept in sync whenever the modal opens or a row is (un)checked.
    function selectedInvoiceUuids() {
        return Array.from(document.querySelectorAll(".invoice-checkbox:checked")).map(cb => cb.value).filter(Boolean);
    }

    function isSelectedScope() {
        var radio = document.querySelector('input[name="exportScope"]:checked');
        return radio && radio.value === "selected";
    }

    function refreshExportScopeUI() {
        var countEl = document.getElementById("exportSelectedCount");
        var count = selectedInvoiceUuids().length;
        if (countEl) countEl.textContent = count;
        var selectedScopeRadio = document.getElementById("exportScopeSelected");
        if (selectedScopeRadio) selectedScopeRadio.disabled = count === 0;
        document.querySelectorAll(".export-filter-field").forEach(function (el) {
            el.style.display = isSelectedScope() ? "none" : "";
        });
    }

    var exportModalEl = document.getElementById("exportModal");
    if (exportModalEl) exportModalEl.addEventListener("show.bs.modal", refreshExportScopeUI);
    document.addEventListener("change", function (e) {
        if (e.target && (e.target.classList.contains("invoice-checkbox") || e.target.id === "selectAll" || e.target.name === "exportScope")) {
            refreshExportScopeUI();
        }
    });

    function startLoading() {
        console.log("✅ Showing loading overlay");
        loadingOverlay.style.display = "flex"; // Ensure it's visible
        document.querySelectorAll("button, a, input, select, textarea").forEach(el => {
            el.disabled = true;
        });
    }

    function stopLoading() {
        console.log("✅ Hiding loading overlay");
        loadingOverlay.style.display = "none";
        document.querySelectorAll("button, a, input, select, textarea").forEach(el => {
            el.disabled = false;
        });
    }
    function handleExport(fileType) {

        startLoading(); //Loading window

        // Export Scope = "Selected Items": skip the date/type/status filters entirely and export
        // exactly the checked rows. Falls through to the same fetch/blob download logic below as
        // Current View, just with a different exportUrl.
        if (isSelectedScope()) {
            let uuids = selectedInvoiceUuids();
            if (uuids.length === 0) {
                alert("No invoices selected. Check at least one row, or switch to Current View.");
                stopLoading();
                return;
            }
            let invoiceDirection = getInvoiceDirection();
            let exportUrl = `/Invoices/InvoiceLists?handler=Export&fileType=${fileType}&invoiceDirection=${encodeURIComponent(invoiceDirection)}&uuids=${encodeURIComponent(uuids.join(','))}`;
            downloadExport(exportUrl, fileType);
            return;
        }

        // Get selected values
        let documentTypeDropdown = document.getElementById("documentTypeExp");
        let documentTypeText = documentTypeDropdown.options[documentTypeDropdown.selectedIndex].text.trim();
        let submissionDateFrom = document.getElementById("submissionDateFromExp")?.value;
        let submissionDateTo = document.getElementById("submissionDateToExp")?.value;
        let internalStatusId = document.getElementById("internalStatusIdExp").value;
        let invoiceDirection = getInvoiceDirection();

        // Map document type text to corresponding code
        const documentTypeMap = {
            "Invoice": "01",
            "Credit Note": "02",
            "Debit Note": "03",
            "Refund Note": "04",
            "Self-billed Invoice": "11",
            "Self-billed Credit Note": "12",
            "Self-billed Debit Note": "13",
            "Self-billed Refund Note": "14"
        };

        // Assign mapped value or empty string if "All"
        let documentType = documentTypeMap[documentTypeText] || "";

        // Handle "All" selection
        if (internalStatusId === "") internalStatusId = "";

        // Validate required inputs
        if (!submissionDateFrom || !submissionDateTo) {
            alert("Please select a valid date range.");
            stopLoading();
            return;
        }

        let fromDate = new Date(submissionDateFrom);
        let toDate = new Date(submissionDateTo);

        if (isNaN(fromDate) || isNaN(toDate)) {
            alert("Invalid date format. Please re-select the dates.");
            stopLoading();
            return;
        }

        if (fromDate > toDate) {
            alert("Submission Start Date cannot be later than Submission End Date.");
            stopLoading();
            return;
        }

        // ✅ Debug: Log the selected invoice direction before sending request
        console.log("📌 Selected Invoice Direction:", invoiceDirection);

        // Construct query parameters dynamically
        //let exportUrl = `/Invoices/InvoiceLists?handler=Export&fileType=${fileType}`;
        let exportUrl = `/Invoices/InvoiceLists?handler=Export&fileType=${fileType}&invoiceDirection=${encodeURIComponent(invoiceDirection)}`;

        if (documentType) exportUrl += `&documentType=${documentType}`;
        if (submissionDateFrom) exportUrl += `&submissionDateFrom=${submissionDateFrom}`;
        if (submissionDateTo) exportUrl += `&submissionDateTo=${submissionDateTo}`;
        if (internalStatusId) exportUrl += `&internalStatusId=${internalStatusId}`;

        downloadExport(exportUrl, fileType);
    }

    // Shared fetch/blob download used by both Export Scopes (Current View above, Selected Items in
    // handleExport's early-return branch) — same UTF-8 BOM fix for CSV, same loading-overlay lifecycle.
    function downloadExport(exportUrl, fileType) {
        if (fileType == "csv") {
            fetch(exportUrl, { method: "GET" })
                .then(response => {
                    if (!response.ok) throw new Error("Failed to export invoices.");
                    return response.blob();
                })
                .then(blob => {
                    return blob.text(); // ✅ Read the blob as text
                })
                .then(text => {
                    // ✅ Add UTF-8 BOM to fix special character issues
                    let bom = "\uFEFF";
                    let utf8Blob = new Blob([bom + text], { type: "text/csv;charset=utf-8" });
                    let url = window.URL.createObjectURL(utf8Blob);
                    let a = document.createElement("a");
                    a.href = url;

                    // ✅ Generate timestamp: ddMMyyyy_HHmmss
                    let now = new Date();
                    let day = String(now.getDate()).padStart(2, '0');
                    let month = String(now.getMonth() + 1).padStart(2, '0'); // Months are 0-based
                    let year = now.getFullYear();
                    let hours = String(now.getHours()).padStart(2, '0');
                    let minutes = String(now.getMinutes()).padStart(2, '0');
                    let seconds = String(now.getSeconds()).padStart(2, '0');

                    let timestamp = `${day}${month}${year}_${hours}${minutes}${seconds}`; // Example output: 27022025_143045 (for 27th Feb 2025, 14:30:45)
                    let filename = `Invoices_${timestamp}.${fileType}`;

                    a.download = filename;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    window.URL.revokeObjectURL(url);
                })
                .catch(error => {
                    console.error("Export error:", error);
                    alert("Failed to export. Please try again.");
                })
                .finally(() => {
                    stopLoading(); // ✅ Stop loading ONLY when done
                });
        }
        else if (fileType == "xlsx") {
            fetch(exportUrl, { method: "GET" })
                .then(response => {
                    if (!response.ok) throw new Error("Failed to export invoices.");
                    return response.blob();
                })
                .then(blob => {
                    let url = window.URL.createObjectURL(blob);
                    let a = document.createElement("a");
                    a.href = url;

                    // ✅ Generate timestamp: ddMMyyyy_HHmmss
                    let now = new Date();
                    let day = String(now.getDate()).padStart(2, '0');
                    let month = String(now.getMonth() + 1).padStart(2, '0'); // Months are 0-based
                    let year = now.getFullYear();
                    let hours = String(now.getHours()).padStart(2, '0');
                    let minutes = String(now.getMinutes()).padStart(2, '0');
                    let seconds = String(now.getSeconds()).padStart(2, '0');

                    let timestamp = `${day}${month}${year}_${hours}${minutes}${seconds}`; // Example output: 27022025_143045 (for 27th Feb 2025, 14:30:45)
                    let filename = `Invoices_${timestamp}.${fileType}`;

                    a.download = filename;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    window.URL.revokeObjectURL(url);
                })
                .catch(error => {
                    console.error("Export error:", error);
                    alert("Failed to export. Please try again.");
                })
                .finally(() => {
                    stopLoading(); // ✅ Stop loading ONLY when done
                });
        }




    }

    exportCsvButton.addEventListener("click", function () {
        handleExport("csv");
    });

    exportXlsxButton.addEventListener("click", function () {
        handleExport("xlsx");
    });

    // PDF (ZIP) only makes sense for an explicit, bounded selection — the server caps this at 50
    // invoices and generates on demand, which is fine for a deliberate small batch but would be a
    // real timeout risk for "every invoice matching the current filters."
    var exportPdfZipButton = document.getElementById("exportPdfZipButton");
    if (exportPdfZipButton) {
        exportPdfZipButton.addEventListener("click", function () {
            if (!isSelectedScope()) {
                Swal.fire("Selected Items Only", "Switch Export Scope to \"Selected Items\" to export PDFs — this avoids generating hundreds of PDFs in one request.", "info");
                return;
            }
            let uuids = selectedInvoiceUuids();
            if (uuids.length === 0) {
                Swal.fire("No Selection", "Please select at least one invoice.", "warning");
                return;
            }
            if (uuids.length > 50) {
                Swal.fire("Too Many Selected", "Select at most 50 invoices for a PDF export.", "warning");
                return;
            }
            let invoiceDirection = getInvoiceDirection();
            let url = `/Invoices/InvoiceLists?handler=BulkPdf&invoiceDirection=${encodeURIComponent(invoiceDirection)}&uuids=${encodeURIComponent(uuids.join(','))}`;
            window.location.href = url;
        });
    }

    // ✅ Preselect the radio button based on the selected tab
    function setDefaultInvoiceDirection() {
        let invoiceDirection = new URLSearchParams(window.location.search).get("invoiceDirection") || "Sent";
        let sentOption = document.getElementById("sentOption");
        let receivedOption = document.getElementById("receivedOption");

        if (sentOption && receivedOption) {
            if (invoiceDirection === "Sent") {
                sentOption.checked = true;
            } else {
                receivedOption.checked = true;
            }
        }
    }

    // ✅ Set default invoice direction when modal is opened
    document.getElementById("exportModal").addEventListener("show.bs.modal", setDefaultInvoiceDirection);

});
