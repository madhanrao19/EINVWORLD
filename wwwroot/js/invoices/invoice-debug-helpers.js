// Helper functions for loading indicators and debug functionality
function showLoadingSpinners(partyType) {
    if (partyType === 'supplier') {
        $('#bankAccountNoSpinner').show();
        $('#bankNameSpinner').show();
    } else if (partyType === 'buyer') {
        $('#attentionSpinner').show();
    }
}

function hideLoadingSpinners(partyType) {
    if (partyType === 'supplier') {
        $('#bankAccountNoSpinner').hide();
        $('#bankNameSpinner').hide();
    } else if (partyType === 'buyer') {
        $('#attentionSpinner').hide();
    }
}

function showAutoPopulatedIndicator(fieldId) {
    $(`#${fieldId}Indicator`).show();
    $(`#${fieldId}`).addClass('is-valid');

    // Remove indicator after 3 seconds
    setTimeout(() => {
        $(`#${fieldId}Indicator`).hide();
    }, 3000);
}

function hideAutoPopulatedIndicator(fieldId) {
    $(`#${fieldId}Indicator`).hide();
    $(`#${fieldId}`).removeClass('is-valid');
}

function clearBankAccountFields() {
    $('#bankAccountNo').val('');
    $('#bankName').val('');
    hideAutoPopulatedIndicator('bankAccountNo');
    hideAutoPopulatedIndicator('bankName');
}

function updateDebugInfo(field, value) {
    if (isDebugModeEnabled()) {
        $(`#debug${field.charAt(0).toUpperCase() + field.slice(1)}`).text(value);
        $('#debugPanel').show();
    }
}

function incrementErrorCount() {
    if (isDebugModeEnabled()) {
        const currentCount = parseInt($('#debugErrorCount').text()) || 0;
        $('#debugErrorCount').text(currentCount + 1);
    }
}

function isDebugModeEnabled() {
    return $('#debugMode').is(':checked');
}

function toggleDebug() {
    const debugPanel = $('#debugPanel');
    if (isDebugModeEnabled()) {
        debugPanel.show();
        console.log('🔧 Debug mode enabled');
    } else {
        debugPanel.hide();
        console.log('🔧 Debug mode disabled');
    }
}

function testBankAccountLoading() {
    console.log('🧪 Testing bank account loading functionality...');

    const supplierSelect = document.getElementById('supplierSelect');
    const buyerSelect = document.getElementById('buyerSelect');

    if (!supplierSelect || !buyerSelect) {
        ErrorHandler.show('Required dropdowns not found.', 'error');
        return;
    }

    const supplierId = supplierSelect.value;
    const buyerId = buyerSelect.value;

    if (!supplierId && !buyerId) {
        ErrorHandler.show('Please select a supplier or buyer first.', 'warning');
        return;
    }

    // Test with available data
    if (supplierId) {
        console.log('🧪 Testing supplier loading...');
        loadPartyDetails(supplierId, 'PI', 'supplier');
    }

    if (buyerId) {
        console.log('🧪 Testing buyer loading...');

        const parts = buyerId.split('_');

        if (parts.length === 2) {
            loadPartyDetails(parts[1], parts[0], 'buyer');
        } else {
            console.warn("⚠️ Invalid buyer format:", buyerId);
        }
    }

    ErrorHandler.show('Check console for detailed results.', 'success');
}

function addTaxToAllItems() {
    console.log('🔧 Adding taxes to all items that don\'t have any...');

    const itemRows = document.querySelectorAll('.item-row');
    let addedCount = 0;

    itemRows.forEach((row, index) => {
        const itemIndex = row.getAttribute('data-item-index');
        const taxSection = document.getElementById(`taxes-${itemIndex}`);
        const taxRows = taxSection?.querySelectorAll('.tax-row') || [];

        if (taxRows.length === 0) {
            addTaxRow(itemIndex);
            addedCount++;
            console.log(`✅ Added tax to item #${index + 1}`);
        }
    });

    if (addedCount > 0) {
        ErrorHandler.show(`Added default taxes to ${addedCount} item(s)! Please fill in the tax category and percentage.`, 'success');
        // Clear visual indicators and show success state
        setTimeout(() => {
            clearTaxVisualIndicators();
            validateItemRequirements();
        }, 500);
    } else {
        ErrorHandler.show('All items already have taxes!', 'info');
    }
}
