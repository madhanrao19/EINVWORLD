// Shared dynamic line-item / tax-row editor for CreateSBI, CreateCN, and CreateSBCN.
// These three pages share an identical row structure (#lineItems tbody, #itemCount hidden input,
// #addItemBtn, per-row #subtotal-{i} span, per-row #taxes-{i} tbody, global addTax(i)/removeTax(this)/
// removeItem(this)) — they previously referenced a script (create-invoice-ori.js) that does not exist
// in the repo, so Add Item / Add Tax / Remove were silently non-functional (no console error suppressed
// it — the onclick handlers simply threw "function is not defined"). This restores that behaviour.
//
// Field names use the exact ASP.NET Core model-binding convention (Invoice.InvoiceLines[i].X,
// Invoice.InvoiceLines[i].Taxes[j].X) so posted values bind to the same InvoiceLineView/InvoiceTaxView
// list the server already expects — no PageModel changes needed.
(function () {
    "use strict";

    function lineItemsBody() { return document.getElementById("lineItems"); }

    function reindexRows() {
        var rows = lineItemsBody().querySelectorAll(":scope > tr");
        rows.forEach(function (row, i) {
            row.setAttribute("data-item-index", i);
            row.querySelectorAll("[name]").forEach(function (el) {
                el.name = el.name.replace(/InvoiceLines\[\d+\]/, "InvoiceLines[" + i + "]");
            });
            var subtotal = row.querySelector('span[id^="subtotal-"]');
            if (subtotal) subtotal.id = "subtotal-" + i;
            var taxesBody = row.querySelector('tbody[id^="taxes-"]');
            if (taxesBody) {
                taxesBody.id = "taxes-" + i;
                reindexTaxRows(taxesBody, i);
            }
            var addTaxBtn = row.querySelector('button[onclick^="addTax("]');
            if (addTaxBtn) addTaxBtn.setAttribute("onclick", "addTax(" + i + ")");
        });
        var itemCount = document.getElementById("itemCount");
        if (itemCount) itemCount.value = rows.length;
    }

    function reindexTaxRows(taxesBody, itemIndex) {
        var rows = taxesBody.querySelectorAll(":scope > tr");
        rows.forEach(function (row, j) {
            row.querySelectorAll("[name]").forEach(function (el) {
                el.name = el.name
                    .replace(/InvoiceLines\[\d+\]/, "InvoiceLines[" + itemIndex + "]")
                    .replace(/Taxes\[\d+\]/, "Taxes[" + j + "]");
            });
        });
    }

    function newLineRowHtml(i) {
        return "" +
            '<tr data-item-index="' + i + '">' +
            '<td><input name="Invoice.InvoiceLines[' + i + '].ClassificationCode" class="form-control" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + i + '].ItemCode" class="form-control" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + i + '].ItemDescription" class="form-control" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + i + '].Quantity" type="number" step="any" class="form-control line-qty" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + i + '].UnitOfMeasure" class="form-control" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + i + '].UnitPrice" type="number" step="any" class="form-control line-price" /></td>' +
            '<td><span id="subtotal-' + i + '" class="subtotal">0.00</span></td>' +
            '<td>' +
            '<table class="table">' +
            '<thead><tr><th>Tax Category</th><th>Tax Percentage</th><th>Tax Amount</th><th>Actions</th></tr></thead>' +
            '<tbody id="taxes-' + i + '"></tbody>' +
            '</table>' +
            '<button type="button" class="btn btn-secondary btn-sm" onclick="addTax(' + i + ')"><i class="ri-add-line align-bottom"></i> Add Tax</button>' +
            '</td>' +
            '<td><button type="button" class="btn btn-outline-danger btn-sm" onclick="removeItem(this)"><i class="ri-delete-bin-line align-bottom"></i> Remove</button></td>' +
            "</tr>";
    }

    function newTaxRowHtml(itemIndex, taxIndex) {
        return "" +
            "<tr>" +
            '<td><input name="Invoice.InvoiceLines[' + itemIndex + '].Taxes[' + taxIndex + '].TaxCategory" class="form-control" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + itemIndex + '].Taxes[' + taxIndex + '].TaxPercentage" type="number" step="any" class="form-control tax-percentage" /></td>' +
            '<td><input name="Invoice.InvoiceLines[' + itemIndex + '].Taxes[' + taxIndex + '].TaxAmount" type="number" step="any" class="form-control tax-amount" /></td>' +
            '<td><button type="button" class="btn btn-outline-danger btn-sm" onclick="removeTax(this)"><i class="ri-delete-bin-line align-bottom"></i> Remove</button></td>' +
            "</tr>";
    }

    function recalcAll() {
        var lineSubtotals = 0;
        var taxTotal = 0;
        lineItemsBody().querySelectorAll(":scope > tr").forEach(function (row, i) {
            var qty = parseFloat(row.querySelector(".line-qty")?.value) || 0;
            var price = parseFloat(row.querySelector(".line-price")?.value) || 0;
            var lineSubtotal = qty * price;
            lineSubtotals += lineSubtotal;
            var subtotalEl = document.getElementById("subtotal-" + i);
            if (subtotalEl) subtotalEl.textContent = lineSubtotal.toFixed(2);

            var taxesBody = document.getElementById("taxes-" + i);
            if (taxesBody) {
                taxesBody.querySelectorAll(":scope > tr").forEach(function (taxRow) {
                    var pct = parseFloat(taxRow.querySelector(".tax-percentage")?.value) || 0;
                    var amountInput = taxRow.querySelector(".tax-amount");
                    var amount = pct > 0 ? (lineSubtotal * pct / 100) : (parseFloat(amountInput?.value) || 0);
                    if (amountInput && pct > 0) amountInput.value = amount.toFixed(2);
                    taxTotal += amount;
                });
            }
        });

        var exclTax = document.getElementById("totalAmountExclTax");
        var taxAmt = document.getElementById("totalTaxAmount");
        var inclTax = document.getElementById("totalAmountIncTax");
        var payable = document.getElementById("totalPayableAmount");
        var net = document.getElementById("totalNetAmount");
        var discount = parseFloat(document.getElementById("totalDiscountAmount")?.value) || 0;

        if (exclTax) exclTax.value = lineSubtotals.toFixed(2);
        if (taxAmt) taxAmt.value = taxTotal.toFixed(2);
        var totalIncTax = lineSubtotals + taxTotal;
        if (inclTax) inclTax.value = totalIncTax.toFixed(2);
        var totalPayable = totalIncTax - discount;
        if (payable) payable.value = totalPayable.toFixed(2);
        if (net) net.value = totalPayable.toFixed(2);
    }

    window.addTax = function (itemIndex) {
        var taxesBody = document.getElementById("taxes-" + itemIndex);
        if (!taxesBody) return;
        var nextTaxIndex = taxesBody.querySelectorAll(":scope > tr").length;
        taxesBody.insertAdjacentHTML("beforeend", newTaxRowHtml(itemIndex, nextTaxIndex));
        recalcAll();
    };

    window.removeTax = function (button) {
        var row = button.closest("tr");
        var taxesBody = row.closest("tbody");
        row.remove();
        reindexTaxRows(taxesBody, parseInt(taxesBody.closest("tr").getAttribute("data-item-index"), 10));
        recalcAll();
    };

    window.removeItem = function (button) {
        var row = button.closest("tr");
        // Keep at least one line — an invoice needs at least one item.
        if (lineItemsBody().querySelectorAll(":scope > tr").length <= 1) return;
        row.remove();
        reindexRows();
        recalcAll();
    };

    document.addEventListener("DOMContentLoaded", function () {
        var addItemBtn = document.getElementById("addItemBtn");
        if (addItemBtn) {
            addItemBtn.addEventListener("click", function () {
                var nextIndex = lineItemsBody().querySelectorAll(":scope > tr").length;
                lineItemsBody().insertAdjacentHTML("beforeend", newLineRowHtml(nextIndex));
                var itemCount = document.getElementById("itemCount");
                if (itemCount) itemCount.value = nextIndex + 1;
            });
        }

        lineItemsBody()?.addEventListener("input", function (e) {
            if (e.target.matches(".line-qty, .line-price, .tax-percentage, .tax-amount")) {
                recalcAll();
            }
        });
        document.getElementById("totalDiscountAmount")?.addEventListener("input", recalcAll);

        recalcAll();
    });
})();
