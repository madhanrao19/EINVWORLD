// 📊 Chart 1: Invoice Status Breakdown (Pie)
const statusCtx = document.getElementById("statusChart");
if (statusCtx) { // Add this check
    new Chart(statusCtx, {
        type: "pie",
        data: {
            labels: data.statusCounts.map(item => item.status),
            datasets: [{ data: data.statusCounts.map(item => item.count) }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: "right" }, colors: { enabled: true } }
        }
    });
}

document.addEventListener("DOMContentLoaded", function () {
    fetch("/Dashboard/MainDashboard?handler=ChartData")
        .then(response => response.json())
        .then(data => {
            if (data.error) {
                console.error("Dashboard error:", data.error);
                return;
            }

            // Change PascalCase to camelCase here:
            renderBarChart("topProductsChart", data.topProducts.map(x => x.label), data.topProducts.map(x => x.value));
            renderPieChart("rejectedReasonsChart", data.rejectedReasons.map(x => x.label), data.rejectedReasons.map(x => x.value));
            renderPieChart("invoiceTypeChart", data.invoiceTypes.map(x => x.label), data.invoiceTypes.map(x => x.value));
            renderLineChart("monthlySummaryChart", data.monthly.map(x => x.label), data.monthly.map(x => x.value));
        })
        .catch(error => {
            console.error("Fetch error:", error);
        });
});

function renderBarChart(canvasId, labels, data) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    new Chart(ctx, {
        type: "bar",
        data: { labels, datasets: [{ label: "Total", data, borderWidth: 1 }] },
        options: {
            responsive: true,
            maintainAspectRatio: false, // Force consistent height
            scales: { y: { beginAtZero: true } },
            plugins: { legend: { display: false } } // Hide legend for cleaner bar charts
        }
    });
}

function renderPieChart(canvasId, labels, data) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    new Chart(ctx, {
        type: "pie",
        data: { labels, datasets: [{ data }] },
        options: {
            responsive: true,
            maintainAspectRatio: false, // Force consistent height
            plugins: {
                legend: { position: "right" },
                tooltip: { callbacks: { label: ctx => `${ctx.label}: ${ctx.raw}` } }
            }
        }
    });
}

function renderLineChart(canvasId, labels, data) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    new Chart(ctx, {
        type: "line",
        data: {
            labels,
            datasets: [{
                label: "Invoices",
                data,
                borderWidth: 2,
                fill: true,
                backgroundColor: 'rgba(54, 162, 235, 0.1)',
                tension: 0.3
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false, // Force consistent height
            scales: { y: { beginAtZero: true } }
        }
    });
}