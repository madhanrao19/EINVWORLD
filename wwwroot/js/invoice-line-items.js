// Shared dynamic line-item / tax-row editor for CreateSBI, CreateCN, and CreateSBCN.
// These three pages share an identical row structure (#lineItems item-row cards, #itemCount hidden
// input, #addItemBtn, per-row #subtotal-{i}, per-row #taxes-{i} tax-row divs, global
// addTax(i)/removeTax(this)/removeItem(this)) — they previously referenced a script
// (create-invoice-ori.js) that does not exist in the repo, so Add Item / Add Tax / Remove were
// silently non-functional (no console error suppressed it — the onclick handlers simply threw
// "function is not defined"). This restores that behaviour.
//
// Field names use the exact ASP.NET Core model-binding convention (Invoice.InvoiceLines[i].X,
// Invoice.InvoiceLines[i].Taxes[j].X) so posted values bind to the same InvoiceLineView/InvoiceTaxView
// list the server already expects — no PageModel changes needed.
(function () {
    "use strict";

    function lineItemsBody() { return document.getElementById("lineItems"); }

    function reindexRows() {
        var rows = lineItemsBody().querySelectorAll(":scope > .item-row");
        rows.forEach(function (row, i) {
            row.setAttribute("data-item-index", i);
            row.querySelectorAll("[name]").forEach(function (el) {
                el.name = el.name.replace(/InvoiceLines\[\d+\]/, "InvoiceLines[" + i + "]");
            });
            var subtotal = row.querySelector('[id^="subtotal-"]');
            if (subtotal) subtotal.id = "subtotal-" + i;
            var taxesSection = row.querySelector('[id^="taxes-"]');
            if (taxesSection) {
                taxesSection.id = "taxes-" + i;
                reindexTaxRows(taxesSection, i);
            }
            var addTaxBtn = row.querySelector('button[onclick^="addTax("]');
            if (addTaxBtn) addTaxBtn.setAttribute("onclick", "addTax(" + i + ")");
        });
        var itemCount = document.getElementById("itemCount");
        if (itemCount) itemCount.value = rows.length;
    }

    function reindexTaxRows(taxesSection, itemIndex) {
        var rows = taxesSection.querySelectorAll(":scope > .tax-row");
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
            '<div class="item-row card mb-3" style="border-left: 4px solid var(--einv-primary, #006948);" data-item-index="' + i + '">' +
            '<div class="card-body">' +
            '<div class="row g-3">' +
            '<div class="col-md-3"><label class="text-uppercase text-muted small mb-1">Classification Code</label>' +
            '<input name="Invoice.InvoiceLines[' + i + '].ClassificationCode" class="form-control form-control-sm" /></div>' +
            '<div class="col-md-3"><label class="text-uppercase text-muted small mb-1">Item Code</label>' +
            '<input name="Invoice.InvoiceLines[' + i + '].ItemCode" class="form-control form-control-sm" /></div>' +
            '<div class="col-md-6"><label class="text-uppercase text-muted small mb-1">Item Description</label>' +
            '<input name="Invoice.InvoiceLines[' + i + '].ItemDescription" class="form-control form-control-sm" /></div>' +
            '<div class="col-md-3"><label class="text-uppercase text-muted small mb-1">Quantity</label>' +
            '<input name="Invoice.InvoiceLines[' + i + '].Quantity" type="number" step="any" class="form-control form-control-sm line-qty" /></div>' +
            '<div class="col-md-3"><label class="text-uppercase text-muted small mb-1">Unit Measurement</label>' +
            '<input name="Invoice.InvoiceLines[' + i + '].UnitOfMeasure" class="form-control form-control-sm" /></div>' +
            '<div class="col-md-3"><label class="text-uppercase text-muted small mb-1">Unit Price</label>' +
            '<input name="Invoice.InvoiceLines[' + i + '].UnitPrice" type="number" step="any" class="form-control form-control-sm line-price" /></div>' +
            '<div class="col-md-3"><label class="text-uppercase text-muted small mb-1">Subtotal</label>' +
            '<div class="p-2 rounded bg-light text-end fw-bold subtotal" id="subtotal-' + i + '">0.00</div></div>' +
            '<div class="col-12"><label class="text-uppercase text-muted small mb-1">Tax</label>' +
            '<div class="tax-section" id="taxes-' + i + '"></div>' +
            '<button type="button" class="btn btn-sm btn-primary mt-1" onclick="addTax(' + i + ')"><i class="ri-add-line align-bottom"></i> Add Tax</button></div>' +
            '</div>' +
            '<div class="d-flex justify-content-end mt-3 pt-3 border-top">' +
            '<button type="button" class="btn btn-link btn-sm text-danger text-decoration-none p-0" onclick="removeItem(this)"><i class="ri-delete-bin-line me-1"></i>Remove</button>' +
            '</div>' +
            '</div>' +
            '</div>';
    }

    function newTaxRowHtml(itemIndex, taxIndex) {
        return "" +
            '<div class="tax-row mb-2 p-2 border rounded">' +
            '<div class="d-flex flex-wrap gap-1 align-items-start">' +
            '<div class="flex-fill" style="min-width: 100px;"><input name="Invoice.InvoiceLines[' + itemIndex + '].Taxes[' + taxIndex + '].TaxCategory" class="form-control form-control-sm tax-category" placeholder="Tax Category" /></div>' +
            '<div style="min-width: 65px; flex: 0 0 70px;"><input name="Invoice.InvoiceLines[' + itemIndex + '].Taxes[' + taxIndex + '].TaxPercentage" type="number" step="any" class="form-control form-control-sm tax-percentage" placeholder="%" /></div>' +
            '<div style="min-width: 85px; flex: 0 0 90px;"><input name="Invoice.InvoiceLines[' + itemIndex + '].Taxes[' + taxIndex + '].TaxAmount" type="number" step="any" class="form-control form-control-sm tax-amount" placeholder="Amount" /></div>' +
            '<div style="flex: 0 0 auto;"><i class="ri-close-circle-line text-danger" onclick="removeTax(this)" title="Remove Tax" style="cursor: pointer; font-size: 16px; padding: 2px;"></i></div>' +
            '</div>' +
            '</div>';
    }

    function recalcAll() {
        var lineSubtotals = 0;
        var taxTotal = 0;
        lineItemsBody().querySelectorAll(":scope > .item-row").forEach(function (row, i) {
            var qty = parseFloat(row.querySelector(".line-qty")?.value) || 0;
            var price = parseFloat(row.querySelector(".line-price")?.value) || 0;
            var lineSubtotal = qty * price;
            lineSubtotals += lineSubtotal;
            var subtotalEl = document.getElementById("subtotal-" + i);
            if (subtotalEl) subtotalEl.textContent = lineSubtotal.toFixed(2);

            var taxesSection = document.getElementById("taxes-" + i);
            if (taxesSection) {
                taxesSection.querySelectorAll(":scope > .tax-row").forEach(function (taxRow) {
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
        var taxesSection = document.getElementById("taxes-" + itemIndex);
        if (!taxesSection) return;
        var nextTaxIndex = taxesSection.querySelectorAll(":scope > .tax-row").length;
        taxesSection.insertAdjacentHTML("beforeend", newTaxRowHtml(itemIndex, nextTaxIndex));
        recalcAll();
    };

    window.removeTax = function (button) {
        var row = button.closest(".tax-row");
        var taxesSection = row.closest('[id^="taxes-"]');
        var itemIndex = parseInt(taxesSection.closest(".item-row").getAttribute("data-item-index"), 10);
        row.remove();
        reindexTaxRows(taxesSection, itemIndex);
        recalcAll();
    };

    window.removeItem = function (button) {
        var row = button.closest(".item-row");
        // Keep at least one line — an invoice needs at least one item.
        if (lineItemsBody().querySelectorAll(":scope > .item-row").length <= 1) return;
        row.remove();
        reindexRows();
        recalcAll();
    };

    document.addEventListener("DOMContentLoaded", function () {
        var addItemBtn = document.getElementById("addItemBtn");
        if (addItemBtn) {
            addItemBtn.addEventListener("click", function () {
                var nextIndex = lineItemsBody().querySelectorAll(":scope > .item-row").length;
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
