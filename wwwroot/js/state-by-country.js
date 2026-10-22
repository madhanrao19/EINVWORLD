/* state-by-country.js
 * Malaysia keeps the fixed LHDN State dropdown (LHDN's own MyInvois Portal requires it); any other country
 * switches to a free-text State / Province input, matching LHDN's portal for foreign parties. "No country
 * chosen yet" counts as Malaysia so the dropdown is the default on a fresh form. Only the visible field is
 * enabled, so exactly one StateCode value is posted. Used by Add/Edit Buyer and Create/Edit Company.
 */
function bindStateToCountry(countryId, stateSelectId, stateTextId) {
    const country = document.getElementById(countryId);
    const select = document.getElementById(stateSelectId);
    const text = document.getElementById(stateTextId);
    if (!country || !select || !text) return;

    function apply() {
        const isMalaysia = country.value === '' || country.value === 'MYS';
        // A Select2 dropdown hides the <select> itself; hide its container instead.
        const select2 = $(select).next('.select2-container');
        (select2.length ? select2 : $(select)).toggleClass('d-none', !isMalaysia);
        select.disabled = !isMalaysia;
        select.required = isMalaysia;
        text.classList.toggle('d-none', isMalaysia);
        text.disabled = isMalaysia;
        text.required = !isMalaysia;
    }

    $(country).on('change', apply); // native and Select2 changes both reach a jQuery handler
    apply();
}
