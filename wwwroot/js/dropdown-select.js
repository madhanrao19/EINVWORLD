$(document).ready(function () {
    $('#idTypeSelect').select2({
        placeholder: "Select an ID Type",
        allowClear: true,
        width: '100%', // Ensure full-width responsiveness
        dropdownParent: $('#idTypeSelect').parent() // To avoid overflow
    });

    // Initialize Select2 for state and country selects
    $('#stateSelect').select2({
        placeholder: "Select a state",
        allowClear: true,
        width: '100%', // Ensure full-width responsiveness
        dropdownParent: $('#stateSelect').parent() // To avoid overflow
    });

    $('#countrySelect').select2({
        placeholder: "Select a Country",
        allowClear: true,
        width: '100%', // Ensure full-width responsiveness
        dropdownParent: $('#countrySelect').parent() // To avoid overflow
    });

    // Handle change event for state dropdown
    $('#stateSelect').on('change', function () {
        var selectedCode = $(this).val();
        var countrySelect = $('#countrySelect');

        // Clear the country dropdown
        countrySelect.val('').trigger('change');

        // Check if selected code is from 01 to 14
        if (selectedCode >= "01" && selectedCode <= "14") {
            // Auto-select "MYS"
            countrySelect.val('MYS').trigger('change');
        }
    });

    $('#msicSelect').select2({
        placeholder: "Select a MSIC Code",
        allowClear: true,
        width: '100%', // Ensure full-width responsiveness
        //dropdownParent: $('#msicSelect').parent() // To avoid overflow
    });




});
