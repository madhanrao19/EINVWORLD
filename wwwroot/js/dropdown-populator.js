// wwwroot/js/dropdown-populator.js

/**
 * Populates a dropdown menu with options derived from JSON data.
 * @param {string} dropdownId - The ID of the dropdown element.
 * @param {Object[]} data - The JSON data to populate the dropdown with.
 */
function populateDropdown(dropdownId, data) {
    const dropdown = document.getElementById(dropdownId);

    if (data && dropdown) {
        data.forEach(item => {
            const option = document.createElement('option');
            option.value = item.Code;
            option.textContent = `${item.Code} - ${item.Description}`;
            dropdown.appendChild(option);
        });
    }
}

/**
 * Initializes dropdowns with data from JSON files.
 */
async function initializeDropdowns() {
    const classificationCodes = await fetchJsonData('/data/classification-codes.json');
    populateDropdown('classificationCodesDropdown', classificationCodes);

    const countryCodes = await fetchJsonData('/data/country-codes.json');
    populateDropdown('countryCodesDropdown', countryCodes);

    const currencyCodes = await fetchJsonData('/data/currency-codes.json');
    populateDropdown('currencyCodesDropdown', currencyCodes);

    const eInvoiceTypes = await fetchJsonData('/data/e-invoice-types.json');
    populateDropdown('eInvoiceTypesDropdown', eInvoiceTypes);

    const msicCodes = await fetchJsonData('/data/msic-codes.json');
    populateDropdown('msicCodesDropdown', msicCodes);

    const paymentModes = await fetchJsonData('/data/payment-modes.json');
    populateDropdown('paymentModesDropdown', paymentModes);

    const stateCodes = await fetchJsonData('/data/state-codes.json');
    populateDropdown('stateCodesDropdown', stateCodes);

    const taxTypes = await fetchJsonData('/data/tax-types.json');
    populateDropdown('taxTypesDropdown', taxTypes);

    const unitOfMeasurement = await fetchJsonData('/data/unit-of-measurement.json');
    populateDropdown('unitOfMeasurementDropdown', unitOfMeasurement);
}

document.addEventListener('DOMContentLoaded', initializeDropdowns);
