// bulk-check-ready.js
//
// "Check selected" on the Draft tab: runs the server's Ready-for-LHDN check (handler=BulkReadiness, the same check
// Submit runs) on the selected drafts, then offers to submit only the ready ones through the existing bulk Submit
// flow (#bulkSubmitBtn), which checks each draft again on the server. Messages are shown as text, never HTML.

document.addEventListener("click", async function (event) {
    const btn = event.target.closest("#bulkCheckReadyBtn");
    if (!btn) return;

    const boxes = Array.from(document.querySelectorAll(".invoice-checkbox:checked")).filter(cb => cb.dataset.invoiceno);
    if (boxes.length === 0) {
        Swal.fire({ title: "No Selection", text: "Select at least one draft to check.", icon: "info" });
        return;
    }
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    Swal.fire({ title: "Checking drafts…", text: `Checking ${boxes.length} draft(s).`, allowOutsideClick: false, didOpen: () => Swal.showLoading() });
    let results;
    try {
        const response = await fetch("/Invoices/InvoiceLists?handler=BulkReadiness", {
            method: "POST",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
            body: JSON.stringify(boxes.map(cb => cb.dataset.invoiceno))
        });
        results = (await response.json()).results || [];
    } catch (error) {
        console.error("Check selected failed:", error);
        Swal.fire({ title: "Could Not Check", text: "A network error occurred. Please try again.", icon: "error" });
        return;
    }

    const ready = results.filter(r => r.ready);
    const notReady = results.filter(r => !r.ready);

    const body = document.createElement("div");
    body.className = "text-start";
    const summary = document.createElement("p");
    summary.className = "fw-bold";
    summary.textContent = `Ready: ${ready.length} · Needs attention: ${notReady.length}`;
    body.appendChild(summary);
    if (notReady.length) {
        const list = document.createElement("ul");
        list.className = "small ps-3 mb-0";
        notReady.forEach(r => {
            const li = document.createElement("li");
            const link = document.createElement("a");
            link.href = `/Invoices/InvoiceEdit/${encodeURIComponent(r.invoiceNo)}`;
            link.textContent = r.invoiceNo;
            li.appendChild(link);
            li.appendChild(document.createTextNode(`: ${r.issue || "Not ready for LHDN."}`));
            list.appendChild(li);
        });
        body.appendChild(list);
    }

    const choice = await Swal.fire({
        title: "Ready for LHDN?",
        html: body,
        icon: notReady.length ? "warning" : "success",
        showCancelButton: ready.length > 0,
        confirmButtonText: ready.length > 0 ? `Submit the ${ready.length} ready` : "OK",
        cancelButtonText: "Close"
    });
    if (!choice.isConfirmed || ready.length === 0) return;

    // Leave only the ready drafts selected, then hand over to the normal bulk Submit.
    const readyNos = new Set(ready.map(r => r.invoiceNo));
    boxes.forEach(cb => {
        if (!readyNos.has(cb.dataset.invoiceno)) {
            cb.checked = false;
            cb.dispatchEvent(new Event("change", { bubbles: true }));
        }
    });
    document.getElementById("bulkSubmitBtn")?.click();
});
