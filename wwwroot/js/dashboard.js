document.addEventListener("DOMContentLoaded", function () {
    const urlParams = new URLSearchParams(window.location.search);

    // --- 1. Filter Persistence ---
    if (urlParams.has('FilterType')) {
        sessionStorage.setItem('dash_FilterType', urlParams.get('FilterType'));
        sessionStorage.setItem('dash_FilterDate', urlParams.get('FilterDate'));
    } else if (sessionStorage.getItem('dash_FilterType')) {
        const savedType = sessionStorage.getItem('dash_FilterType');
        const savedDate = sessionStorage.getItem('dash_FilterDate');
        window.location.replace(window.location.pathname + `?FilterType=${savedType}&FilterDate=${savedDate}`);
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

        if (mode === 'Year') {
            filterDate.style.display = 'none';
            filterDate.removeAttribute('name');
            filterYear.style.display = 'block';
            filterYear.setAttribute('name', 'FilterDate');
        } else {
            filterYear.style.display = 'none';
            filterYear.removeAttribute('name');
            filterDate.style.display = 'block';
            filterDate.setAttribute('name', 'FilterDate');

            if (mode === 'Month') {
                filterDate.type = 'month';
                filterDate.value = defaultDateFull.substring(0, 7);
            } else if (mode === 'Day') {
                filterDate.type = 'date';
                filterDate.value = defaultDateFull;
            }
        }
    }

    if (filterType) {
        filterType.addEventListener('change', updateInputMode);
        updateInputMode();
    }

    // --- 3. Chart Rendering & Empty State Logic ---
    let monthlyChartInstance = null;
    const modernColors = ['#4f46e5', '#10b981', '#f59e0b', '#f43f5e', '#8b5cf6', '#06b6d4'];

    const doughnutOptions = {
        responsive: true, maintainAspectRatio: false, cutout: '78%',
        plugins: {
            legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', padding: 20, font: { size: 12, family: "'Inter', sans-serif" } } },
            tooltip: { backgroundColor: '#1f2937', padding: 10, cornerRadius: 8 }
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
                emptyDiv.className = 'empty-chart-state d-flex flex-column justify-content-center align-items-center h-100 w-100 text-center';
                emptyDiv.style.minHeight = "200px";
                emptyDiv.innerHTML = `
                    <div class="d-inline-flex align-items-center justify-content-center rounded-circle mb-2 shadow-sm" style="width: 48px; height: 48px; background-color: #f8f9fa; color: #9ca3af;">
                        <i class="ri-bar-chart-2-line fs-3"></i>
                    </div>
                    <h6 class="fw-bold text-dark mb-1" style="font-size: 0.95rem;">No Data Found</h6>
                    <p class="text-muted small mb-0 px-3">${customMessage}</p>
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
    const currentFilterDate = urlParams.get('FilterDate') || sessionStorage.getItem('dash_FilterDate') || new Date().toISOString().split('T')[0];

    fetch(`/Dashboard/Dashboard?handler=InvoiceData&FilterType=${currentFilterType}&FilterDate=${currentFilterDate}`)
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
                                backgroundColor: ['#f43f5e', '#10b981', '#f59e0b']
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
                                legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', padding: 15, font: { size: 11, family: "'Inter', sans-serif" } } },
                                tooltip: {
                                    backgroundColor: '#1f2937', padding: 12, cornerRadius: 8,
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
                                legend: { position: 'bottom', labels: { usePointStyle: true, pointStyle: 'circle', padding: 15, font: { size: 11, family: "'Inter', sans-serif" } } },
                                tooltip: {
                                    backgroundColor: '#1f2937', padding: 12, cornerRadius: 8,
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
                                scales: { x: { grid: { display: false } }, y: { beginAtZero: true, grid: { color: '#f3f4f6' } } },
                                plugins: {
                                    zoom: { zoom: { wheel: { enabled: true }, pinch: { enabled: true }, mode: 'x' }, pan: { enabled: true, mode: 'x' } },
                                    legend: { position: 'top', align: 'end' }
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
                                { label: 'Internal Rejected', data: data.internalRejectedReasons.map(item => item.count), backgroundColor: '#ef4444', borderRadius: 4 },
                                { label: 'LHDN Cancelled', data: data.cancelledReasons.map(item => item.count), backgroundColor: '#6b7280', borderRadius: 4 }
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
                    let fixesHtml = '<div class="mt-4 pt-2 border-top border-light-subtle"><div class="d-flex align-items-center mb-3"><div class="d-flex align-items-center justify-content-center rounded-circle me-2" style="width: 32px; height: 32px; background-color: #fffbeb; color: #d97706;"><i class="ri-lightbulb-flash-fill fs-5"></i></div><h6 class="fw-bold text-dark mb-0">Suggested Fixes</h6></div><div class="d-flex flex-column gap-2">';
                    allFixes.forEach(item => {
                        let fixText = item.suggestedFix || item.SuggestedFix || "Review technical validation details.";
                        fixesHtml += `<div class="p-3 rounded-3 border border-light-subtle" style="background-color: #f8f9fa;"><div class="fw-bold text-dark mb-1 d-flex align-items-center" style="font-size: 0.85rem;"><i class="ri-error-warning-line ${item.color} me-2"></i>[${item.type}] ${item.reason}</div><div class="text-muted ms-4" style="font-size: 0.8rem; line-height: 1.5;">${fixText}</div></div>`;
                    });
                    fixesHtml += '</div></div>';
                    fixesContainer.innerHTML = fixesHtml;
                } else {
                    fixesContainer.innerHTML = `<div class="text-center py-5 h-100 d-flex flex-column justify-content-center"><div class="d-inline-flex align-items-center justify-content-center rounded-circle mb-3 mx-auto" style="width: 50px; height: 50px; background-color: #f0fdf4; color: #16a34a;"><i class="ri-shield-check-fill fs-3"></i></div><h6 class="fw-bold text-dark">No Fixes Needed</h6><p class="text-muted small mb-0">There are no failed invoices requiring attention right now.</p></div>`;
                }
            }

            // 2. Buyer Rejection Table
            const buyerContainer = document.getElementById("buyerRejectionTable");
            if (buyerContainer) {
                if (data.customerRejectionRates && data.customerRejectionRates.length > 0) {
                    let html = '<div class="d-flex flex-column gap-4 mt-2">';
                    data.customerRejectionRates.forEach(buyer => {
                        let barColor = buyer.rejectionRate >= 50 ? 'bg-danger' : (buyer.rejectionRate >= 20 ? 'bg-warning' : 'bg-info');
                        html += `<div><div class="d-flex justify-content-between align-items-end mb-2"><span class="fw-bold text-dark text-truncate" style="max-width: 75%; font-size: 0.95rem;">${buyer.customerName}</span><span class="fw-bold text-dark">${buyer.rejectionRate}%</span></div><div class="progress shadow-sm" style="height: 8px; border-radius: 4px; background-color: #f3f4f6;"><div class="progress-bar ${barColor} progress-bar-striped progress-bar-animated" role="progressbar" style="width: ${buyer.rejectionRate}%" aria-valuenow="${buyer.rejectionRate}" aria-valuemin="0" aria-valuemax="100"></div></div><div class="d-flex justify-content-between mt-2"><span class="small fw-semibold text-danger"><i class="ri-user-unfollow-line align-middle"></i> ${buyer.rejectedCount} Rejected by Buyer</span><span class="small text-muted">out of ${buyer.totalInvoices} Total</span></div></div>`;
                    });
                    html += '</div>';
                    buyerContainer.innerHTML = html;
                } else {
                    buyerContainer.innerHTML = `<div class="text-center py-5"><div class="d-inline-flex align-items-center justify-content-center rounded-circle mb-3" style="width: 60px; height: 60px; background-color: #f0fdf4; color: #16a34a;"><i class="ri-check-double-line fs-1"></i></div><h6 class="fw-bold text-dark">Excellent Data Quality</h6><p class="text-muted small mb-0">None of your buyers currently have high rejection rates.</p></div>`;
                }
            }

            // 3. Internal Errors Table
            const internalContainer = document.getElementById("internalErrorsTable");
            if (internalContainer) {
                if (data.internalErrorRates && data.internalErrorRates.length > 0) {
                    let html = '<div class="d-flex flex-column gap-4 mt-2">';
                    data.internalErrorRates.forEach(staff => {
                        let barColor = staff.errorRate >= 20 ? 'bg-secondary' : 'bg-primary';
                        html += `<div><div class="d-flex justify-content-between align-items-end mb-2"><span class="fw-bold text-dark text-truncate" style="max-width: 75%; font-size: 0.95rem;"><i class="ri-user-smile-line text-muted me-1"></i> ${staff.staffName}</span><span class="fw-bold text-dark">${staff.errorRate}%</span></div><div class="progress shadow-sm" style="height: 8px; border-radius: 4px; background-color: #f3f4f6;"><div class="progress-bar ${barColor} progress-bar-striped progress-bar-animated" role="progressbar" style="width: ${staff.errorRate}%" aria-valuenow="${staff.errorRate}" aria-valuemin="0" aria-valuemax="100"></div></div><div class="d-flex justify-content-between mt-2"><span class="small fw-semibold text-secondary"><i class="ri-arrow-go-back-line align-middle"></i> ${staff.cancelledCount} Cancelled</span><span class="small text-muted">out of ${staff.totalCreated} Created</span></div></div>`;
                    });
                    html += '</div>';
                    internalContainer.innerHTML = html;
                } else {
                    internalContainer.innerHTML = `<div class="text-center py-5"><div class="d-inline-flex align-items-center justify-content-center rounded-circle mb-3" style="width: 60px; height: 60px; background-color: #eef2ff; color: #4f46e5;"><i class="ri-shield-check-line fs-1"></i></div><h6 class="fw-bold text-dark">High Accuracy</h6><p class="text-muted small mb-0">No internal cancellations recorded for this period.</p></div>`;
                }
            }

            // 4. Aging Drafts Table
            const draftsContainer = document.getElementById("agingDraftsTableBody");
            if (draftsContainer) {
                if (data.agingDrafts && data.agingDrafts.length > 0) {
                    let html = '';
                    data.agingDrafts.forEach(draft => {
                        let badgeClass = draft.daysStuck > 7 ? 'bg-danger text-white' : 'bg-warning text-dark';
                        let iconClass = draft.daysStuck > 7 ? 'ri-alarm-warning-fill' : 'ri-time-line';
                        html += `<tr><td class="ps-4 fw-bold text-dark">${draft.invoiceNo}</td><td class="text-truncate" style="max-width: 250px;">${draft.customerName}</td><td class="text-muted">${draft.createdDate}</td><td><span class="badge ${badgeClass} px-2 py-1 shadow-sm" style="font-size: 0.8rem;"><i class="${iconClass} align-middle me-1"></i> ${draft.daysStuck} Days</span></td><td class="text-end pe-4"><a href="/Invoices/InvoiceEdit?id=${draft.invoiceNo}" class="btn btn-sm btn-outline-primary fw-semibold transition-hover"><i class="ri-edit-box-line align-bottom me-1"></i> Edit & Submit</a></td></tr>`;
                    });
                    draftsContainer.innerHTML = html;
                } else {
                    draftsContainer.innerHTML = `<tr><td colspan="5" class="text-center py-5"><div class="d-inline-flex align-items-center justify-content-center rounded-circle mb-3 shadow-sm" style="width: 50px; height: 50px; background-color: #f0fdf4; color: #16a34a;"><i class="ri-check-line fs-3"></i></div><h6 class="fw-bold text-dark">All Caught Up!</h6><p class="text-muted small mb-0">You have no forgotten drafts older than 3 days.</p></td></tr>`;
                }
            }

            // 5. BUYER: Supplier Rejection Table
            const supplierContainer = document.getElementById("supplierRejectionTable");
            if (supplierContainer) {
                if (data.supplierRejectionRates && data.supplierRejectionRates.length > 0) {
                    let html = '<div class="d-flex flex-column gap-4 mt-2">';
                    data.supplierRejectionRates.forEach(supplier => {
                        let barColor = supplier.rejectionRate >= 50 ? 'bg-danger' : (supplier.rejectionRate >= 20 ? 'bg-warning' : 'bg-info');
                        html += `<div><div class="d-flex justify-content-between align-items-end mb-2"><span class="fw-bold text-dark text-truncate" style="max-width: 75%; font-size: 0.95rem;">${supplier.supplierName}</span><span class="fw-bold text-dark">${supplier.rejectionRate}%</span></div><div class="progress shadow-sm" style="height: 8px; border-radius: 4px; background-color: #f3f4f6;"><div class="progress-bar ${barColor} progress-bar-striped progress-bar-animated" role="progressbar" style="width: ${supplier.rejectionRate}%"></div></div><div class="d-flex justify-content-between mt-2"><span class="small fw-semibold text-danger"><i class="ri-error-warning-line align-middle"></i> ${supplier.rejectedCount} Issues</span><span class="small text-muted">out of ${supplier.totalInvoices} Received</span></div></div>`;
                    });
                    html += '</div>';
                    supplierContainer.innerHTML = html;
                } else {
                    supplierContainer.innerHTML = `<div class="text-center py-5"><div class="d-inline-flex align-items-center justify-content-center rounded-circle mb-3" style="width: 60px; height: 60px; background-color: #f0fdf4; color: #16a34a;"><i class="ri-shield-star-fill fs-1"></i></div><h6 class="fw-bold text-dark">Excellent Supplier Quality</h6><p class="text-muted small mb-0">No invoices have been rejected recently.</p></div>`;
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