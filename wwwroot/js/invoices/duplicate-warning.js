// Possible-duplicate warning after Save Draft on Create e-Invoice and Edit Invoice. The server only warns (the draft is
// already saved): the user can open the existing invoice or simply carry on. Text only, never HTML from the server.
(function () {
    'use strict';

    // Returns true when a warning was shown (so the caller skips its own success toast).
    function show(response) {
        const warnings = (response && response.warnings) || [];
        if (!warnings.length || typeof Swal === 'undefined') return false;

        const body = document.createElement('div');
        body.className = 'text-start';
        const intro = document.createElement('p');
        intro.textContent = `Draft ${response.invoiceNo || ''} was saved. It looks like an invoice you already have:`;
        body.appendChild(intro);
        const list = document.createElement('ul');
        list.className = 'small ps-3';
        warnings.forEach((text, i) => {
            const li = document.createElement('li');
            li.textContent = text + ' ';
            const dup = (response.duplicates || [])[i];
            if (dup && dup.invoiceNo) {
                const link = document.createElement('a');
                link.href = dup.uuid ? `/Invoices/InvoiceDetails2/${encodeURIComponent(dup.uuid)}` : `/Invoices/InvoiceEdit/${encodeURIComponent(dup.invoiceNo)}`;
                link.target = '_blank';
                link.rel = 'noopener';
                link.textContent = 'View existing invoice';
                li.appendChild(link);
            }
            list.appendChild(li);
        });
        body.appendChild(list);

        Swal.fire({
            icon: 'warning',
            title: 'Possible duplicate invoice',
            html: body,
            confirmButtonText: 'Continue anyway',
            confirmButtonColor: '#006948'
        });
        return true;
    }

    window.EinvDuplicateWarning = { show };
})();
