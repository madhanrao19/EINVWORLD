document.addEventListener("DOMContentLoaded", function () {
    // Every value interpolated into the HTML strings below (company/staff names, invoice numbers,
    // LHDN reasons, the FilterDate query param) is user- or server-supplied: escape it.
    const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // Chart font and text/grid colours come from the design tokens (einvworld-tokens.css), so they
    // follow the active theme. Fallbacks keep charts readable if the token sheet failed to load.
    const rootStyle = getComputedStyle(document.documentElement);
    const token = (name, fallback) => rootStyle.getPropertyValue(name).trim() || fallback;
    const chartFont = token('--einv-font-sans', 'sans-serif');
    const chartGrid = token('--einv-chart-grid', '#f3f4f6');
    if (window.Chart) {
        Chart.defaults.font.family = chartFont;
        Chart.defaults.color = token('--einv-text-muted', '#6b7280');
        Chart.defaults.borderColor = chartGrid;
        Chart.defaults.elements.arc.borderColor = token('--einv-surface', '#ffffff'); // doughnut slice gaps match the card
    }

    const urlParams = new URLSearchParams(window.location.search);

    // --- 1. Filter Persistence ---
    if (urlParams.has('FilterType')) {
        sessionStorage.setItem('dash_FilterType', urlParams.get('FilterType'));
        sessionStorage.setItem('dash_FilterDate', urlParams.get('FilterDate') || ''); // never the string "null"
    } else if (sessionStorage.getItem('dash_FilterType')) {
        const savedType = sessionStorage.getItem('dash_FilterType');
        const savedDate = sessionStorage.getItem('dash_FilterDate');
        window.location.replace(window.location.pathname + `?FilterType=${encodeURIComponent(savedType)}&FilterDate=${encodeURIComponent(savedDate && savedDate !== 'null' ? savedDate : '')}`);
        return;
    }

    // --- 2. Filter Input Mode Logic & UI Enforcement ---
    const filterType = document.getElementById('filterType');
    const filterDate = document.getElementById('filterDate');
    const filterYear = document.getElementById('filterYear');

    function updateInputMode() {
        if (!filterType || !filterDate || !filterYear) return;

        const mode = filterType.value;
        const defaultDateFull = filterDate.getAttribute('data-default-date') || new Date().toISOString().split('T')[0];
        // The shared calendar (einvworld-date.js) wraps this input; rebuild it for the new mode.
        if (filterDate._flatpickr) { filterDate._flatpickr.destroy(); filterDate._flatpickr = undefined; } // destroy() leaves the reference

        if (mode === 'Year') {
            filterDate.style.display = 'none';
            filterDate.removeAttribute('name');
            filterYear.style.display = 'block';
            filterYear.setAttribute('name', 'FilterDate');
        } else {
            filterYear.style.display = 'none';
            filterYear.removeAttribute('name');
            filterDate.style.display = '';
            filterDate.setAttribute('name', 'FilterDate');

            if (mode === 'Month') {
                filterDate.type = 'month';
                filterDate.value = defaultDateFull.substring(0, 7);
            } else if (mode === 'Day') {
                filterDate.type = 'date';
                filterDate.value = defaultDateFull;
            }
            if (window.einvworld && window.einvworld.initDates) window.einvworld.initDates(filterDate.parentNode);
        }
    }

    if (filterType) {
        filterType.addEventListener('change', updateInputMode);
        updateInputMode();
    }

    // --- 3. Chart Rendering & Empty State Logic ---
    let monthlyChartInstance = null;
    // Brand-anchored categorical palette (EinvWorld Professional): compliance green first, then the
    // semantic accents, kept distinguishable for up to 8 series (the 8 LHDN document types).
    const modernColors = [token('--einv-fill', '#006948'), '#0ea5e9', '#f59e0b', '#ef4444', '#00855d', '#8b5cf6', '#14b8a6', '#6b7280'];

    // Status → the same colours as the status badges and dots (einvworld-tokens.css), keyed by name so
    // donut segments stay correct however the API orders them.
    const statusTone = name => token('--einv-status-' + name + '-fg', '#6b7280');
    const statusColors = {
        'Valid': statusTone('valid'), 'Submitted': statusTone('submitted'), 'Draft': statusTone('draft'), 'Pending': statusTone('draft'),
        'Rejected': statusTone('rejected'), 'RequestReject': statusTone('rejected'), 'Invalid': statusTone('invalid'), 'Cancelled': statusTone('cancelled')
    };
    const tooltipBg = token('--einv-text', '#14201a');

    const doughnutOptions = {
        responsive: true, maintainAspectRatio: false, cutout: '78%',
        plugins: {
            legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', padding: 20, font: { size: 12, family: chartFont } } },
            tooltip: { backgroundColor: tooltipBg, padding: 10, cornerRadius: 8 }
        }
    };

    // 🎨 UI HELPER: Handles Empty Chart States Beautifully
    function handleEmptyCanvas(canvasId, hasData, customMessage = "No data available for this period.") {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const container = canvas.parentElement;
        let emptyDiv = container.querySelector('.empty-chart-state');

        if (!hasData) {
            canvas.style.display = 'none'; // Hide the empty canvas
            if (!emptyDiv) {
                emptyDiv = document.createElement('div');
                emptyDiv.className = 'empty-chart-state einv-empty h-100 w-100';
                emptyDiv.innerHTML = `
                    <span class="einv-empty-icon" aria-hidden="true"><i class="ri-bar-chart-2-line"></i></span>
                    <p class="einv-empty-title">No data for this period</p>
                    <p class="einv-empty-text">${esc(customMessage)}</p>
                `;
                container.appendChild(emptyDiv);
            }
            emptyDiv.style.display = 'flex';
        } else {
            canvas.style.display = 'block'; // Show canvas
            if (emptyDiv) {
                emptyDiv.style.display = 'none'; // Hide empty message
            }
        }
    }

    const currentFilterType = urlParams.get('FilterType') || sessionStorage.getItem('dash_FilterType') || 'Month';
    const savedDate = sessionStorage.getItem('dash_FilterDate');
    const currentFilterDate = urlParams.get('FilterDate') || (savedDate && savedDate !== 'null' ? savedDate : '') || new Date().toISOString().split('T')[0];

    fetch(`/Dashboard/Dashboard?handler=InvoiceData&FilterType=${encodeURIComponent(currentFilterType)}&FilterDate=${encodeURIComponent(currentFilterDate)}`)
        .then(response => response.json())
        .then(data => {
            if (data.error) return;

            // 📊 STATUS BREAKDOWN 
            const statusCanvas = document.getElementById("statusChart");
            if (statusCanvas && data.statusCounts) {
                const hasStatusData = data.statusCounts.some(item => item.count > 0);
                handleEmptyCanvas("statusChart", hasStatusData, "No invoices found for this period.");

                if (hasStatusData) {
                    new Chart(statusCanvas, {
                        type: "doughnut",
                        data: {
                            labels: data.statusCounts.map(item => item.status),
                            datasets: [{
                                data: data.statusCounts.map(item => item.count),
                                backgroundColor: data.statusCounts.map((item, i) => statusColors[item.status] || modernColors[i % modernColors.length])
                            }]
                        },
                        options: doughnutOptions
                    });
                }
            }

            // 📊 CUSTOMER SHARE (REVENUE BASED - DOUGHNUT)
            const customerCanvas = document.getElementById("customerChart");
            if (customerCanvas && data.invoicesByCustomer) {
                const hasCustomerData = data.invoicesByCustomer.some(item => item.totalAmount > 0);
                handleEmptyCanvas("customerChart", hasCustomerData, "No revenue generated in this period.");

                if (hasCustomerData) {
                    new Chart(customerCanvas, {
                        type: "doughnut",
                        data: {
                            labels: data.invoicesByCustomer.map(item => item.customerName || "Unknown"),
                            datasets: [{
                                data: data.invoicesByCustomer.map(item => item.totalAmount || 0),
                                backgroundColor: modernColors
                            }]
                        },
                        options: {
                            responsive: true, maintainAspectRatio: false, cutout: '78%',
                            plugins: {
                                legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', padding: 15, font: { size: 11, family: chartFont } } },
                                tooltip: {
                                    backgroundColor: tooltipBg, padding: 12, cornerRadius: 8,
                                    callbacks: {
                                        label: function (context) {
                                            let label = context.label || '';
                                            if (label) label += ': ';
                                            if (context.parsed !== null) {
                                                label += 'RM ' + context.parsed.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                                            }
                                            return label;
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            }

            // 📊 BUYER: TOP SUPPLIERS BY SPEND (DOUGHNUT)
            const buyerSupplierCanvas = document.getElementById("buyerTopSuppliersChart");
            if (buyerSupplierCanvas && data.supplierSpendShare) {
                const hasSupplierData = data.supplierSpendShare.some(item => item.totalAmount > 0);
                handleEmptyCanvas("buyerTopSuppliersChart", hasSupplierData, "No supplier spend recorded in this period.");

                if (hasSupplierData) {
                    new Chart(buyerSupplierCanvas, {
                        type: "doughnut",
                        data: {
                            labels: data.supplierSpendShare.map(item => item.supplierName),
                            datasets: [{
                                data: data.supplierSpendShare.map(item => item.totalAmount),
                                backgroundColor: modernColors
                            }]
                        },
                        options: {
                            responsive: true, maintainAspectRatio: false, cutout: '78%',
                            plugins: {
                                legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', padding: 15, font: { size: 11, family: chartFont } } },
                                tooltip: {
                                    backgroundColor: tooltipBg, padding: 12, cornerRadius: 8,
                                    callbacks: {
                                        label: function (context) {
                                            let label = context.label || '';
                                            if (label) label += ': ';
                                            if (context.parsed !== null) {
                                                label += 'RM ' + context.parsed.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                                            }
                                            return label;
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            }

            // 📊 INVOICE TYPES 
            const typesCanvas = document.getElementById("invoiceTypesChart");
            if (typesCanvas && data.invoiceType) {
                const hasTypesData = data.invoiceType.some(item => item.count > 0);
                handleEmptyCanvas("invoiceTypesChart", hasTypesData, "No specific document types found.");

                if (hasTypesData) {
                    new Chart(typesCanvas, {
                        type: "doughnut",
                        data: {
                            labels: data.invoiceType.map(item => item.type),
                            datasets: [{
                                data: data.invoiceType.map(item => item.count),
                                backgroundColor: modernColors
                            }]
                        },
                        options: doughnutOptions
                    });
                }
            }

            // 📊 INVOICE TRENDS (WITH ZOOM/SHRINK FEATURE) 
            const rawData = data.monthlyInvoiceTypes;
            const monthLabels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
            const latestYear = currentFilterDate.split('-')[0] || new Date().getFullYear().toString();

            function updateInvoiceChart(selectedYear) {
                const filteredData = rawData
                    .filter(item => item.months.startsWith(selectedYear))
                    .map(item => ({ ...item, months: monthLabels[parseInt(item.months.split("-")[1], 10) - 1] }));

                const datasets = Object.entries(data.docTypeNames || {}).map(([key, type], idx) => ({
                    label: type,
                    data: monthLabels.map(month => {
                        const found = filteredData.find(item => item.months === month && item.type === type);
                        return found ? found.count : 0;
                    }),
                    backgroundColor: modernColors[idx % modernColors.length],
                    borderRadius: 6,
                    barPercentage: 0.6
                }));

                const hasTrendData = datasets.some(ds => ds.data.some(val => val > 0));
                const monthlyCanvas = document.getElementById("invoiceTypesMonthlyChart");

                if (monthlyCanvas) {
                    // Create a beautiful, human-readable date string
                    let displayDateText = "this period";
                    if (currentFilterDate) {
                        const dateParts = currentFilterDate.split('-');
                        const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

                        if (currentFilterType === 'Year' && dateParts.length > 0) {
                            displayDateText = dateParts[0];
                        } else if (currentFilterType === 'Month' && dateParts.length > 1) {
                            displayDateText = `${monthNames[parseInt(dateParts[1], 10) - 1]} ${dateParts[0]}`;
                        } else if (currentFilterType === 'Day' && dateParts.length > 2) {
                            displayDateText = `${parseInt(dateParts[2], 10)} ${monthNames[parseInt(dateParts[1], 10) - 1]} ${dateParts[0]}`;
                        } else {
                            displayDateText = currentFilterDate;
                        }
                    }

                    handleEmptyCanvas("invoiceTypesMonthlyChart", hasTrendData, `No invoice trends to display for ${displayDateText}.`);

                    if (hasTrendData) {
                        if (monthlyChartInstance) monthlyChartInstance.destroy();
                        const ctx = monthlyCanvas.getContext("2d");
                        monthlyChartInstance = new Chart(ctx, {
                            type: "bar",
                            data: { labels: monthLabels, datasets },
                            options: {
                                responsive: true, maintainAspectRatio: false,
                                scales: { x: { grid: { display: false } }, y: { beginAtZero: true, grid: { color: chartGrid } } },
                                plugins: {
                                    zoom: { zoom: { wheel: { enabled: true }, pinch: { enabled: true }, mode: 'x' }, pan: { enabled: true, mode: 'x' } },
                                    legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', boxWidth: 8, padding: 12 } }
                                }
                            }
                        });
                    }
                }
            }
            if (rawData) {
                updateInvoiceChart(latestYear);
            }

            // 📊 REJECTED & CANCELLED REASONS (HORIZONTAL STACKED BAR) 
            const rejectCanvas = document.getElementById("rejectedReasonsChart");
            if (rejectCanvas && data.internalRejectedReasons && data.cancelledReasons) {
                const hasRejected = data.internalRejectedReasons.some(item => item.count > 0);
                const hasCancelled = data.cancelledReasons.some(item => item.count > 0);
                const hasRejectData = hasRejected || hasCancelled;

                handleEmptyCanvas("rejectedReasonsChart", hasRejectData, "No rejections or cancellations recorded.");

                if (hasRejectData) {
                    new Chart(rejectCanvas, {
                        type: "bar",
                        data: {
                            labels: data.internalRejectedReasons.map(item => item.reason),
                            datasets: [
                                { label: 'Internal Rejected', data: data.internalRejectedReasons.map(item => item.count), backgroundColor: statusTone('rejected'), borderRadius: 4 },
                                { label: 'LHDN Cancelled', data: data.cancelledReasons.map(item => item.count), backgroundColor: statusTone('cancelled'), borderRadius: 4 }
                            ]
                        },
                        options: {
                            indexAxis: 'y', responsive: true, maintainAspectRatio: false,
                            scales: { x: { stacked: true }, y: { stacked: true } },
                            plugins: { legend: { display: true, position: 'bottom' } }
                        }
                    });
                }
            }

            // --- HTML TABLES GENERATION ---
            // Empty states use the shared einv-empty component; the arguments are fixed strings.
            const emptyState = (icon, title, text) => `<div class="einv-empty"><span class="einv-empty-icon" aria-hidden="true"><i class="${icon}"></i></span><p class="einv-empty-title">${title}</p><p class="einv-empty-text">${text}</p></div>`;
            // One rate meter (buyer/supplier rejection, staff error rate). Every value is escaped; the bar
            // width is a clamped number.
            const meter = (tone, name, rate, left, right) => `<li class="einv-meter-${tone}"><div class="einv-meter-top"><span title="${esc(name)}">${esc(name)}</span><span>${esc(rate)}%</span></div><div class="einv-meter-bar" role="progressbar" aria-label="${esc(name)}" aria-valuenow="${esc(rate)}" aria-valuemin="0" aria-valuemax="100"><span style="width: ${Math.max(0, Math.min(100, Number(rate) || 0))}%"></span></div><div class="einv-meter-meta"><span>${left}</span><span>${right}</span></div></li>`;

            // 1. Suggested Fixes Table
            const fixesContainer = document.getElementById("rejectionFixesTable");
            let allFixes = [];
            if (data.internalRejectedReasons) {
                data.internalRejectedReasons.forEach(item => { if (item.count > 0) allFixes.push({ ...item, type: "Rejected", color: "text-danger" }); });
            }
            if (data.cancelledReasons) {
                data.cancelledReasons.forEach(item => { if (item.count > 0) allFixes.push({ ...item, type: "Cancelled", color: "text-secondary" }); });
            }

            if (fixesContainer) {
                if (allFixes.length > 0) {
                    let fixesHtml = '<ul class="einv-fixes">';
                    allFixes.forEach(item => {
                        let fixText = item.suggestedFix || item.SuggestedFix || "Review technical validation details.";
                        fixesHtml += `<li><b><i class="ri-error-warning-line ${item.color} me-1" aria-hidden="true"></i>${esc(item.type)}: ${esc(item.reason)}</b><p>${esc(fixText)}</p></li>`;
                    });
                    fixesHtml += '</ul>';
                    fixesContainer.innerHTML = fixesHtml;
                } else {
                    fixesContainer.innerHTML = emptyState('ri-shield-check-line', 'No fixes needed', 'No rejected or cancelled invoices need attention right now.');
                }
            }

            // 2. Buyer Rejection Table
            const buyerContainer = document.getElementById("buyerRejectionTable");
            if (buyerContainer) {
                if (data.customerRejectionRates && data.customerRejectionRates.length > 0) {
                    let html = '<ul class="einv-meters">';
                    data.customerRejectionRates.forEach(buyer => {
                        const tone = buyer.rejectionRate >= 50 ? 'danger' : (buyer.rejectionRate >= 20 ? 'warning' : 'info');
                        html += meter(tone, buyer.customerName, buyer.rejectionRate, `${esc(buyer.rejectedCount)} rejected by buyer`, `of ${esc(buyer.totalInvoices)} total`);
                    });
                    html += '</ul>';
                    buyerContainer.innerHTML = html;
                } else {
                    buyerContainer.innerHTML = emptyState('ri-check-double-line', 'No buyer rejections', 'None of your buyers have a high rejection rate.');
                }
            }

            // 3. Internal Errors Table
            const internalContainer = document.getElementById("internalErrorsTable");
            if (internalContainer) {
                if (data.internalErrorRates && data.internalErrorRates.length > 0) {
                    let html = '<ul class="einv-meters">';
                    data.internalErrorRates.forEach(staff => {
                        const tone = staff.errorRate >= 20 ? 'warning' : 'neutral';
                        html += meter(tone, staff.staffName, staff.errorRate, `${esc(staff.cancelledCount)} cancelled`, `of ${esc(staff.totalCreated)} created`);
                    });
                    html += '</ul>';
                    internalContainer.innerHTML = html;
                } else {
                    internalContainer.innerHTML = emptyState('ri-shield-check-line', 'No internal errors', 'No invoices were cancelled internally in this period.');
                }
            }

            // 4. Aging Drafts Table
            const draftsContainer = document.getElementById("agingDraftsTableBody");
            if (draftsContainer) {
                if (data.agingDrafts && data.agingDrafts.length > 0) {
                    let html = '';
                    data.agingDrafts.forEach(draft => {
                        const tone = draft.daysStuck > 7 ? 'danger' : 'warning';
                        const icon = draft.daysStuck > 7 ? 'ri-alarm-warning-line' : 'ri-time-line';
                        html += `<tr><td data-label="Invoice" class="ps-4 fw-semibold">${esc(draft.invoiceNo)}</td><td data-label="Buyer" class="truncate" title="${esc(draft.customerName)}">${esc(draft.customerName)}</td><td data-label="Created" class="text-muted small">${esc(draft.createdDate)}</td><td data-label="Days stuck"><span class="einv-badge einv-badge-${tone}"><i class="${icon}" aria-hidden="true"></i>${esc(draft.daysStuck)} days</span></td><td class="text-end pe-4 col-action"><div class="dropdown d-inline-block"><button class="btn btn-sm einv-action-btn" type="button" data-bs-toggle="dropdown" aria-expanded="false" aria-label="Actions"><i class="ri-more-fill align-middle"></i></button><ul class="dropdown-menu dropdown-menu-end"><li><a href="/Invoices/InvoiceEdit?id=${encodeURIComponent(draft.invoiceNo ?? '')}" class="dropdown-item"><i class="ri-pencil-fill align-bottom me-2 text-muted" aria-hidden="true"></i> Edit and submit</a></li></ul></div></td></tr>`;
                    });
                    draftsContainer.innerHTML = html;
                } else {
                    draftsContainer.innerHTML = `<tr><td colspan="5">${emptyState('ri-check-line', 'All caught up', 'No drafts have been waiting more than 3 days.')}</td></tr>`;
                }
            }

            // 5. BUYER: Supplier Rejection Table
            const supplierContainer = document.getElementById("supplierRejectionTable");
            if (supplierContainer) {
                if (data.supplierRejectionRates && data.supplierRejectionRates.length > 0) {
                    let html = '<ul class="einv-meters">';
                    data.supplierRejectionRates.forEach(supplier => {
                        const tone = supplier.rejectionRate >= 50 ? 'danger' : (supplier.rejectionRate >= 20 ? 'warning' : 'info');
                        html += meter(tone, supplier.supplierName, supplier.rejectionRate, `${esc(supplier.rejectedCount)} with issues`, `of ${esc(supplier.totalInvoices)} received`);
                    });
                    html += '</ul>';
                    supplierContainer.innerHTML = html;
                } else {
                    supplierContainer.innerHTML = emptyState('ri-shield-star-line', 'No supplier issues', 'None of your received invoices were rejected recently.');
                }
            }

            // 🛠️ PHASE 5: EXPORT RECENT INVOICES TO CSV (Global Function)
            window.exportRecentInvoices = function () {
                let csv = [];
                let table = document.getElementById("recentInvoicesTable");
                if (!table) {
                    alert("Could not find the table to export!");
                    return;
                }
                let rows = table.querySelectorAll("tr");
                for (let i = 0; i < rows.length; i++) {
                    let row = [], cols = rows[i].querySelectorAll("td, th");
                    for (let j = 0; j < cols.length - 1; j++) {
                        let cellData = cols[j].innerText.replace(/(\r\n|\n|\r)/gm, "").trim();
                        row.push('"' + cellData + '"');
                    }
                    csv.push(row.join(","));
                }
                let csvFile = new Blob([csv.join("\n")], { type: "text/csv" });
                let downloadLink = document.createElement("a");
                downloadLink.download = "Recent_System_Invoices.csv";
                downloadLink.href = window.URL.createObjectURL(csvFile);
                downloadLink.click();
            };

        }).catch(error => {
            console.error("Dashboard data fetch error: ", error);
        });
});