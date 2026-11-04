/*lhdn-tin-validate.js*/
// Shared "Validate with LHDN" button + save gate for the Buyer and Company Create/Edit forms.
// The page wires it up with initLhdnTinValidation({...}); the markup keeps calling the global
// validateLHDN() (button onclick) and checkValidationBeforeSubmit() (form onsubmit).
(function () {
    const GENERAL_TINS = ["EI00000000010", "EI00000000020", "EI00000000030", "EI00000000040"];
    let cfg = null;
    let validated = false;

    const val = id => (document.getElementById(id)?.value || "").trim();

    function setButtons(cls, html, disabled) {
        document.querySelectorAll('.btn-validate').forEach(btn => {
            btn.classList.remove('btn-info', 'btn-success', 'btn-danger');
            btn.classList.add(cls);
            btn.innerHTML = html;
            btn.disabled = !!disabled;
        });
    }

    function reset() {
        validated = false;
        setButtons('btn-info', 'Validate with LHDN');
    }

    const markValidated = () => setButtons('btn-success', '<i class="ri-check-line align-bottom"></i> Validated');
    const markFailed = text => setButtons('btn-danger', '<i class="ri-close-line align-bottom"></i> ' + text);

    window.validateLHDN = async function () {
        const tin = val(cfg.tinId), regType = val(cfg.regTypeId), regNo = val(cfg.regNoId);

        if (!tin || !regType || !regNo) { validated = false; markFailed("Missing Info - Retry"); return; }
        if (GENERAL_TINS.includes(tin)) { validated = true; markValidated(); return; }

        setButtons('btn-info', '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Validating...', true);
        try {
            // POST body, never the URL: the ID number is an NRIC for individuals (URLs end up in server/CDN logs).
            const response = await fetch(cfg.url, {
                method: 'POST',
                headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
                body: new URLSearchParams({ tin: tin, idType: regType, idNo: regNo })
            });
            const data = await response.json();
            validated = !!data.success;
            if (validated) markValidated(); else { markFailed("Failed - Retry"); console.warn(data.message); }
        } catch (error) {
            console.error("Validation Error:", error);
            validated = false;
            markFailed("Error - Retry");
        }
    };

    window.checkValidationBeforeSubmit = function () {
        if (validated || GENERAL_TINS.includes(val(cfg.tinId))) return true;
        alert(`Please validate the ${cfg.entityLabel} Info (TIN, Reg No, ID Type) before saving.`);
        return false;
    };

    /**
     * @param {{tinId:string, regTypeId:string, regNoId:string, url:string, entityLabel:string, startValidated?:boolean}} options
     * startValidated: true on an Edit form showing saved values — saving is only gated again once
     * the TIN, Registration Type or Registration Number is changed.
     */
    window.initLhdnTinValidation = function (options) {
        cfg = options;
        validated = !!options.startValidated;
        // jQuery binding so the Select2-driven Registration Type change (a jQuery-triggered event) resets too.
        $('#' + cfg.tinId + ', #' + cfg.regNoId).on('input', reset);
        $('#' + cfg.regTypeId).on('change', reset);
    };
})();
