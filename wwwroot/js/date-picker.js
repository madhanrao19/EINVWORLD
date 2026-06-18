document.addEventListener("DOMContentLoaded", function () {
    // 1. Get the values from the URL query string
    const urlParams = new URLSearchParams(window.location.search);
    const dateFrom = urlParams.get('submissionDateFrom');
    const dateTo = urlParams.get('submissionDateTo');

    // 2. Define default fallback dates (past 1 month)
    let defaultStartDate = new Date(new Date().setMonth(new Date().getMonth() - 1)).toISOString().split('T')[0];
    let defaultEndDate = new Date().toISOString().split('T')[0];

    // 3. Override defaults if URL parameters exist
    if (dateFrom && dateTo) {
        defaultStartDate = dateFrom;
        defaultEndDate = dateTo;
    }

    // 4. Initialize Flatpickr
    flatpickr("#submissionDateRange", {
        mode: "range",
        dateFormat: "Y-m-d",
        defaultDate: [defaultStartDate, defaultEndDate],
        onClose: function (selectedDates) {
            if (selectedDates.length === 2) {
                // Adjust for local timezone to prevent off-by-one errors
                const tzoffset = (new Date()).getTimezoneOffset() * 60000;

                const start = new Date(selectedDates[0].getTime() - tzoffset).toISOString().split('T')[0];
                const end = new Date(selectedDates[1].getTime() - tzoffset).toISOString().split('T')[0];

                document.getElementById("submissionDateFrom").value = start;
                document.getElementById("submissionDateTo").value = end;
            }
        }
    });

    // Add event listeners for min and max inputs to update hidden fields on form submission
    const minAmountInput = document.getElementById('minTotalAmount');
    const maxAmountInput = document.getElementById('maxTotalAmount');
    const minAmountHidden = document.getElementById('minTotalAmountHidden');
    const maxAmountHidden = document.getElementById('maxTotalAmountHidden');

    // Check if the form actually exists before adding an event listener to avoid errors
    const formElement = document.querySelector('form');
    if (formElement) {
        // When the form is submitted, calculate the min and max values and add them to hidden fields
        formElement.addEventListener('submit', function (event) {

            // Check if values are entered for min and max
            const minAmount = minAmountInput && minAmountInput.value ? parseFloat(minAmountInput.value) : null;
            const maxAmount = maxAmountInput && maxAmountInput.value ? parseFloat(maxAmountInput.value) : null;

            // If a value is entered, set the hidden inputs
            if (minAmount !== null && minAmountHidden) {
                minAmountHidden.value = minAmount;
            } else if (minAmountHidden) {
                minAmountHidden.value = ''; // Clear hidden field if no value
            }

            if (maxAmount !== null && maxAmountHidden) {
                maxAmountHidden.value = maxAmount;
            } else if (maxAmountHidden) {
                maxAmountHidden.value = ''; // Clear hidden field if no value
            }
        });
    }
});