// "Saved bank account" picker on Create / Edit Invoice. The supplier's saved accounts (from LoadPartyDetails
// → bankAccounts, default first) fill the existing #bankName / #bankAccountNo fields, which are what the
// invoice saves — so a typed-in account still works and nothing else in the save path changes.
// Markup: <div id="bankAccountPickerWrap" class="d-none"><select id="bankAccountSelect"></select></div>
(function () {
    "use strict";

    function el(id) { return document.getElementById(id); }

    function masked(no) {
        var s = String(no || "").trim();
        return s.length <= 4 ? s : "•••• " + s.slice(-4);
    }

    function fill(opt) {
        if (!opt || !opt.value) return;
        var no = el("bankAccountNo"), name = el("bankName");
        if (no) { no.value = opt.dataset.accountNo || ""; no.dispatchEvent(new Event("change", { bubbles: true })); }
        if (name) { name.value = opt.dataset.bankName || ""; name.dispatchEvent(new Event("change", { bubbles: true })); }
    }

    /**
     * accounts: [{ id, bankName, bankAccountNo, isDefault }] (either casing).
     * keepCurrent: true on an existing invoice — select the option matching the saved account instead of
     * overwriting it with the default.
     */
    function populate(accounts, keepCurrent) {
        var wrap = el("bankAccountPickerWrap"), select = el("bankAccountSelect");
        if (!wrap || !select) return;

        var list = (accounts || []).map(function (a) {
            return {
                id: a.id != null ? a.id : a.Id,
                bankName: a.bankName != null ? a.bankName : (a.BankName || ""),
                no: a.bankAccountNo != null ? a.bankAccountNo : (a.BankAccountNo || ""),
                isDefault: !!(a.isDefault != null ? a.isDefault : a.IsDefault)
            };
        });

        select.innerHTML = "";
        if (list.length === 0) { wrap.classList.add("d-none"); return; }

        var placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "Other (type below)";
        select.appendChild(placeholder);

        list.forEach(function (a) {
            var o = document.createElement("option");
            o.value = a.id;
            o.textContent = (a.bankName || "Bank") + " — " + masked(a.no) + (a.isDefault ? " (default)" : "");
            o.dataset.accountNo = a.no;
            o.dataset.bankName = a.bankName;
            select.appendChild(o);
        });
        wrap.classList.remove("d-none");

        var currentNo = (el("bankAccountNo") || {}).value || "";
        var match = Array.prototype.find.call(select.options, function (o) { return o.value && o.dataset.accountNo === currentNo.trim(); });
        if (keepCurrent && currentNo) {
            select.value = match ? match.value : "";
            return;
        }
        var def = list.filter(function (a) { return a.isDefault; })[0] || list[0];
        select.value = String(def.id);
        fill(select.options[select.selectedIndex]);
    }

    document.addEventListener("change", function (e) {
        if (e.target && e.target.id === "bankAccountSelect") fill(e.target.options[e.target.selectedIndex]);
    });

    window.einvBankPicker = { populate: populate };
})();
