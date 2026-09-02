        // currentStep and totalSteps moved to line 2696 to avoid duplicate declaration

        // Track the last selected buyer
        let lastSelectedBuyerId = null;

        // Classification/unit/tax-category/saved-item dropdown HTML for dynamic item creation.
        // Built server-side in CreateInvoice.cshtml (Razor loops over Model data) and exposed
        // via window.* because this file cannot contain Razor syntax.
        const classificationOptionsHtml = window.classificationOptionsHtml;
        const unitOptionsHtml = window.unitOptionsHtml;
        const taxCategoryOptionsHtml = window.taxCategoryOptionsHtml;
        const countryOptionsHtml = window.countryOptionsHtml;

        // Debug: Log dropdown data immediately
        console.log('🔍 Dropdown HTML generated:', {
            classificationCount: classificationOptionsHtml.split('<option').length - 1,
            unitCount: unitOptionsHtml.split('<option').length - 1,
            taxCategoryCount: taxCategoryOptionsHtml.split('<option').length - 1,
            classificationSample: classificationOptionsHtml.substring(0, 100),
            unitSample: unitOptionsHtml.substring(0, 100),
            taxSample: taxCategoryOptionsHtml.substring(0, 100)
        });


        // Removed auto-fill tax function - users should fill their own tax values

        // Real-time field validation functions
        function validateField(field) {
            try {
                if (!field) {
                    console.warn('⚠️ validateField called with null/undefined field');
                    return;
                }

                const row = field.closest('.item-row');
                if (!row) {
                    console.warn('⚠️ Could not find parent item-row for field validation');
                    return;
                }

                // Mark the field the user actually interacted with as touched — validateRow()
                // only styles touched fields, so a sibling field the user hasn't reached yet
                // doesn't turn red just because this one changed.
                field.dataset.touched = 'true';

                // Validate the entire row at once
                validateRow(row);
            } catch (error) {
                console.error('❌ Error in validateField:', error);
            }
        }

        // MyInvois permits a genuine 0% rate on any tax type (e.g. Sales Tax at 0%), not just
        // "Not Applicable"/"Exemption" — so a tax line is valid once a rate is actually entered,
        // whether or not it is zero. Only a blank/negative percentage is rejected.
        function isTaxPercentageProvided(percentageInput) {
            const raw = percentageInput?.value;
            if (raw === undefined || raw === null || raw.trim() === '') return false;
            const value = parseFloat(raw);
            return !isNaN(value) && value >= 0;
        }

        function validateTaxField(field) {
            try {
                if (!field) {
                    console.warn('⚠️ validateTaxField called with null/undefined field');
                    return;
                }

                const taxRow = field.closest('.tax-row');
                if (!taxRow) {
                    console.warn('⚠️ Could not find parent tax-row for tax field validation');
                    return;
                }

                // Additional safety check for taxRow
                if (typeof taxRow.closest !== 'function') {
                    console.warn('⚠️ taxRow.closest is not a function, skipping validation');
                    return;
                }

                const itemRow = taxRow.closest('.item-row');
                if (!itemRow) {
                    console.warn('⚠️ Could not find parent item-row for tax field validation');
                    return;
                }

                // Mark the field the user actually interacted with as touched — see validateField().
                field.dataset.touched = 'true';

                // Validate the entire row at once
                validateRow(itemRow);
            } catch (error) {
                console.error('❌ Error in validateTaxField:', error);
            }
        }

        function validateRow(row) {
            // Safety check: ensure row exists
            if (!row) {
                console.warn('⚠️ validateRow called with null/undefined row');
                return;
            }

            // Skip while the page's own initial setup is still applying default values — see
            // isInitializing's declaration for why this exists.
            if (typeof isInitializing !== 'undefined' && isInitializing) {
                return;
            }

            try {
                // Get all validation elements in the row
                const quantityInput = row.querySelector('.quantity-input');
                const priceInput = row.querySelector('.price-input');
                const classificationSelect = row.querySelector('select[name*=".ClassificationCode"]');
                const itemDescriptionTextarea = row.querySelector('textarea[name*=".ItemDescription"]');
                const unitSelect = row.querySelector('select[name*=".UnitOfMeasure"]');
                const taxRows = row.querySelectorAll('.tax-row');

            // Validate quantity and price
            const quantityValue = parseFloat(quantityInput?.value) || 0;
            const priceValue = parseFloat(priceInput?.value) || 0;

            const quantityValid = quantityValue > 0;
            const priceValid = priceValue > 0;

            // Validate other required fields
            const classificationValid = classificationSelect?.value && classificationSelect.value !== '';
            const descriptionValid = itemDescriptionTextarea?.value && itemDescriptionTextarea.value.trim() !== '';
            const unitValid = unitSelect?.value && unitSelect.value !== '';

            // Validate taxes
            let taxValid = false;
            taxRows.forEach(taxRow => {
                const categorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
                const percentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');

                const categoryValue = categorySelect?.value;

                if (categoryValue && categoryValue !== '' && isTaxPercentageProvided(percentageInput)) {
                    taxValid = true;
                }
            });

            // Update field styling — but only for fields the user has actually touched, so a
            // change to one field in the row (e.g. picking a tax category) doesn't paint every
            // other still-empty, never-touched field in the row red (that's what Next/Submit's
            // validateCurrentStep() is for — it deliberately marks everything on a submit attempt).
            const styleIfTouched = (field, valid) => {
                if (field && field.dataset.touched === 'true') updateFieldStyling(field, valid);
            };
            styleIfTouched(quantityInput, quantityValid);
            styleIfTouched(priceInput, priceValid);
            styleIfTouched(classificationSelect, classificationValid);
            styleIfTouched(itemDescriptionTextarea, descriptionValid);
            styleIfTouched(unitSelect, unitValid);

            // Update tax field styling
            taxRows.forEach(taxRow => {
                const categorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
                const percentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');

                const categoryValue = categorySelect?.value;

                const categoryValid = categoryValue && categoryValue !== '';

                // A rate is valid once it's present and non-negative — 0% is fine on any category.
                const percentageValid = categoryValid && isTaxPercentageProvided(percentageInput);

                styleIfTouched(categorySelect, categoryValid);
                styleIfTouched(percentageInput, percentageValid);

                // Validate exemption reason field if visible
                const exemptionReasonInput = taxRow.querySelector('.tax-exemption-reason input');
                if (exemptionReasonInput && exemptionReasonInput.style.display !== 'none' &&
                    exemptionReasonInput.closest('.tax-exemption-reason').style.display !== 'none') {
                    const exemptionReasonValid = exemptionReasonInput.value && exemptionReasonInput.value.trim() !== '';
                    styleIfTouched(exemptionReasonInput, exemptionReasonValid);
                }
            });

            // Check if all required fields are valid
            const allFieldsValid = quantityValid && priceValid && classificationValid && descriptionValid && unitValid && taxValid;

            // Update row styling
            updateRowValidationStatus(row, allFieldsValid);

            console.log(`✅ Row validation: Quantity=${quantityValid}, Price=${priceValid}, Classification=${classificationValid}, Description=${descriptionValid}, Unit=${unitValid}, Tax=${taxValid}`);

            } catch (error) {
                console.error('❌ Error in validateRow:', error);
                console.warn('⚠️ Row validation failed, continuing without validation');
            }
        }

        function updateFieldStyling(field, isValid) {
            try {
                if (!field) return;

                if (isValid) {
                    field.classList.remove('is-invalid');
                    field.classList.add('is-valid');
                } else {
                    field.classList.remove('is-valid');
                    field.classList.add('is-invalid');
                }
                setFieldAriaValidation(field, isValid);
            } catch (error) {
                console.error('❌ Error in updateFieldStyling:', error, field);
            }
        }

        // Wires aria-invalid/aria-describedby for any field passed through updateFieldStyling() or
        // the generic blur handler, so an error is never communicated by color alone. Composes with
        // (rather than replaces) any existing aria-describedby reference — e.g. a field's own helper
        // text — and cleans up again once the field becomes valid. Fields with no adjacent
        // .invalid-feedback element (most dynamic invoice-item fields today only carry a border
        // color) simply get aria-invalid alone, which is still meaningful on its own.
        function setFieldAriaValidation(field, isValid) {
            try {
                if (!field) return;
                const feedback = field.parentElement ? field.parentElement.querySelector(':scope > .invalid-feedback') : null;

                if (!isValid) {
                    field.setAttribute('aria-invalid', 'true');
                    if (feedback) {
                        if (!feedback.id) {
                            feedback.id = `${field.id || field.name || 'field'}-error`.replace(/[^\w-]/g, '-');
                        }
                        const existingIds = (field.getAttribute('aria-describedby') || '').split(/\s+/).filter(id => id && id !== feedback.id);
                        field.setAttribute('aria-describedby', [...existingIds, feedback.id].join(' '));
                    }
                } else {
                    field.removeAttribute('aria-invalid');
                    if (feedback && feedback.id) {
                        const remainingIds = (field.getAttribute('aria-describedby') || '').split(/\s+/).filter(id => id && id !== feedback.id);
                        if (remainingIds.length) {
                            field.setAttribute('aria-describedby', remainingIds.join(' '));
                        } else {
                            field.removeAttribute('aria-describedby');
                        }
                    }
                }
            } catch (error) {
                console.error('❌ Error in setFieldAriaValidation:', error, field);
            }
        }

        // Focuses (and scrolls to) the first invalid field after a failed Next/Review/Submit
        // attempt. For a Select2-enhanced <select>, focuses its visible Select2 control instead of
        // the hidden native <select> — focusing the hidden element would be invisible to the user.
        function focusFirstInvalidField(field) {
            if (!field) return;
            try {
                let target = field;
                if (window.jQuery && jQuery.fn.select2 && jQuery(field).hasClass('select2-hidden-accessible')) {
                    const select2Selection = jQuery(field).next('.select2-container').find('.select2-selection')[0];
                    if (select2Selection) target = select2Selection;
                }
                target.scrollIntoView({ behavior: 'smooth', block: 'center' });
                target.focus({ preventScroll: true });
            } catch (error) {
                console.error('❌ Error in focusFirstInvalidField:', error, field);
            }
        }

        function updateRowValidationStatus(row, allFieldsValid) {
            // Update row visual status based on validation results
            if (allFieldsValid) {
                row.classList.remove('border-warning', 'border-danger');
                row.classList.add('border-success');
                row.style.borderWidth = '1px';
            } else {
                row.classList.remove('border-success');
                row.classList.add('border-warning');
                row.style.borderWidth = '2px';
            }
        }

        function validateItemRequirements() {
            console.log('🔍 Validating item requirements...');

            const itemRows = document.querySelectorAll('.item-row');
            const itemsWithMissingFields = [];
            const itemsWithEmptyTax = [];
            let firstInvalidField = null;

            // Clear previous visual indicators
            clearTaxVisualIndicators();

            itemRows.forEach((row, index) => {
                // Use the same validation logic as real-time validation
                const quantityInput = row.querySelector('.quantity-input');
                const priceInput = row.querySelector('.price-input');
                const itemDescriptionField = row.querySelector('textarea[name*=".ItemDescription"]');
                const classificationField = row.querySelector('select[name*=".ClassificationCode"]');
                const unitField = row.querySelector('select[name*=".UnitOfMeasure"]');
                const itemDescription = itemDescriptionField?.value;
                const classificationCode = classificationField?.value;
                const unitOfMeasure = unitField?.value;
                const taxRows = row.querySelectorAll('.tax-row');

                // Validate quantity and price
                const quantityValid = quantityInput && parseFloat(quantityInput.value) > 0;
                
                // All document types now require positive prices (negative symbol removed from UI)
                // Credit logic is handled by document type internally, not by negative values
                const priceValid = priceInput && parseFloat(priceInput.value) > 0;

                // Validate taxes
                let taxValid = false;
                taxRows.forEach(taxRow => {
                    const categorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
                    const percentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');

                    const categoryValue = categorySelect?.value;

                    // A rate is valid once it's present and non-negative — MyInvois allows a
                    // genuine 0% rate on any tax type, not just "Not Applicable"/"Exemption".
                    if (categoryValue && categoryValue !== '' && isTaxPercentageProvided(percentageInput)) {
                        const categoryText = categorySelect.options[categorySelect.selectedIndex]?.text || '';
                        const isExemption = categoryText.toLowerCase().includes('exemption');

                        if (isExemption) {
                            // Exemption category additionally requires its reason when shown
                            const exemptionReasonInput = taxRow.querySelector('.tax-exemption-reason input');
                            if (exemptionReasonInput && exemptionReasonInput.closest('.tax-exemption-reason').style.display !== 'none') {
                                if (exemptionReasonInput.value && exemptionReasonInput.value.trim() !== '') {
                                    taxValid = true;
                                }
                            } else {
                                taxValid = true;
                            }
                        } else {
                            taxValid = true;
                        }
                    }
                });

                let hasIssues = false;

                // Check if any required field is missing
                if (!quantityValid || !priceValid || !itemDescription || !classificationCode || !unitOfMeasure) {
                    itemsWithMissingFields.push(index + 1);
                    hasIssues = true;
                    if (!firstInvalidField) {
                        firstInvalidField = (!quantityValid && quantityInput) || (!priceValid && priceInput) ||
                            (!itemDescription && itemDescriptionField) || (!classificationCode && classificationField) ||
                            (!unitOfMeasure && unitField) || null;
                    }
                }

                // Check tax requirements
                if (!taxValid) {
                    itemsWithEmptyTax.push(index + 1);
                    hasIssues = true;
                    if (!firstInvalidField) {
                        firstInvalidField = row.querySelector('.tax-row select[name*=".TaxCategory"]');
                    }
                }

                // Add visual indicators using the same logic as real-time validation
                if (hasIssues) {
                    row.classList.add('border-warning');
                    row.style.borderWidth = '2px';
                } else {
                    row.classList.add('border-success');
                    row.style.borderWidth = '1px';
                }
            });

            if (itemsWithMissingFields.length > 0) {
                const itemList = itemsWithMissingFields.join(', ');
                ErrorHandler.show(`Items ${itemList} have missing required fields (Quantity, Unit Price, Description, Classification, or Unit of Measure).`, 'warning');
                focusFirstInvalidField(firstInvalidField);
                return false;
            }

            if (itemsWithEmptyTax.length > 0) {
                const itemList = itemsWithEmptyTax.join(', ');
                ErrorHandler.show(`Items ${itemList} have empty or incomplete tax entries. Please fill in the tax category and percentage.`, 'warning');
                focusFirstInvalidField(firstInvalidField);
                return false;
            }

            ErrorHandler.show('All items have properly filled required fields and taxes! ✅', 'success');
            return true;
        }

        function clearTaxVisualIndicators() {
            const itemRows = document.querySelectorAll('.item-row');
            itemRows.forEach(row => {
                row.classList.remove('border-warning', 'border-danger', 'border-success');
                row.style.borderWidth = '';
            });
        }

        function enforceLineLength(textarea, maxLength) {
            let lines = textarea.value.split(/\r?\n/);

            // Check if any line actually exceeds the max length.
            // If not, DO NOTHING. This allows the user to type spaces normally.
            let needsFormat = lines.some(line => line.length > maxLength);
            if (!needsFormat) return;

            let start = textarea.selectionStart;
            let formattedLines = [];

            for (let i = 0; i < lines.length; i++) {
                let line = lines[i];

                // If the line is too long, wrap it to the next line safely
                while (line.length > maxLength) {
                    let slice = line.substring(0, maxLength);
                    let lastSpaceIndex = slice.lastIndexOf(' ');

                    // If there's no space to break at, just hard break at maxLength
                    if (lastSpaceIndex === -1) {
                        formattedLines.push(line.substring(0, maxLength));
                        line = line.substring(maxLength);
                    } else {
                        // Break nicely at the last space
                        formattedLines.push(line.substring(0, lastSpaceIndex));
                        line = line.substring(lastSpaceIndex + 1);
                    }
                }
                formattedLines.push(line);
            }

            let newText = formattedLines.join('\n');

            if (textarea.value !== newText) {
                textarea.value = newText;
                // Keep cursor position stable
                textarea.setSelectionRange(start, start);
            }
        }

        // Main Invoice Manager - Module Pattern
        const InvoiceManager = {
            init() {
                this.initializeComponents();
                this.bindEventListeners();
                this.loadInitialData();
                this.setupDebugMode();
            },

            initializeComponents() {
                this.initializeTooltips();
                this.initializeFormValidation();
                this.initializeCalculations();
                this.initializeDatePickers();
                this.initializeDropdowns();
                this.initializeExistingItems();
                this.updateProgress();
                this.updateExchangeRate();
                this.updateSummary();
                setDefaultCurrency();
            },

            bindEventListeners() {
                this.bindSupplierChangeHandler();
                this.bindBuyerChangeHandler();
                this.bindDocTypeChangeHandler();
                this.bindFormSubmission();
                this.bindItemManagement();
                this.bindTaxHandling();
            },

        loadInitialData() {
            const initialSupplierId = document.getElementById('supplierSelect')?.value;

        if (initialSupplierId) {

               // 1️⃣ Load supplier details
               loadPartyDetails(initialSupplierId, 'PI', 'supplier');

               // 2️⃣ 🔥 ALSO load customers for that supplier
               console.log("🔥 Loading customers on page load...");
               loadCustomersForSupplier(initialSupplierId);
           }

            const buyerValue = document.getElementById('buyerSelect')?.value;
            const attentionInput = document.getElementById('attention');

            if (buyerValue && (!attentionInput || !attentionInput.value.trim())) {

                const parts = buyerValue.split('_');

                if (parts.length === 2) {
                    const type = parts[0];   // "PI" or "PC"
                    const id = parts[1];     // numeric id

                    loadPartyDetails(id, type, 'buyer');
                } else {
                    console.warn("⚠️ Invalid buyer value format:", buyerValue);
                }
            }

            // Force initial calculation after everything is loaded
            setTimeout(() => {
                calculateTotals();
                updateSummary();
                document.querySelectorAll('.quantity-input, .price-input').forEach(input => {
                    input.dispatchEvent(new Event('input'));
                });
            }, 100);
        },


            setupDebugMode() {
                document.getElementById('debugMode')?.addEventListener('change', toggleDebug);
                console.log("🔄 Auto-loading party details on page load...");
            },


            initializeTooltips() {
                var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
                var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
                    return new bootstrap.Tooltip(tooltipTriggerEl);
                });
            },

            initializeDropdowns() {
                if (typeof $ !== 'undefined' && $.fn.select2) {
                    // Add delay to ensure ASP.NET Core asp-items have rendered
                    setTimeout(() => {
                        console.log('🔄 Initializing Select2 after delay to ensure options are rendered...');
                        
                        // Initialize Select2 for existing dropdowns
                        $('.select2').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: function() {
                            return $(this).data('placeholder') || 'Select an option';
                        },
                        allowClear: true
                    });

                    // Supplier dropdown
                    $('#supplierSelect').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Supplier',
                        allowClear: true
                    });

                    // Buyer dropdown
                    $('#buyerSelect').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Buyer',
                        allowClear: true
                    });

                    // Classification Code dropdowns - Make searchable
                    $('select[name*=".ClassificationCode"]').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Classification Code',
                        allowClear: true,
                        minimumResultsForSearch: 0 // Always show search box
                    });

                    // Tax Category dropdowns - Make searchable
                    $('select[name*=".TaxCategory"]').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Tax Category',
                        allowClear: true,
                        minimumResultsForSearch: 0 // Always show search box
                    });

                    // Unit of Measure dropdowns - Make searchable
                    $('select[name*=".UnitOfMeasure"]').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Unit Measurement',
                        allowClear: true,
                        minimumResultsForSearch: 0 // Always show search box
                    });

                    // Country of Origin dropdowns (line-level Additional Information) - Make searchable
                    $('select[name*=".CountryOfOrigin"]').select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Country',
                        allowClear: true,
                        minimumResultsForSearch: 0 // Always show search box
                    });

                    console.log('✅ Select2 initialization completed after delay');
                    
                    // Re-bind document type change handler after Select2 initialization
                    setTimeout(() => {
                        console.log('🔄 Re-binding document type handler after Select2 initialization...');
                        const docTypeSelect = document.getElementById('docTypeCode');
                        if (docTypeSelect && typeof $ !== 'undefined' && $.fn.select2) {
                            $(docTypeSelect).off('change.doctype').on('change.doctype', function() {
                                const docTypeCode = this.value;
                                console.log(`🔄 [Select2 Post-Init] Document Type changed to: ${docTypeCode}`);
                                
                                const isSelfBilled = ['11', '12', '13', '14'].includes(docTypeCode);
                                if (isSelfBilled) {
                                    console.log(`📋 Self-billed detected - updating classification codes to 004`);
                                    setTimeout(() => updateAllClassificationCodes(true), 100);
                                } else {
                                    console.log(`📋 Regular document detected - re-enabling classification code dropdowns`);
                                    setTimeout(() => updateAllClassificationCodes(true), 100);
                                }
                                
                                // Filter supplier dropdown based on document type  
                        filterSupplierOptions(docTypeCode);
                        // Always refresh buyer dropdown when document type changes
                                const selectedSupplier = document.getElementById('supplierSelect')?.value;
                                if (selectedSupplier) {
                                    console.log(`🔄 [Select2] Refreshing buyer dropdown for supplier ${selectedSupplier} with docType ${docTypeCode}`);
                                    setTimeout(() => loadCustomersForSupplier(selectedSupplier), 500);
                                } else {
                                    // Clear buyer dropdown when no supplier is selected
                                    const buyerSelect = document.getElementById('buyerSelect');
                                    if (buyerSelect) {
                                        buyerSelect.innerHTML = '<option value="">Select Buyer</option>';
                                        console.log(`🔄 [Select2] Cleared buyer dropdown for docType change to ${docTypeCode}`);
                                    }
                                }
                                updateSummary();
                            });
                            console.log('✅ Document Type Select2 handler re-bound after initialization');
                        }
                    }, 50);
                    
                }, 200); // 200ms delay to ensure asp-items rendering is complete
                }
            },

            bindDocTypeChangeHandler() {
                const docTypeSelect = document.getElementById('docTypeCode');
                if (docTypeSelect) {
                    console.log('🔗 Binding Document Type change handler...');
                    
                    // Regular DOM change event
                    docTypeSelect.addEventListener("change", function () {
                        const docTypeCode = this.value;
                        console.log(`🔄 [DOM Event] Document Type changed to: ${docTypeCode}`);
                        handleDocTypeChange(docTypeCode);
                    });
                    
                    // Select2 change event (if Select2 is used)
                    if (typeof $ !== 'undefined' && $.fn.select2) {
                        $(docTypeSelect).on('change', function() {
                            const docTypeCode = this.value;
                            console.log(`🔄 [Select2 Event] Document Type changed to: ${docTypeCode}`);
                            handleDocTypeChange(docTypeCode);
                        });
                        console.log('✅ Select2 change handler bound');
                    }
                    
                    // Handle document type change logic
                    function handleDocTypeChange(docTypeCode) {
                        console.log(`🔄 Processing document type change: ${docTypeCode}`);
                        
                        // Auto-update classification codes only for self-billed documents
                        const isSelfBilled = ['11', '12', '13', '14'].includes(docTypeCode);
                        
                        if (isSelfBilled) {
                            console.log(`📋 Self-billed document detected - Auto-setting classification codes to: 004`);
                            
                            // Use the helper function with a small delay
                            setTimeout(() => {
                                updateAllClassificationCodes(true);
                            }, 100);
                        } else {
                            console.log(`📋 Regular document - Re-enabling classification code dropdowns for user selection`);
                            // For regular documents, re-enable classification code dropdowns
                            setTimeout(() => {
                                updateAllClassificationCodes(true);
                            }, 100);
                        }
                        
                        // Update price input validation based on document type
                        updatePriceInputValidation(docTypeCode);
                        
                        // Filter supplier dropdown based on document type  
                        filterSupplierOptions(docTypeCode);
                        // Always refresh buyer dropdown when document type changes
                        const selectedSupplier = document.getElementById('supplierSelect')?.value;
                        if (selectedSupplier) {
                            console.log(`🔄 Refreshing buyer dropdown for supplier ${selectedSupplier} with docType ${docTypeCode}`);
                            setTimeout(() => loadCustomersForSupplier(selectedSupplier), 500);
                        } else {
                            // Clear buyer dropdown when no supplier is selected
                            const buyerSelect = document.getElementById('buyerSelect');
                            if (buyerSelect) {
                                buyerSelect.innerHTML = '<option value="">Select Buyer</option>';
                                console.log(`🔄 Cleared buyer dropdown for docType change to ${docTypeCode}`);
                            }
                        }
                        
                        // Handle RefUUID field visibility for all adjustment document types
                        // Document type 11 = Self-billed Invoice (original, no RefUUID)
                        // Document types 12,13,14 = Self-billed adjustments (need RefUUID to reference type 11)
                        const refUUIDSection = document.getElementById('refUUIDSection');
                        const refUUIDRequiredTypes = ['02', '03', '04', '12', '13', '14']; // All adjustment types need RefUUID
                        const isRefUUIDRequired = refUUIDRequiredTypes.includes(docTypeCode);
                        
                        if (isRefUUIDRequired) {
                            const docTypeNames = {
                                '02': 'Credit Note', '03': 'Debit Note', '04': 'Refund Note',
                                '12': 'Self-Billed Credit Note', '13': 'Self-Billed Debit Note', '14': 'Self-Billed Refund Note'
                            };
                            console.log(`📋 ${docTypeNames[docTypeCode]} detected via dropdown change - Showing RefUUID field`);
                            refUUIDSection.style.display = 'block';
                            
                            // For manual document type changes, always show select mode (user is creating manually)
                            const refUUIDDisplayMode = document.getElementById('refUUIDDisplayMode');
                            const refUUIDSelectMode = document.getElementById('refUUIDSelectMode');
                            
                            console.log(`🔄 Manual ${docTypeNames[docTypeCode]} selection - switching to select mode`);
                            refUUIDDisplayMode.style.display = 'none';
                            refUUIDSelectMode.style.display = 'block';
                            
                            // Clear any preset values since this is manual selection
                            const refUUIDDisplay = document.getElementById('refUUIDDisplay');
                            const refUUIDHidden = document.getElementById('refUUIDHidden');
                            if (refUUIDDisplay) refUUIDDisplay.value = '';
                            if (refUUIDHidden) refUUIDHidden.value = '';
                            
                            // Load available invoices for reference
                            loadAvailableInvoicesForReference();
                        } else {
                            console.log(`📋 Regular document - Hiding RefUUID field`);
                            refUUIDSection.style.display = 'none';
                            
                            // Clear both modes
                            const refUUIDSelect = document.getElementById('refUUIDSelect');
                            const refUUIDDisplay = document.getElementById('refUUIDDisplay');
                            const refUUIDHidden = document.getElementById('refUUIDHidden');
                            
                            if (refUUIDSelect) {
                                refUUIDSelect.innerHTML = '<option value="">Select original invoice</option>';
                            }
                            if (refUUIDDisplay) {
                                refUUIDDisplay.value = '';
                            }
                            if (refUUIDHidden) {
                                refUUIDHidden.value = '';
                            }
                        }
                        
                        updateSummary();
                    }
                    
                    console.log('✅ Document Type change handlers bound successfully');
                }
            },

            bindFormSubmission() {
                const invoiceForm = document.getElementById("invoice_form");
                const handlerInput = document.getElementById("handler");
                const actionInput = document.getElementById("invoiceAction");

                // Save Draft
                document.querySelector('button[name="action"][value="saveDraft"]')?.addEventListener("click", (e) => {
                    e.preventDefault(); // Prevent form submission

                    // Show loading state
                    const saveBtn = document.querySelector('button[name="action"][value="saveDraft"]');
                    const originalText = saveBtn.innerHTML;
                    saveBtn.disabled = true;
                    saveBtn.innerHTML = '<i class="ri-loader-4-line me-1 spinner-border spinner-border-sm"></i>Saving...';

                    // Get form data
                    const formData = new FormData(invoiceForm);
                    formData.set('action', 'saveDraft');
                    formData.delete('handler'); // Remove handler since we're using action

                    // AJAX call to save draft
                    $.ajax({
                        url: '/Invoices/CreateInvoice',
                        method: 'POST',
                        data: formData,
                        dataType: 'json',
                        processData: false,
                        contentType: false,
                        timeout: 15000,
                        success: function(response) {
                            // Update button text based on whether this was an update or new draft
                            const updatedButtonText = response.isUpdate 
                                ? '<i class="ri-save-line me-1"></i>Update Draft'
                                : '<i class="ri-save-line me-1"></i>Save as Draft';
                            
                            // Update the invoice number field if returned from server
                            if (response.invoiceNo) {
                                const invoiceNoInput = document.querySelector('input[name="Invoice.InvoiceNo"]');
                                if (invoiceNoInput) {
                                    invoiceNoInput.value = response.invoiceNo;
                                    console.log(`📝 Invoice number updated to: ${response.invoiceNo}`);
                                }
                            }
                            
                            // Restore button state
                            saveBtn.disabled = false;
                            saveBtn.innerHTML = updatedButtonText;

                            if (response.success) {
                                // Update draft file path in the hidden field
                                if (response.draftPath) {
                                    document.getElementById("draftFilePath").value = response.draftPath;
                                }

                                // Enable Submit to LHDN button
                                const submitBtn = document.getElementById("sa-success-submit-lhdn");
                                if (submitBtn) {
                                    submitBtn.disabled = false;
                                }

                                // Refresh the readiness checklist so "Draft Saved" flips green immediately
                                InvoiceManager.updateSummary();

                                // Show success message using SweetAlert
                                const successTitle = response.isUpdate ? 'Draft Updated Successfully!' : 'Draft Saved Successfully!';
                                Swal.fire({
                                    icon: 'success',
                                    title: successTitle,
                                    text: `Draft saved for Invoice ${response.invoiceNo || 'N/A'}`,
                                    confirmButtonColor: '#006948',  // eInvWorld brand primary green
                                    timer: 2500,
                                    timerProgressBar: true
                                });
                            } else {
                                ErrorHandler.show(response.message || 'Failed to save draft. Please try again.', 'error');
                            }
                        },
                        error: function(xhr, status, error) {
                            // Restore button state
                            saveBtn.disabled = false;
                            saveBtn.innerHTML = originalText;

                            let errorMessage = 'Failed to save draft. Please try again.';
                            if (xhr.responseJSON && xhr.responseJSON.message) {
                                errorMessage = xhr.responseJSON.message;
                            }
                            ErrorHandler.show(errorMessage, 'error');
                        }
                    });
                });

                // Submit to LHDN
                document.getElementById("sa-success-submit-lhdn")?.addEventListener("click", (e) => {
                    e.preventDefault(); // Prevent form submission

                    const draftPath = document.getElementById("draftFilePath").value;
                    if (!draftPath) {
                        ErrorHandler.show('You must save the draft before submitting to LHDN.', 'warning');
                        return;
                    }

                    const invoiceNo = document.getElementById("invoiceNo").value;
                    if (!invoiceNo) {
                        ErrorHandler.show('Invoice number is missing.', 'error');
                        return;
                    }

                    // Show loading state
                    const submitBtn = document.getElementById("sa-success-submit-lhdn");
                    const originalText = submitBtn.innerHTML;
                    submitBtn.disabled = true;
                    submitBtn.innerHTML = '<i class="ri-loader-4-line me-1 spinner-border spinner-border-sm"></i>Submitting...';

                    // AJAX call to submit to LHDN
                    $.ajax({
                        url: '/Invoices/CreateInvoice?handler=SubmitDocuments',
                        method: 'POST',
                        data: {
                            invoiceNo: invoiceNo,
                            isAjax: true
                        },
                        headers: {
                            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                        },
                        timeout: 30000,
                        success: function(response) {
                            // Restore button state
                            submitBtn.disabled = false;
                            submitBtn.innerHTML = originalText;

                            if (response.success) {
                                // Prepare detailed HTML message with submission details
                                let htmlMessage = `<div class="text-center mb-3">
                                    <i class="ri-checkbox-circle-line text-success" style="font-size: 2rem;"></i>
                                    <p class="mt-2 mb-0 fw-bold">${response.message || 'Invoice has been successfully submitted to LHDN!'}</p>
                                </div>`;

                                // Add submission details if available
                                if (response.submissionUid || response.uuid) {
                                    htmlMessage += '<div class="submission-details mt-3">';
                                    htmlMessage += '<h6 class="text-muted mb-2"><i class="ri-information-line me-1"></i>Submission Details:</h6>';

                                    if (response.submissionUid) {
                                        htmlMessage += `<div class="detail-item mb-2">
                                            <strong>Submission UID:</strong>
                                            <button class="copy-btn float-end" onclick="copyToClipboard('${response.submissionUid}', this)" title="Copy Submission UID">
                                                <i class="ri-file-copy-line"></i>
                                            </button>
                                            <br>
                                            <code class="text-primary">${response.submissionUid}</code>
                                        </div>`;
                                    }

                                    if (response.uuid) {
                                        htmlMessage += `<div class="detail-item mb-2">
                                            <strong>UUID:</strong>
                                            <button class="copy-btn float-end" onclick="copyToClipboard('${response.uuid}', this)" title="Copy UUID">
                                                <i class="ri-file-copy-line"></i>
                                            </button>
                                            <br>
                                            <code class="text-info">${response.uuid}</code>
                                        </div>`;
                                    }

                                    htmlMessage += '</div>';
                                }

                                // Show success message using SweetAlert
                                Swal.fire({
                                    icon: 'success',
                                    title: 'Submission Successful!',
                                    html: htmlMessage,
                                    confirmButtonColor: '#006948',  // eInvWorld brand primary green
                                    confirmButtonText: 'Continue',
                                    timer: 5000,
                                    timerProgressBar: true,
                                    customClass: {
                                        popup: 'swal-wide swal-submission-success'
                                    },
                                    showClass: {
                                        popup: 'animate__animated animate__fadeInDown'
                                    }
                                }).then(() => {
                                    // Redirect to invoice list after success
                                    window.location.href = '/Invoices/InvoiceLists?refresh=true&invoiceDirection=Sent&timestamp=' + Date.now();
                                });
                            } else {
                                ErrorHandler.show(response.message || 'Submission failed. Please try again.', 'error');
                            }
                        },
                        error: function(xhr, status, error) {
                            // Restore button state
                            submitBtn.disabled = false;
                            submitBtn.innerHTML = originalText;

                            let errorMessage = 'Failed to submit invoice to LHDN. Please try again.';
                            if (xhr.responseJSON && xhr.responseJSON.message) {
                                errorMessage = xhr.responseJSON.message;
                            }
                            ErrorHandler.show(errorMessage, 'error');
                        }
                    });
                });

                // Generic form action safety
                invoiceForm?.addEventListener('submit', function(event) {
                    if (typeof invoiceForm.action !== 'string' || invoiceForm.action.includes('[object')) {
                        invoiceForm.removeAttribute('action');
                    }
                });

                // Submit button handling - disable if no draft
                const submitBtn = document.getElementById("sa-success-submit-lhdn");
                if (!document.getElementById("draftFilePath").value) {
                    submitBtn.disabled = true;
                }
            },

            bindItemManagement() {
                const addItemBtn = document.getElementById('addItemBtn');
                if (addItemBtn) {
                    console.log('🔗 Binding Add Item button event listener');

                    // Add the event listener directly to the existing button
                    addItemBtn.addEventListener('click', function(e) {
                        e.preventDefault();
                        e.stopPropagation();
                        console.log('🖱️ Add Item button clicked (InvoiceManager)');
                        addItemRow();
                    });

                    console.log('✅ Add Item button event listener bound successfully');
                } else {
                    console.warn('⚠️ Add Item button not found');
                }
            },

            bindTaxHandling() {
                document.addEventListener('change', function(e) {
                    console.log('🎯 Global change event:', e.target.tagName, e.target.classList.toString());
                    if (e.target.classList.contains('tax-category')) {
                        console.log('✅ Tax category change detected globally');
                        toggleExemptionReason(e.target);
                    }
                });

                // Global Select2 event listener for tax categories
                $(document).on('select2:select', '.tax-category', function(e) {
                    console.log('🎯 Global Select2 event triggered');
                    toggleExemptionReason(this);
                });
            },

            bindSupplierChangeHandler() {
                const supplierSelect = document.getElementById("supplierSelect");
                if (supplierSelect) {
                    supplierSelect.addEventListener("change", function () {
                        const supplierId = this.value;
                        console.log(`🔄 Supplier changed to ID: ${supplierId}`);
                        updateDebugInfo('supplierId', supplierId);

                        if (supplierId) {
                            // Load customers for this supplier
                            loadCustomersForSupplier(supplierId);

                            // Load supplier details for additional fields
                            loadPartyDetails(supplierId, 'PI', 'supplier');
                            
                            // Load available invoices for RefUUID (if Credit Note is selected)
                            const docTypeSelect = document.getElementById('docTypeCode');
                            const isCreditNote = docTypeSelect?.value === '02';
                            if (isCreditNote) {
                                console.log(`🔄 Supplier changed for Credit Note - Reloading RefUUID options`);
                                loadAvailableInvoicesForReference();
                            }
                        } else {
                            // Clear buyer dropdown when no supplier is selected
                            const buyerSelect = document.getElementById('buyerSelect');
                            if (buyerSelect) {
                                buyerSelect.innerHTML = '<option value="">Select Buyer</option>';
                                if (typeof $ !== 'undefined' && $.fn.select2) {
                                    $('#buyerSelect').trigger('change');
                                }
                            }

                            // Clear bank account fields
                            clearBankAccountFields();
                            
                            // Clear RefUUID dropdown when supplier is cleared
                            const refUUIDSelect = document.getElementById('refUUIDSelect');
                            if (refUUIDSelect) {
                                refUUIDSelect.innerHTML = '<option value="">Select supplier first</option>';
                            }
                        }
                        updateSummary();
                    });
                }
            },

        bindBuyerChangeHandler() {
            const buyerSelect = document.getElementById('buyerSelect');
            if (buyerSelect) {
                // Function to handle the change logic
                const handleBuyerChange = function(value) {
                    lastSelectedBuyerId = value; // Store the selected value
                    console.log(`🔄 Buyer changed to ID: ${lastSelectedBuyerId}`);
                    updateDebugInfo('buyerId', lastSelectedBuyerId);

                    if (lastSelectedBuyerId) {
                        const parts = lastSelectedBuyerId.split('_');

                        if (parts.length === 2) {
                            const type = parts[0];   // PI or PC
                            const id = parts[1];
                            loadPartyDetails(id, type, 'buyer');
                        } else {
                            console.warn("⚠️ Invalid buyer format:", lastSelectedBuyerId);
                        }
                    } else {
                        // Clear attention field when no buyer is selected
                        $('#attention').val('');
                        hideAutoPopulatedIndicator('attention');
                    }
                    updateSummary();
                };

                // 1. Bind native event
                buyerSelect.addEventListener("change", function() {
                    handleBuyerChange(this.value);
                });

                // 2. Bind jQuery/Select2 event (Critical for Select2)
                if (typeof $ !== 'undefined') {
                    $(buyerSelect).on('select2:select change', function(e) {
                        // 'this.value' works for both select2:select and standard change
                        handleBuyerChange(this.value);
                    });
                }

                console.log('✅ Buyer change handler bound (Native + Select2)');
            } else {
                console.error('❌ Buyer select element (#buyerSelect) not found!');
            }
        },

            initializeFormValidation() {
            const form = document.getElementById('invoice_form');

            form.addEventListener('submit', function(event) {
                    console.log('🔍 Validating form submission...');

                    // Check if form is valid
                if (!form.checkValidity()) {
                    event.preventDefault();
                    event.stopPropagation();
                        ErrorHandler.show('Please check all required fields and try again.', 'warning');
                        focusFirstInvalidField(form.querySelector(':invalid'));
                        return;
                    }

                    // Validate that each item has required fields filled
                    const itemRows = document.querySelectorAll('.item-row');
                    const itemsWithMissingFields = [];
                    const itemsWithEmptyTax = [];
                    let firstInvalidField = null;

                    itemRows.forEach((row, index) => {
                        const itemIndex = row.getAttribute('data-item-index');

                        // Check required item fields
                        const quantityInput = row.querySelector('input[name*=".Quantity"]');
                        const unitPriceInput = row.querySelector('input[name*=".UnitPrice"]');
                        const itemDescriptionField = row.querySelector('textarea[name*=".ItemDescription"]');
                        const classificationField = row.querySelector('select[name*=".ClassificationCode"]');
                        const unitField = row.querySelector('select[name*=".UnitOfMeasure"]');

                        // Check if any required field is missing
                        if (!quantityInput?.value || !unitPriceInput?.value || !itemDescriptionField?.value || !classificationField?.value || !unitField?.value) {
                            itemsWithMissingFields.push(index + 1);
                            if (!firstInvalidField) {
                                firstInvalidField = (!quantityInput?.value && quantityInput) || (!unitPriceInput?.value && unitPriceInput) ||
                                    (!itemDescriptionField?.value && itemDescriptionField) || (!classificationField?.value && classificationField) ||
                                    (!unitField?.value && unitField) || null;
                            }
                        }

                        // Check tax requirements
                        const taxSection = document.getElementById(`taxes-${itemIndex}`);
                        const taxRows = taxSection?.querySelectorAll('.tax-row') || [];

                        if (taxRows.length === 0) {
                            itemsWithEmptyTax.push(index + 1);
                            if (!firstInvalidField) firstInvalidField = taxSection;
                        } else {
                            // Check if any tax row is properly filled
                            let hasValidTax = false;
                            let firstInvalidTaxField = null;
                            taxRows.forEach(taxRow => {
                                const taxCategory = taxRow.querySelector('select[name*=".TaxCategory"]')?.value;
                                const taxPercentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');

                                if (taxCategory && isTaxPercentageProvided(taxPercentageInput)) {
                                    hasValidTax = true;
                                } else if (!firstInvalidTaxField) {
                                    firstInvalidTaxField = taxRow.querySelector('select[name*=".TaxCategory"]');
                                }
                            });

                            if (!hasValidTax) {
                                itemsWithEmptyTax.push(index + 1);
                                if (!firstInvalidField) firstInvalidField = firstInvalidTaxField;
                            }
                        }
                    });

                    if (itemsWithMissingFields.length > 0) {
                        event.preventDefault();
                        event.stopPropagation();

                        const itemList = itemsWithMissingFields.join(', ');
                        ErrorHandler.show(`Items ${itemList} have missing required fields (Quantity, Unit Price, Description, Classification, or Unit of Measure). Please fill in all required fields.`, 'warning');
                        focusFirstInvalidField(firstInvalidField);
                        return;
                    }

                    if (itemsWithEmptyTax.length > 0) {
                        event.preventDefault();
                        event.stopPropagation();

                        const itemList = itemsWithEmptyTax.join(', ');
                        ErrorHandler.show(`Items ${itemList} have empty or incomplete tax entries. Please fill in the tax category and percentage for these items.`, 'warning');
                        focusFirstInvalidField(firstInvalidField);
                        return;
                    }

                    console.log('✅ Form validation passed - all items have taxes');
                form.classList.add('was-validated');
            });

            // Real-time validation
            form.querySelectorAll('input, select, textarea').forEach(field => {
                const revalidate = function() {
                    const isValid = !(this.hasAttribute('required') && !this.value.trim());
                    if (!isValid) {
                        this.classList.add('is-invalid');
                    } else {
                        this.classList.remove('is-invalid');
                    }
                    setFieldAriaValidation(this, isValid);
                };
                field.addEventListener('blur', revalidate);
                // Select2 (used for buyer/supplier/document-type/etc.) commits a selection via
                // jQuery's `.trigger('change')`, which — unlike a real native change event — is only
                // ever delivered to jQuery-bound handlers, never to a plain addEventListener('change',
                // ...) listener. Bind through jQuery too so a Select2 pick is actually caught;
                // otherwise the field stayed marked invalid until blurred some other way.
                if (window.jQuery) {
                    jQuery(field).on('change', revalidate);
                } else {
                    field.addEventListener('change', revalidate);
                }
            });
            },

            initializeCalculations() {
            // 1. Prevent scientific notation characters (e, E, +, -) from being typed
            document.addEventListener('keydown', function(e) {
                if (e.target.classList.contains('price-input')) {
                    if (['e', 'E', '+', '-'].includes(e.key)) {
                        e.preventDefault();
                    }
                }
            });

            // 2. Auto-format to exactly 2 decimal places when the user clicks away (blur/focusout)
            document.addEventListener('focusout', function(e) {
                if (e.target.classList.contains('price-input')) {
                    if (e.target.value) {
                        let val = parseFloat(e.target.value);
                        if (!isNaN(val)) {
                            e.target.value = val.toFixed(2);
                            calculateSubtotal(e.target); // Recalculate to ensure accuracy
                        }
                    }
                }
            });

            // Use event delegation for dynamically added elements
            document.addEventListener('input', function(e) {
                if (e.target.classList.contains('quantity-input') || e.target.classList.contains('price-input')) {
                    calculateSubtotal(e.target);
                        validateField(e.target); // Add real-time validation
                }
                // Trigger totals recalculation when tax percentage changes
                if (e.target.matches('.tax-row input[type="number"]')) {
                    calculateTotals();
                    updateSummary();
                        validateTaxField(e.target); // Add tax validation
                    }
                    // Validate other required fields on input
                    if (e.target.name && (e.target.name.includes('ItemDescription'))) {
                        enforceLineLength(e.target, 56);
                        validateField(e.target);
                    }
                });

                // Add validation for dropdown selections. Delegated through jQuery, not a plain
                // addEventListener, because these are Select2-enhanced selects: Select2 commits a
                // pick via jQuery's `.trigger('change')`, which a vanilla document-level 'change'
                // listener never receives (jQuery's synthetic trigger doesn't go through native DOM
                // dispatch) — without this, picking Tax Category/Classification/Unit left the field
                // marked invalid until something else happened to blur it.
                if (window.jQuery) {
                    jQuery(document).on('change', 'select[name*="TaxCategory"], select[name*="ClassificationCode"], select[name*="UnitOfMeasure"]', function() {
                        validateField(this);
                    });
                } else {
                    document.addEventListener('change', function(e) {
                        if (e.target.name && (e.target.name.includes('TaxCategory') || e.target.name.includes('ClassificationCode') || e.target.name.includes('UnitOfMeasure'))) {
                            validateField(e.target);
                        }
                    });
                }

            // Calculate initial totals
            calculateTotals();
            },

            initializeDatePickers() {
                const issueDateInput = document.getElementById("issueDate");
                const startInput = document.getElementById("startDate");
                const endInput = document.getElementById("endDate");

                // Issue date picker
                if (issueDateInput) {
                    flatpickr(issueDateInput, {
                        dateFormat: "Y-m-d H:i",
                        enableTime: true,
                        time_24hr: false,
                        defaultDate: issueDateInput.value || new Date(),
                        maxDate: new Date(), // today
                        minDate: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000), // 3 days ago
                        onChange: function (selectedDates) {
                            if (selectedDates.length > 0) {
                                updateSummary();
                            }
                        }
                    });
                }

                // Start date picker
                if (startInput) {
                    flatpickr(startInput, {
                        dateFormat: "d-M-Y",
                        defaultDate: startInput.value || null,
                        onChange: function (selectedDates) {
                            if (selectedDates.length > 0 && endInput) {
                                // Update end date picker min date
                                const endPicker = endInput._flatpickr;
                                if (endPicker) {
                                    endPicker.set("minDate", selectedDates[0]);
                                }
                            }
                        }
                    });
                }

                // End date picker
                if (endInput) {
                    flatpickr(endInput, {
                        dateFormat: "d-M-Y",
                        defaultDate: endInput.value || null,
                        minDate: startInput?.value || new Date()
                    });
                }
            },

            initializeExistingItems() {
                // Add event listeners to existing quantity and price inputs
                document.querySelectorAll('.quantity-input, .price-input').forEach(input => {
                    input.addEventListener('input', () => calculateSubtotal(input));
                });

                // Initialize existing tax category handlers
                document.querySelectorAll('.tax-category').forEach(select => {
                    select.addEventListener('change', () => toggleExemptionReason(select));
                });

                // Wire the Discount/Fee "Rate %" convenience inputs and Amount inputs for every
                // server-rendered row (dynamically-added rows wire this themselves in addItemRow).
                bindDiscountFeeRateInputs(document);

                // Set default values for existing inputs to trigger calculations
                document.querySelectorAll('.quantity-input').forEach(input => {
                    if (!input.value || input.value === '') {
                        input.value = '0';
                    }
                });

                document.querySelectorAll('.price-input').forEach(input => {
                    if (!input.value || input.value === '') {
                        input.value = '0';
                    }
                });

                // Calculate initial totals
                calculateTotals();

                // Deliberately no onload validateRow() pass here: a page load is not a submission
                // attempt or user interaction, so per the untouched-until-interacted validation
                // model (see the validation-timing fix), rows stay visually neutral until the user
                // actually touches a field or clicks Next/Submit — otherwise every row (blank or an
                // incomplete loaded draft) would show red the instant the page renders.
            },

            updateProgress() {
                const progress = (currentStep / totalSteps) * 100;
                document.getElementById('formProgress').style.width = progress + '%';
                document.querySelectorAll('.ci-step-node').forEach(node => {
                    const step = parseInt(node.dataset.step, 10);
                    node.classList.toggle('is-active', step === currentStep);
                    node.classList.toggle('is-complete', step < currentStep);
                });
            },

            updateExchangeRate() {
                const currency = document.getElementById('currency').value;
                const foreignCurrency = document.getElementById('foreignCurrency');

                // Set ForeignCurrency to same as Currency
                if (currency) {
                    foreignCurrency.value = currency;
                }

                if (currency && currency !== 'MYR') {
                    // You can implement API call to get real exchange rates here
                    document.getElementById('exchangeRate').value = '1.00';
                } else {
                    document.getElementById('exchangeRate').value = '1.00';
                }
            },

            updateSummary() {
                const docType = document.getElementById('docTypeCode');
                const supplier = document.getElementById('supplierSelect');
                const buyer = document.getElementById('buyerSelect');
                const issueDate = document.getElementById('issueDate');
                const paymentTerms = document.getElementById('paymentTerms');
                const billingFrequency = document.querySelector('select[name="Invoice.InvoicePeriod"]');

                document.getElementById('summaryDocType').textContent = docType.options[docType.selectedIndex]?.text || '-';
                document.getElementById('summarySupplier').textContent = supplier.options[supplier.selectedIndex]?.text || '-';
                document.getElementById('summaryBuyer').textContent = buyer.options[buyer.selectedIndex]?.text || '-';
                document.getElementById('summaryIssueDate').textContent = issueDate.value || '-';
                updateReviewTradingParties();
                const itemRows = document.querySelectorAll('.item-row');
                document.getElementById('summaryItemsCount').textContent = itemRows.length;

                const summaryPaymentTerms = document.getElementById('summaryPaymentTerms');
                if (summaryPaymentTerms) summaryPaymentTerms.textContent = (paymentTerms && paymentTerms.value) || '-';
                const summaryBillingFrequency = document.getElementById('summaryBillingFrequency');
                if (summaryBillingFrequency) {
                    summaryBillingFrequency.textContent = (billingFrequency && billingFrequency.options[billingFrequency.selectedIndex]?.text) || '-';
                }

                // Item Summary table — built directly from the actual item cards, no server round-trip
                const reviewBody = document.getElementById('reviewItemsBody');
                if (reviewBody) {
                    reviewBody.innerHTML = '';
                    itemRows.forEach(row => {
                        const desc = row.querySelector('.item-description')?.value || '(no description)';
                        const qty = row.querySelector('.quantity-input')?.value || '0';
                        const price = row.querySelector('.price-input')?.value || '0';
                        const subtotalEl = row.querySelector('[id^="subtotal-"]');
                        const rowTotalEl = row.querySelector('[id^="rowtotal-"]');
                        const subtotal = parseFloat(subtotalEl?.textContent) || 0;
                        const rowTotal = parseFloat(rowTotalEl?.textContent) || 0;
                        const tax = (rowTotal - subtotal).toFixed(2);
                        const tr = document.createElement('tr');
                        tr.innerHTML = `<td>${desc}</td><td class="text-center">${parseFloat(qty).toFixed(2)}</td><td class="text-end">${parseFloat(price).toFixed(2)}</td><td class="text-end">${tax}</td><td class="text-end fw-bold">${rowTotal.toFixed(2)}</td>`;
                        reviewBody.appendChild(tr);
                    });
                }
                const summaryItemBadge = document.getElementById('summaryItemBadge');
                if (summaryItemBadge) summaryItemBadge.textContent = `${itemRows.length} Item${itemRows.length === 1 ? '' : 's'}`;

                // Submission Readiness — only real, currently-true facts; no external verification implied.
                const hasItems = itemRows.length > 0;
                const chkItemsIcon = document.getElementById('chkItemsIcon');
                const chkItemsText = document.getElementById('chkItemsText');
                if (chkItemsText) {
                    chkItemsText.textContent = hasItems ? `Items Ready (${itemRows.length})` : 'No Items';
                    chkItemsIcon.className = hasItems ? 'ri-checkbox-circle-fill text-success' : 'ri-error-warning-fill text-danger';
                }

                const hasParties = !!(supplier.value && buyer.value);
                const chkPartiesIcon = document.getElementById('chkPartiesIcon');
                const chkPartiesText = document.getElementById('chkPartiesText');
                if (chkPartiesText) {
                    chkPartiesText.textContent = hasParties ? 'Supplier & Buyer Set' : 'Supplier/Buyer Not Set';
                    chkPartiesIcon.className = hasParties ? 'ri-checkbox-circle-fill text-success' : 'ri-error-warning-fill text-danger';
                }

                const totalAmountEl = document.getElementById('summaryTotalAmount');
                const hasTotals = totalAmountEl && parseFloat(totalAmountEl.textContent) > 0;
                const chkTotalsIcon = document.getElementById('chkTotalsIcon');
                const chkTotalsText = document.getElementById('chkTotalsText');
                if (chkTotalsText) {
                    chkTotalsText.textContent = hasTotals ? 'Totals Calculated' : 'Totals Not Calculated';
                    chkTotalsIcon.className = hasTotals ? 'ri-checkbox-circle-fill text-success' : 'ri-error-warning-fill text-danger';
                }

                // The Submit to LHDN button stays disabled until a draft has actually been
                // saved (see bindFormSubmission — submitBtn.disabled is gated on draftFilePath).
                // Surface that precondition here too, so this checklist never shows "all ready"
                // while the button is still disabled for a reason the user can't see.
                const hasDraft = !!document.getElementById('draftFilePath')?.value;
                const chkDraftIcon = document.getElementById('chkDraftIcon');
                const chkDraftText = document.getElementById('chkDraftText');
                if (chkDraftText) {
                    chkDraftText.textContent = hasDraft ? 'Draft Saved' : 'Draft Not Saved';
                    chkDraftIcon.className = hasDraft ? 'ri-checkbox-circle-fill text-success' : 'ri-error-warning-fill text-danger';
                }
            }
        };

        // Table scroll indicators
        function initTableScrollIndicators() {
            const tableResponsive = document.querySelector('.table-responsive');
            if (!tableResponsive) return;

            function updateScrollIndicators() {
                const scrollLeft = tableResponsive.scrollLeft;
                const scrollWidth = tableResponsive.scrollWidth;
                const clientWidth = tableResponsive.clientWidth;
                const maxScrollLeft = scrollWidth - clientWidth;

                // Update left indicator
                if (scrollLeft > 0) {
                    tableResponsive.classList.add('scrolled-left');
                } else {
                    tableResponsive.classList.remove('scrolled-left');
                }

                // Update right indicator
                if (scrollLeft >= maxScrollLeft - 1) {
                    tableResponsive.classList.add('scrolled-right');
                } else {
                    tableResponsive.classList.remove('scrolled-right');
                }
            }

            // Add scroll event listener
            tableResponsive.addEventListener('scroll', updateScrollIndicators);

            // Initial check
            updateScrollIndicators();

            // Update indicators when window resizes
            window.addEventListener('resize', updateScrollIndicators);
        }

        // Copy to Clipboard Function
        window.copyToClipboard = function(text, buttonElement) {
            navigator.clipboard.writeText(text).then(function() {
                // Show success feedback
                const originalIcon = buttonElement.querySelector('i').className;
                const originalTitle = buttonElement.title;

                // Change button appearance
                buttonElement.classList.add('copied');
                buttonElement.title = 'Copied!';

                // Show temporary success message
                const tempToast = document.createElement('div');
                tempToast.style.cssText = `
                    position: fixed;
                    top: 20px;
                    right: 20px;
                    background: #006948;  /* eInvWorld brand primary green */
                    color: white;
                    padding: 8px 12px;
                    border-radius: 4px;
                    font-size: 12px;
                    z-index: 99999;
                    animation: fadeInOut 2s ease;
                `;
                tempToast.textContent = 'Copied to clipboard!';
                document.body.appendChild(tempToast);

                // Reset button after delay
                setTimeout(() => {
                    buttonElement.classList.remove('copied');
                    buttonElement.title = originalTitle;
                    tempToast.remove();
                }, 2000);

            }).catch(function(err) {
                console.error('Failed to copy: ', err);
                // Fallback for older browsers
                const textArea = document.createElement('textarea');
                textArea.value = text;
                document.body.appendChild(textArea);
                textArea.select();
                try {
                    document.execCommand('copy');
                    buttonElement.classList.add('copied');
                    setTimeout(() => buttonElement.classList.remove('copied'), 2000);
                } catch (fallbackErr) {
                    console.error('Fallback copy failed: ', fallbackErr);
                }
                document.body.removeChild(textArea);
            });
        };

        // Error Handler - Centralized error management
        const ErrorHandler = {
            show(message, type = 'error') {
                const config = {
                    error: { icon: 'error', title: 'Error', color: '#d33' },
                    warning: { icon: 'warning', title: 'Warning', color: '#ffc107' },
                    success: { icon: 'success', title: 'Success', color: '#006948' },  // eInvWorld brand primary green
                    info: { icon: 'info', title: 'Information', color: '#0dcaf0' }
                };

                const settings = config[type] || config.error;

                Swal.fire({
                    icon: settings.icon,
                    title: settings.title,
                    text: message,
                    confirmButtonColor: settings.color,
                    timer: type === 'success' ? 2500 : undefined,
                    timerProgressBar: type === 'success'
                });
            },

            handleApiError(error, context = '') {
                console.error(`API Error${context ? ` in ${context}` : ''}:`, error);
                this.show('An unexpected error occurred. Please try again.');
            },

            handleValidationError(errors) {
                const errorList = Array.isArray(errors) ? errors.join('\n') : errors;
                this.show(errorList, 'warning');
            }
        };

        // Loading Manager - Centralized loading state management
        const LoadingManager = {
            show(element, loadingText = 'Loading...') {
                if (!element) return;

                element.disabled = true;
                element.dataset.originalText = element.innerHTML;
                element.innerHTML = `<i class="spinner-border spinner-border-sm me-2"></i>${loadingText}`;
            },

            hide(element) {
                if (!element) return;

                element.disabled = false;
                if (element.dataset.originalText) {
                    element.innerHTML = element.dataset.originalText;
                    delete element.dataset.originalText;
                }
            },

            showForSelector(selector, loadingText = 'Loading...') {
                const element = document.querySelector(selector);
                this.show(element, loadingText);
            },

            hideForSelector(selector) {
                const element = document.querySelector(selector);
                this.hide(element);
            }
        };

        // Error Handler (if not already defined) - MOVED BEFORE DOM READY
        if (typeof ErrorHandler === 'undefined') {
            window.ErrorHandler = {
                show: function(message, type = 'info') {
                    console.log(`${type.toUpperCase()}: ${message}`);
                    if (typeof Swal !== 'undefined') {
                        Swal.fire({
                            icon: type === 'error' ? 'error' : type === 'warning' ? 'warning' : 'info',
                            title: type.charAt(0).toUpperCase() + type.slice(1),
                            text: message,
                            confirmButtonColor: '#006948'  // eInvWorld brand primary green
                        });
                    } else {
                        alert(`${type.toUpperCase()}: ${message}`);
                    }
                }
            };
        }

        // Step navigation variables - MOVED BEFORE DOM READY
        let currentStep = 1;
        const totalSteps = 3;

        // True while the page's own initial setup (default classification/unit codes, etc.) is
        // still running. Several of those defaults are applied via `.trigger('change')` so Select2
        // refreshes its display — which, correctly, also reaches the real validation listeners now.
        // Without this guard, that purely cosmetic sync would mark a brand-new, untouched row
        // invalid the instant the page renders. Cleared shortly after DOMContentLoaded once initial
        // setup has had time to finish; genuine user interaction after that validates normally.
        let isInitializing = true;


        // Step navigation functions
        function nextStep() {
            try {
                console.log(`🔄 Next button clicked from step ${currentStep} - running validations...`);

                // Only validate items when moving FROM Step 2 TO Step 3 (from Invoice Items to Review)
                if (currentStep === 2) {
                    // Validation 1: Check if at least one item exists
                    const itemRows = document.querySelectorAll('.item-row');
                    if (itemRows.length === 0) {
                        ErrorHandler.show('You must add at least one item before proceeding.', 'warning');
                        return;
                    }

                    // Validation 2: Check if all items have valid data
                    if (!validateItemRequirements()) {
                        console.log('❌ Item requirements validation failed');
                        return;
                    }
                }

                // Validation 3: Check if current step is valid (for other step-specific validations)
                if (validateCurrentStep()) {
                    if (currentStep < totalSteps) {
                        document.getElementById(`step${currentStep}`).classList.add('d-none');
                        currentStep++;
                        document.getElementById(`step${currentStep}`).classList.remove('d-none');
                        if (InvoiceManager && InvoiceManager.updateProgress) {
                            InvoiceManager.updateProgress();
                        }
                        if (InvoiceManager && InvoiceManager.updateSummary) {
                            InvoiceManager.updateSummary();
                        }
                        console.log(`✅ Next step validation passed - proceeding to step ${currentStep}`);
                    }
                } else {
                    console.log('❌ Current step validation failed');
                }
            } catch (error) {
                console.error('❌ Error in nextStep function:', error);
                ErrorHandler.show('An error occurred while proceeding to the next step. Please try again.', 'error');
            }
        }

        function prevStep() {
            if (currentStep > 1) {
                document.getElementById(`step${currentStep}`).classList.add('d-none');
                currentStep--;
                document.getElementById(`step${currentStep}`).classList.remove('d-none');
                if (InvoiceManager && InvoiceManager.updateProgress) {
                    InvoiceManager.updateProgress();
                }
            }
        }

        // Make functions globally accessible
        window.nextStep = nextStep;
        window.prevStep = prevStep;
        window.validateItemRequirements = validateItemRequirements;
        window.validateCurrentStep = validateCurrentStep;

        // Immediate verification that functions are available
        console.log('🔧 Immediate function availability check:', {
            nextStep: typeof window.nextStep === 'function',
            prevStep: typeof window.prevStep === 'function'
        });

        // Helper function to filter supplier dropdown options based on document type
        async function filterSupplierOptions(docTypeCode) {
            console.log(`🔄 Filtering supplier options for document type: ${docTypeCode}`);
            
            try {
                const response = await fetch(`?handler=FilterSuppliers&docTypeCode=${encodeURIComponent(docTypeCode)}`, {
                    method: 'GET',
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                    }
                });
                
                if (response.ok) {
                    const allowedSuppliers = await response.json();
                    console.log(`✅ Received ${allowedSuppliers.length} allowed suppliers for document type ${docTypeCode}`);
                    
                    const supplierSelect = document.getElementById('supplierSelect');
                    if (supplierSelect) {
                        const currentValue = supplierSelect.value;
                        const allowedValues = allowedSuppliers.map(s => s.value);
                        
                        // Clear and repopulate dropdown with filtered options
                        supplierSelect.innerHTML = '<option value="">Select Supplier</option>';
                        
                        allowedSuppliers.forEach(supplier => {
                            const option = new Option(supplier.text, supplier.value);
                            supplierSelect.appendChild(option);
                        });
                        
                        // Check if current selection is still valid
                        if (currentValue && allowedValues.includes(currentValue)) {
                            supplierSelect.value = currentValue;
                            console.log(`✅ Kept current supplier selection: ${currentValue}`);
                        } else if (currentValue && !allowedValues.includes(currentValue)) {
                            supplierSelect.value = '';
                            console.log(`⚠️ Cleared invalid supplier selection for ${docTypeCode === '11' || docTypeCode === '12' || docTypeCode === '13' || docTypeCode === '14' ? 'self-billed' : 'regular'} document`);
                        }
                        
                        // Refresh Select2 if initialized
                        if (typeof $ !== 'undefined' && $.fn.select2) {
                            $(supplierSelect).trigger('change');
                            console.log('✅ Refreshed Select2 supplier dropdown');
                        }
                    }
                } else {
                    console.error('❌ Failed to filter supplier options:', response.status);
                }
            } catch (error) {
                console.error('❌ Error filtering supplier options:', error);
            }
        }

        // Helper function to update classification codes for all items
        function updateAllClassificationCodes(forceDebug = false) {
            const docTypeSelect = document.getElementById('docTypeCode');
            const docTypeCode = docTypeSelect?.value;
            
            if (forceDebug || docTypeCode) {
                console.log(`🔍 [updateAllClassificationCodes] Document Type Code: "${docTypeCode}"`);
                console.log(`🔍 [updateAllClassificationCodes] Document Type Element:`, docTypeSelect);
                
                if (docTypeSelect && docTypeSelect.selectedIndex >= 0) {
                    const selectedOption = docTypeSelect.options[docTypeSelect.selectedIndex];
                    console.log(`🔍 [updateAllClassificationCodes] Selected Option Text: "${selectedOption.text}"`);
                }
            }
            
            const isSelfBilled = docTypeCode && ['11', '12', '13', '14'].includes(docTypeCode);
            console.log(`🔍 [updateAllClassificationCodes] Is Self-Billed: ${isSelfBilled}`);
            
            const classificationSelects = document.querySelectorAll('select[name*=".ClassificationCode"]');
            console.log(`🔍 Found ${classificationSelects.length} classification dropdowns`);
            
            if (isSelfBilled) {
                const supplierTIN = window.currentSupplierTIN;

                // Default behaviour for self-billed: lock to 004
                let allowedCodes = [ "004", "010", "011", "033", "034", "035", "036", "037", "038", "039", "040", "041", "045" ];
                let defaultCode = '004';
                let lockDropdown = false;
                let titleText = 'Classification code is locked for self-billed documents';

                console.log(`🔄 Self-billed document: supplierTIN=${supplierTIN || 'null'}, allowed=${allowedCodes.join(',')}, default=${defaultCode}, lock=${lockDropdown}`);

                classificationSelects.forEach((select, index) => {
                    const oldValue = select.value;

                    // Enable/disable options based on allowed codes
                    Array.from(select.options).forEach(opt => {
                        if (!opt.value) return; // skip placeholder
                        opt.disabled = !allowedCodes.includes(opt.value);
                    });

                    // If current value is not allowed, apply default
                    if (!allowedCodes.includes(select.value)) {
                        select.value = defaultCode;
                    }

                    // Visual + interaction handling
                    if (lockDropdown) {
                        select.style.backgroundColor = '#f8f9fa';
                        select.style.cursor = 'not-allowed';
                        select.style.pointerEvents = 'none';
                        select.setAttribute('data-locked', 'true');
                    } else {
                        select.style.backgroundColor = '';
                        select.style.cursor = '';
                        select.style.pointerEvents = '';
                        select.removeAttribute('data-locked');
                    }
                    select.title = titleText;

                    // Update Select2 if initialized
                    if (typeof $ !== 'undefined' && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
                        $(select).val(select.value).trigger('change');
                        $(select).next('.select2-container').css({
                            'pointer-events': lockDropdown ? 'none' : '',
                            'opacity': lockDropdown ? '0.6' : ''
                        });
                    }

                    console.log(`✅ Self-billed classification updated for dropdown ${index + 1}: "${oldValue}" → "${select.value}" (allowed: ${allowedCodes.join(',')})`);
                });
            } else {
                console.log(`📋 Regular document: Resetting classification codes to "022" and ENABLING dropdowns`);
                
                classificationSelects.forEach((select, index) => {
                    console.log(`🔍 Processing dropdown ${index + 1}: Current value = "${select.value}"`);
                    
                        // Reset to default "022" for regular documents
                    const option022 = select.querySelector('option[value="022"]');
                    if (option022) {
                        console.log(`✅ Option "022" found: "${option022.text}"`);
                        
                        const oldValue = select.value;
                        select.value = '022';
                        
                        // Re-enable all classification options for regular documents
                        Array.from(select.options).forEach(opt => { opt.disabled = false; });

                        // Enable the dropdown for regular documents
                        select.style.backgroundColor = '';
                        select.style.cursor = '';
                        select.style.pointerEvents = '';
                        select.title = 'Select appropriate classification code for your business';
                        select.removeAttribute('data-locked');
                        
                        // Update Select2 if initialized
                        if (typeof $ !== 'undefined' && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
                            $(select).val('022').trigger('change');
                            // Re-enable Select2 interaction
                            $(select).next('.select2-container').css({
                                'pointer-events': '',
                                'opacity': ''
                            });
                            console.log(`🔓 Reset and enabled Select2 dropdown ${index + 1}: "${oldValue}" → "022" (USER-SELECTABLE)`);
                        } else {
                            console.log(`🔓 Reset and enabled regular dropdown ${index + 1}: "${oldValue}" → "022" (USER-SELECTABLE)`);
                        }
                    } else {
                        console.warn(`❌ Option "022" NOT found in dropdown ${index + 1}`);
                        // Just enable the dropdown even if we can't find 022
                        Array.from(select.options).forEach(opt => { opt.disabled = false; });
                        select.style.backgroundColor = '';
                        select.style.cursor = '';
                        select.style.pointerEvents = '';
                        select.title = 'Select appropriate classification code for your business';
                        select.removeAttribute('data-locked');
                        
                        if (typeof $ !== 'undefined' && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
                            $(select).next('.select2-container').css({
                                'pointer-events': '',
                                'opacity': ''
                            });
                        }
                        console.log(`🔓 Enabled dropdown ${index + 1} (could not reset to 022)`);
                    }
                });
            }
        }

        // Function to ensure all price inputs use positive validation (negative symbol removed from UI)
        function updatePriceInputValidation(docTypeCode) {
            console.log(`🔍 [updatePriceInputValidation] Ensuring positive price validation for all document types`);
            
            // Find all unit price input fields and ensure they require positive values
            const priceInputs = document.querySelectorAll('input[name*=".UnitPrice"], .price-input');
            console.log(`🔍 [updatePriceInputValidation] Found ${priceInputs.length} price input fields`);
            
            priceInputs.forEach((input, index) => {
                // All document types now require positive values (min="0")
                input.setAttribute('min', '0');
                input.setAttribute('placeholder', '0.00');
                console.log(`✅ [updatePriceInputValidation] Set positive validation for price input ${index + 1}`);
            });
        }

        // Function to update draft button text based on invoice state
        function updateDraftButtonText() {
            const invoiceNoInput = document.querySelector('input[name="Invoice.InvoiceNo"]');
            const saveBtn = document.querySelector('button[name="action"][value="saveDraft"]');
            
            if (saveBtn && invoiceNoInput) {
                const hasInvoiceNo = invoiceNoInput.value && invoiceNoInput.value.trim() !== '';
                const buttonText = hasInvoiceNo 
                    ? '<i class="ri-save-line me-1"></i>Update Draft'
                    : '<i class="ri-save-line me-1"></i>Save as Draft';
                
                saveBtn.innerHTML = buttonText;
                console.log(`🔄 Draft button updated: ${hasInvoiceNo ? 'Update Draft' : 'Save as Draft'} (Invoice: ${invoiceNoInput.value})`);
            }
        }

        // Initialize everything when DOM is ready
        document.addEventListener('DOMContentLoaded', function() {
            console.log('🚀 DOM Content Loaded - Initializing Invoice Manager');
            
            // Check if this is a Credit Note creation and show RefUUID field
            const docTypeSelect = document.getElementById('docTypeCode');
            const refUUIDSection = document.getElementById('refUUIDSection');
            
            // Multiple ways to detect RefUUID required document types:
            // Adjustment document types that need RefUUID: 02=CN, 03=DN, 04=RN, 12=Self-CN, 13=Self-DN, 14=Self-RN
            // Only document type 11 (Self-billed Invoice) is original and does NOT need RefUUID
            const refUUIDRequiredTypes = ['02', '03', '04', '12', '13', '14'];
            const urlRefUUIDTypes = ['CN', 'DN', 'RN', 'SELF-CN', 'SELF-DN', 'SELF-RN'];
            
            const isDropdownRefUUIDType = docTypeSelect && refUUIDRequiredTypes.includes(docTypeSelect.value);
            const isModelRefUUIDType = refUUIDRequiredTypes.includes(window.invoiceCreateServerData.modelDocTypeCode);
            const isUrlRefUUIDType = urlRefUUIDTypes.includes(window.invoiceCreateServerData.urlTypeParam);

            console.log(`🔍 RefUUID Required Document Detection:
                - Dropdown value: ${docTypeSelect?.value || 'null'}
                - Model DocTypeCode: ${window.invoiceCreateServerData.modelDocTypeCode}
                - URL type parameter: ${window.invoiceCreateServerData.urlTypeParam}
                - Is RefUUID Required: ${isDropdownRefUUIDType || isModelRefUUIDType || isUrlRefUUIDType}`);
            
            if (isDropdownRefUUIDType || isModelRefUUIDType || isUrlRefUUIDType) {
                console.log('📋 RefUUID required document detected on page load - RefUUID field should be visible');
                
                // Check current server-side rendered state
                const refUUIDDisplayMode = document.getElementById('refUUIDDisplayMode');
                const refUUIDSelectMode = document.getElementById('refUUIDSelectMode');
                const isDisplayModeVisible = refUUIDDisplayMode && refUUIDDisplayMode.style.display !== 'none';
                const isSelectModeVisible = refUUIDSelectMode && refUUIDSelectMode.style.display !== 'none';
                
                console.log(`🔍 Server-side rendered state:
                    - Display mode visible: ${isDisplayModeVisible}
                    - Select mode visible: ${isSelectModeVisible}
                    - RefUUID value: '${window.invoiceCreateServerData.modelRefUUID}'`);
                
                // Only load dropdown options if select mode is visible
                if (isSelectModeVisible) {
                    console.log('🔍 Select mode active - will load dropdown options if supplier selected');
                    const supplierSelect = document.getElementById('supplierSelect');
                    if (supplierSelect && supplierSelect.value) {
                        setTimeout(() => loadAvailableInvoicesForReference(), 1000);
                    }
                } else if (isDisplayModeVisible) {
                    console.log('🎯 Display mode active - RefUUID auto-selected from original invoice');
                }
            }
            
            // Update draft button text on page load
            setTimeout(updateDraftButtonText, 100);
            
            // Debug: Log available classification codes
            const firstClassificationSelect = document.querySelector('select[name*=".ClassificationCode"]');
            if (firstClassificationSelect) {
                console.log('🔍 Available Classification Codes:');
                const options = Array.from(firstClassificationSelect.options);
                options.forEach((opt, index) => {
                    if (opt.value) { // Skip empty option
                        console.log(`  ${index}: Value="${opt.value}", Text="${opt.text}"`);
                    }
                });
                
                const code004 = options.find(opt => opt.value === '004');
                if (code004) {
                    console.log('✅ Classification code "004" is available:', code004.text);
                } else {
                    console.warn('❌ Classification code "004" NOT found in dropdown options');
                }
            }
            
            window.InvoiceManager = InvoiceManager; // expose for debugging/tests; nextStep() already calls this in-scope
            InvoiceManager.init();

            // Update classification codes if document is already self-billed on page load
            console.log('🔄 Scheduling classification code updates...');
            
            // Update price input validation on page load
            const initialDocTypeCode = docTypeSelect?.value;
            if (initialDocTypeCode) {
                updatePriceInputValidation(initialDocTypeCode);
            }
            
            // Try multiple times with different delays to catch all scenarios
            setTimeout(() => {
                console.log('🔄 [100ms] First classification code update attempt...');
                updateAllClassificationCodes(true);
            }, 100);
            
            setTimeout(() => {
                console.log('🔄 [500ms] Second classification code update attempt...');
                updateAllClassificationCodes(true);
            }, 500);
            
            setTimeout(() => {
                console.log('🔄 [1000ms] Final classification code update attempt...');
                updateAllClassificationCodes(true);
            }, 1000);

            // Initial setup (including the classification-code retries above) is done applying
            // its own defaults by now — real user interaction validates normally from this point.
            setTimeout(() => { isInitializing = false; }, 1200);

            // Initialize table scroll indicators
            initTableScrollIndicators();

            // EXPLICITLY bind nextStep to the button to avoid onclick issues
            const nextStepButton = document.querySelector('button[onclick*="nextStep"]');
            if (nextStepButton) {
                console.log('🔧 Found nextStep button, binding directly');
                nextStepButton.onclick = function(e) {
                    e.preventDefault();
                    console.log('🖱️ NextStep button clicked via direct binding');
                    if (typeof window.nextStep === 'function') {
                        window.nextStep();
                    } else {
                        console.error('❌ nextStep function still not available');
                    }
                };
            }

            // Debug: Check Add Item button status
            const addItemBtn = document.getElementById('addItemBtn');
            if (addItemBtn) {
                console.log('✅ Add Item button found:', {
                    id: addItemBtn.id,
                    className: addItemBtn.className,
                    disabled: addItemBtn.disabled,
                    type: addItemBtn.type,
                    textContent: addItemBtn.textContent.trim()
                });

                console.log('✅ Add Item button ready - using InvoiceManager handler');
            } else {
                console.error('❌ Add Item button not found in DOM');
            }

            // Debug: Check if all required functions exist
            console.log('🔍 Function availability check:', {
                addItemRow: typeof addItemRow === 'function',
                calculateTotals: typeof calculateTotals === 'function',
                nextStep: typeof nextStep === 'function',
                'window.nextStep': typeof window.nextStep === 'function',
                ErrorHandler: typeof ErrorHandler === 'object',
                classificationOptionsHtml: typeof classificationOptionsHtml !== 'undefined',
                unitOptionsHtml: typeof unitOptionsHtml !== 'undefined',
                taxCategoryOptionsHtml: typeof taxCategoryOptionsHtml !== 'undefined'
            });
        });



        // updateProgress function removed - using InvoiceManager.updateProgress instead

        function validateCurrentStep() {
            const currentStepElement = document.getElementById(`step${currentStep}`);
            const requiredFields = currentStepElement.querySelectorAll('[required]');
            let isValid = true;
            let firstInvalidField = null;

            requiredFields.forEach(field => {
                // Skip validation for tax percentage fields that should be exempt
                if (field.name && field.name.includes('.TaxPercentage')) {
                    const taxRow = field.closest('.tax-row');
                    if (taxRow) {
                        const taxCategorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
                        if (taxCategorySelect) {
                            const categoryText = taxCategorySelect.options[taxCategorySelect.selectedIndex]?.text || '';
                            const isNotApplicable = categoryText.toLowerCase().includes('not applicable');
                            const isExemption = categoryText.toLowerCase().includes('exemption');
                            
                            // Skip validation for exempt categories
                            if (isNotApplicable || isExemption) {
                                console.log('⏭️ Skipping step validation for exempt tax percentage field:', categoryText);
                                return; // Skip this field
                            }
                        }
                    }
                }
                
                const fieldValid = !!field.value.trim();
                if (!fieldValid) {
                    field.classList.add('is-invalid');
                    isValid = false;
                    if (!firstInvalidField) firstInvalidField = field;
                } else {
                    field.classList.remove('is-invalid');
                }
                setFieldAriaValidation(field, fieldValid);
            });

            if (!isValid) {
                ErrorHandler.show('Please fill in all required fields before proceeding.', 'warning');
                focusFirstInvalidField(firstInvalidField);
            }

            return isValid;
        }





        function calculateSubtotal(input) {
            const row = input.closest('.item-row');
            if (!row) return;

            const quantityInput = row.querySelector('.quantity-input');
            const priceInput = row.querySelector('.price-input');

            if (!quantityInput || !priceInput) return;

            const quantity = parseFloat(quantityInput.value) || 0;
            const price = parseFloat(priceInput.value) || 0;
            const subtotal = quantity * price;

            const subtotalElement = row.querySelector('[id^="subtotal-"]');
            if (subtotalElement) {
                subtotalElement.textContent = subtotal.toFixed(2);
            }

            calculateTotals();
        }

        // Total Excl Tax = Subtotal - Discount + Fee/Charge (matches InvoiceLine.CalculateAmounts()
        // and InvoiceMapper's LHDN submission math), then tax is computed on that net base.
        function getLineExclTaxAmount(row, itemSubtotal) {
            const discountInput = row.querySelector('.discount-amount-input');
            const feeInput = row.querySelector('.fee-amount-input');
            const discount = parseFloat(discountInput?.value) || 0;
            const fee = parseFloat(feeInput?.value) || 0;
            return itemSubtotal - discount + fee;
        }

        // Wires the optional "Rate %" convenience inputs next to Discount/Fee Amount: typing a rate
        // computes Amount = Subtotal * Rate / 100 and fills the real (bound, persisted) Amount field.
        // Rate itself is never submitted - no DiscountRate/FeeChargeRate column exists, matching the
        // LHDN payload's line-level AllowanceCharge, which carries an amount, not a rate. Also wires
        // the Amount inputs themselves so typing directly into them recalculates totals immediately.
        function bindDiscountFeeRateInputs(scope) {
            const wireRateInput = (rateInput, targetSelectorAttr) => {
                rateInput.addEventListener('input', () => {
                    const row = rateInput.closest('.item-row');
                    const amountInput = row?.querySelector(rateInput.getAttribute(targetSelectorAttr));
                    const quantityInput = row?.querySelector('.quantity-input');
                    const priceInput = row?.querySelector('.price-input');
                    if (!amountInput || !quantityInput || !priceInput) return;

                    const subtotal = (parseFloat(quantityInput.value) || 0) * (parseFloat(priceInput.value) || 0);
                    const rate = parseFloat(rateInput.value) || 0;
                    amountInput.value = (subtotal * rate / 100).toFixed(2);
                    amountInput.dispatchEvent(new Event('input', { bubbles: true }));
                });
            };

            scope.querySelectorAll('.discount-rate').forEach(input => wireRateInput(input, 'data-discount-target'));
            scope.querySelectorAll('.fee-rate').forEach(input => wireRateInput(input, 'data-fee-target'));
            scope.querySelectorAll('.discount-amount-input, .fee-amount-input').forEach(amountInput => {
                amountInput.addEventListener('input', () => calculateTotals());
            });
        }

        function calculateTotals() {
            let subtotal = 0;      // gross Sum(Qty*UnitPrice) - shown as "Subtotal" (pre-discount/fee)
            let exclTaxTotal = 0;  // Sum(Subtotal - Discount + Fee/Charge) - the true taxable-base total
            let taxAmount = 0;

            // Debug: Log the number of item rows found
            const itemRows = document.querySelectorAll('.item-row');
            console.log(`🔢 Found ${itemRows.length} item rows for calculation`);

            // Calculate subtotal and tax from all item rows
            itemRows.forEach((row, index) => {
                const quantityInput = row.querySelector('.quantity-input');
                const priceInput = row.querySelector('.price-input');
                let itemSubtotal = 0;

                if (quantityInput && priceInput) {
                    const quantity = parseFloat(quantityInput.value) || 0;
                    const price = parseFloat(priceInput.value) || 0;
                    itemSubtotal = quantity * price;
                    subtotal += itemSubtotal;

                    const itemExclTax = getLineExclTaxAmount(row, itemSubtotal);
                    exclTaxTotal += itemExclTax;

                    console.log(`📊 Item ${index + 1}: Qty=${quantity}, Price=${price}, Subtotal=${itemSubtotal.toFixed(2)}, ExclTax=${itemExclTax.toFixed(2)}`);

                    // Calculate tax for this item (on the discount/fee-netted base, not raw subtotal)
                    const taxSection = row.querySelector('.tax-section');
                    if (taxSection) {
                        let itemTaxAmount = 0;
                        taxSection.querySelectorAll('.tax-row').forEach(taxRow => {
                            const taxPercentInput = taxRow.querySelector('input[name*="TaxPercentage"]');
                            const taxAmountInput = taxRow.querySelector('input[name*="TaxAmount"]');
                            if (taxPercentInput) {
                                const percent = parseFloat(taxPercentInput.value) || 0;
                                const taxValue = Math.round((itemExclTax * percent / 100) * 100) / 100;
                                itemTaxAmount += taxValue;
                                if (taxAmountInput) {
                                    taxAmountInput.value = taxValue.toFixed(2);
                                }
                            }
                        });
                        taxAmount += itemTaxAmount;

                        const rowTotalElement = row.querySelector('[id^="rowtotal-"]');
                        if (rowTotalElement) {
                            rowTotalElement.textContent = (itemExclTax + itemTaxAmount).toFixed(2);
                        }
                    }
                }
            });

            const totalAmount = Math.round((exclTaxTotal + taxAmount) * 100) / 100;
            console.log(`💰 Totals: Subtotal=${subtotal.toFixed(2)}, ExclTax=${exclTaxTotal.toFixed(2)}, Tax=${taxAmount.toFixed(2)}, Total=${totalAmount.toFixed(2)}`);

            // Update summary if elements exist
            const summarySubtotal = document.getElementById('summarySubtotal');
            const summaryTaxAmount = document.getElementById('summaryTaxAmount');
            const summaryTotalAmount = document.getElementById('summaryTotalAmount');

            if (summarySubtotal) {
                summarySubtotal.textContent = subtotal.toFixed(2);
                console.log(`✅ Updated summarySubtotal to ${subtotal.toFixed(2)}`);
            } else {
                console.warn(`⚠️ summarySubtotal element not found`);
            }

            if (summaryTaxAmount) {
                summaryTaxAmount.textContent = taxAmount.toFixed(2);
                console.log(`✅ Updated summaryTaxAmount to ${taxAmount.toFixed(2)}`);
            } else {
                console.warn(`⚠️ summaryTaxAmount element not found`);
            }

            if (summaryTotalAmount) {
                summaryTotalAmount.textContent = totalAmount.toFixed(2);
                console.log(`✅ Updated summaryTotalAmount to ${totalAmount.toFixed(2)}`);
            } else {
                console.warn(`⚠️ summaryTotalAmount element not found`);
            }

            // Step 2 "Invoice Summary" card (separate ids from the Step 3 review card above)
            const step2Subtotal = document.getElementById('step2SummarySubtotal');
            const step2TaxAmount = document.getElementById('step2SummaryTaxAmount');
            const step2TotalAmount = document.getElementById('step2SummaryTotalAmount');
            const step2AmountPayable = document.getElementById('step2AmountPayable');
            if (step2Subtotal) step2Subtotal.textContent = subtotal.toFixed(2);
            if (step2TaxAmount) step2TaxAmount.textContent = taxAmount.toFixed(2);
            if (step2TotalAmount) step2TotalAmount.textContent = totalAmount.toFixed(2);
            if (step2AmountPayable) step2AmountPayable.textContent = totalAmount.toFixed(2);

            // Step 1 "Running Total" KPI tile — the only summary display calculateTotals() didn't
            // already update, so it stayed frozen at its initial "RM 0.00" no matter how many items
            // were added on Step 2.
            const kpiRunningTotal = document.getElementById('kpiRunningTotal');
            if (kpiRunningTotal) kpiRunningTotal.textContent = 'RM ' + totalAmount.toFixed(2);

            // Step 1 sticky "Invoice Summary" sidebar card — was never wired up, so it stayed frozen at
            // its initial "RM 0.00" no matter how many items/totals were entered on Step 2 (visible as
            // soon as the user went back to Step 1, e.g. after loading a template/clone with items).
            const step1SummarySubtotal = document.getElementById('step1SummarySubtotal');
            const step1SummaryTax = document.getElementById('step1SummaryTax');
            const step1SummaryTotal = document.getElementById('step1SummaryTotal');
            if (step1SummarySubtotal) step1SummarySubtotal.textContent = 'RM ' + subtotal.toFixed(2);
            if (step1SummaryTax) step1SummaryTax.textContent = 'RM ' + taxAmount.toFixed(2);
            if (step1SummaryTotal) step1SummaryTotal.textContent = totalAmount.toFixed(2);

            // Update hidden fields
            const totalAmountExclTax = document.getElementById('totalAmountExclTax');
            const totalTaxAmount = document.getElementById('totalTaxAmount');
            const totalAmountIncTax = document.getElementById('totalAmountIncTax');
            if (totalAmountExclTax) totalAmountExclTax.value = exclTaxTotal.toFixed(2);
            if (totalTaxAmount) totalTaxAmount.value = taxAmount.toFixed(2);
            if (totalAmountIncTax) totalAmountIncTax.value = totalAmount.toFixed(2);
        }



        // Update summary when form changes
        function updateSummary() {
            const docType = document.getElementById('docTypeCode');
            const supplier = document.getElementById('supplierSelect');
            const buyer = document.getElementById('buyerSelect');
            const issueDate = document.getElementById('issueDate');

            if (docType) {
            document.getElementById('summaryDocType').textContent = docType.options[docType.selectedIndex]?.text || '-';
            }
            if (supplier) {
            document.getElementById('summarySupplier').textContent = supplier.options[supplier.selectedIndex]?.text || '-';
            }
            if (buyer) {
            document.getElementById('summaryBuyer').textContent = buyer.options[buyer.selectedIndex]?.text || '-';
            }
            if (issueDate) {
            document.getElementById('summaryIssueDate').textContent = issueDate.value || '-';
            }
            updateReviewTradingParties();

            const itemsCountElement = document.getElementById('summaryItemsCount');
            if (itemsCountElement) {
                itemsCountElement.textContent = document.querySelectorAll('.item-row').length;
            }

            // Update financial summary by recalculating totals
            calculateTotals();
        }

        // Populates the Step 3 review's TIN/BRN/SST (supplier) and TIN/BRN/Address (buyer) rows
        // from the party data already fetched by loadPartyDetails() — no extra round-trip.
        function updateReviewTradingParties() {
            const setText = (id, value) => {
                const el = document.getElementById(id);
                if (el) el.textContent = value || '-';
            };
            setText('summarySupplierTIN', window.currentSupplierTIN);
            setText('summarySupplierRegNo', window.currentSupplierRegNo);
            setText('summarySupplierSST', window.currentSupplierSST);
            setText('summaryBuyerTIN', window.currentBuyerTIN);
            setText('summaryBuyerRegNo', window.currentBuyerRegNo);
        }

        // Add event listeners for summary updates
        document.addEventListener('change', function(e) {
            if (e.target.id === 'docTypeCode' || e.target.id === 'supplierSelect' ||
                e.target.id === 'buyerSelect' || e.target.id === 'issueDate') {
                updateSummary();
            }
        });

        // Tax category handling
        function toggleExemptionReason(input) {
            console.log('🔧 toggleExemptionReason called with category:', input.value);
            const taxRow = input.closest('.tax-row');
            const category = input.value;
            const categoryText = input.options[input.selectedIndex]?.text || '';
            const exemptionInput = taxRow.querySelector('.tax-exemption-reason');
            const taxPercentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');
            
            console.log('📊 Tax category details:', {
                value: category,
                text: categoryText,
                taxRow: !!taxRow,
                exemptionInput: !!exemptionInput,
                taxPercentageInput: !!taxPercentageInput
            });
            
            // Check for categories that should have 0% tax and be read-only
            const isNotApplicable = category === "NA" || categoryText.toLowerCase().includes('not applicable');
            const isExemption = category === "E" || categoryText.toLowerCase().includes('exemption') || categoryText.toLowerCase().includes('exempt');
            const shouldBeZeroPercent = isNotApplicable || isExemption;
            
            console.log('🔍 Category analysis:', {
                isNotApplicable,
                isExemption,
                shouldBeZeroPercent
            });
            
            // Handle tax percentage
            if (taxPercentageInput && shouldBeZeroPercent) {
                console.log('✅ Setting tax percentage to 0 and making read-only');
                taxPercentageInput.value = '0';
                taxPercentageInput.readOnly = true;
                taxPercentageInput.style.backgroundColor = '#f8f9fa';
                // Remove required attribute and validation styling
                taxPercentageInput.removeAttribute('required');
                taxPercentageInput.classList.remove('is-invalid');
                taxPercentageInput.classList.add('is-valid');
                // Trigger change event to recalculate totals
                taxPercentageInput.dispatchEvent(new Event('input', { bubbles: true }));
            } else if (taxPercentageInput) {
                console.log('🔓 Making tax percentage editable');
                taxPercentageInput.readOnly = false;
                taxPercentageInput.style.backgroundColor = '';
                // Add back required attribute
                taxPercentageInput.setAttribute('required', 'required');
                taxPercentageInput.classList.remove('is-valid');
            }
            
            // Show/hide exemption reason field for Tax Exemption category only
            if (exemptionInput) {
                exemptionInput.style.display = isExemption ? "block" : "none";
                const exemptionReasonInput = exemptionInput.querySelector('input');
                if (!isExemption) {
                    exemptionReasonInput.value = "";
                    // Remove validation styling when hidden
                    exemptionReasonInput.classList.remove('is-invalid', 'is-valid');
                    exemptionReasonInput.removeAttribute('required');
                } else {
                    // Add required attribute when shown
                    exemptionReasonInput.setAttribute('required', 'required');
                }
                console.log('🎯 Exemption field display set to:', exemptionInput.style.display);
            }
        }

        // Add tax row (renamed to avoid conflict with external JS)
        function addTaxRow(itemIndex) {
            const taxSection = document.getElementById(`taxes-${itemIndex}`);
            const taxCount = taxSection.children.length;
            const taxRow = document.createElement('div');
            taxRow.className = 'tax-row mb-2 p-2 border rounded';
            taxRow.innerHTML = `
                <div class="d-flex flex-wrap gap-2 align-items-center">
                    <div class="flex-fill tax-category-wrapper" style="min-width: 100px;">
                        <select name="Invoice.InvoiceLines[${itemIndex}].Taxes[${taxCount}].TaxCategory" class="form-select form-select-sm tax-category">
                            <option value="">Tax Category</option>
                            ${taxCategoryOptionsHtml}
                        </select>
                    </div>
                    <div class="tax-percentage-wrapper" style="flex: 0 0 90px;">
                        <input name="Invoice.InvoiceLines[${itemIndex}].Taxes[${taxCount}].TaxPercentage" type="number" class="form-control form-control-sm" placeholder="%" step="0.01" required />
                    </div>
                    <div class="tax-action-wrapper" style="flex: 0 0 auto;">
                        <i class="ri-close-circle-line text-danger" onclick="removeTaxRow(this)" title="Remove Tax" style="cursor: pointer; font-size: 16px; padding: 2px;"></i>
                    </div>
                </div>
                <div class="mt-2 tax-exemption-reason" style="display: none;">
                    <input name="Invoice.InvoiceLines[${itemIndex}].Taxes[${taxCount}].TaxExemptionReason" class="form-control form-control-sm" placeholder="Exemption Reason (Required)" />
                </div>
            `;
            taxSection.appendChild(taxRow);

            // Add validation for new tax fields
            const taxCategorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
            const taxPercentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');

            // Initialize Select2 for the new tax category dropdown
            if (taxCategorySelect && typeof $ !== 'undefined' && $.fn.select2) {
                $(taxCategorySelect).select2({
                    theme: 'bootstrap-5',
                    width: '100%',
                    placeholder: 'Select Tax Category',
                    allowClear: true,
                    minimumResultsForSearch: 0 // Always show search box
                });

                // Bind Select2 change event
                $(taxCategorySelect).on('select2:select', function() {
                    console.log('🎯 Select2 change event triggered');
                    toggleExemptionReason(this);
                    validateTaxField(this);
                });
            } else {
                // Fallback for regular dropdowns without Select2
                taxCategorySelect?.addEventListener('change', function() {
                    console.log('🎯 Regular change event triggered');
                    toggleExemptionReason(this);
                    validateTaxField(this);
                });
            }

            taxPercentageInput?.addEventListener('input', function() {
                validateTaxField(this);
            });

            // Apply validation styling (only affects fields already touched — see forceValidationStyling)
            setTimeout(() => {
                forceValidationStyling();
            }, 50);

            console.log(`✅ Tax row added for item ${itemIndex + 1} - Please fill in tax category and percentage`);
        }

        // Remove tax row (renamed to avoid conflict with external JS)
        function removeTaxRow(button) {
            const taxRow = button.closest('.tax-row');
            const taxSection = taxRow.parentElement;
            const taxRows = taxSection.querySelectorAll('.tax-row');

            // Prevent removing the last tax from an item
            if (taxRows.length <= 1) {
                ErrorHandler.show('Each item must have at least one tax entry. Cannot remove the last tax.', 'warning');
                return;
            }

            taxRow.remove();
            console.log('✅ Tax row removed');
        }


        // Built server-side in CreateInvoice.cshtml; see window.classificationOptionsHtml comment above.
        const savedItemsOptionsHtml = window.savedItemsOptionsHtml;

        function addItemRow() {
            console.log('🚀 addItemRow function called');

            try {
                const tbody = document.getElementById('lineItems');
                if (!tbody) {
                    console.error('❌ lineItems tbody not found');
                    ErrorHandler.show('Could not find the items table. Please refresh the page.', 'error');
                    return;
                }

                const itemCountInput = document.getElementById('itemCount');
                const itemCount = parseInt(itemCountInput?.value) || tbody.children.length;
                console.log(`📝 Adding item #${itemCount + 1}, current count: ${itemCount}`);

                const newRow = document.createElement('div');
                newRow.className = 'item-row';
                newRow.style.borderLeft = '4px solid var(--einv-primary, #006948)';
                newRow.style.borderBottom = '1px solid var(--einv-border, #e9ecef)';
                newRow.setAttribute('data-item-index', itemCount);

                // Mirrors _CreateInvoice_Step2Items.cshtml's server-rendered markup: Item/Service
                // (Select Saved Item -> Item Code -> Item Description -> Classification -> Unit),
                // Quantity & Pricing, then collapsed-by-default Discount/Fee-Charge/Taxes/Additional-Info.
                newRow.innerHTML = `
                    <div class="irow">
                        <div class="irow-num">${itemCount + 1}</div>
                        <div class="irow-desc">
                            <select class="form-select form-select-sm saved-item-select bg-light border-primary mb-2" onchange="autoFillItem(this, ${itemCount})">
                                <option value="">-- Select Saved Item --</option>
                                ${savedItemsOptionsHtml}
                            </select>
                            <input name="Invoice.InvoiceLines[${itemCount}].ItemCode" class="form-control form-control-sm item-code mb-2" placeholder="Item Code" />
                            <textarea name="Invoice.InvoiceLines[${itemCount}].ItemDescription" class="form-control form-control-sm item-description" rows="1" placeholder="Enter comprehensive item description..." required></textarea>
                        </div>
                        <div class="irow-classification">
                            <div class="irow-mlabel">Classification <span class="text-danger">*</span></div>
                            <select name="Invoice.InvoiceLines[${itemCount}].ClassificationCode" class="form-select form-select-sm item-classification" required>
                                <option value="">Select Classification</option>
                                ${classificationOptionsHtml}
                            </select>
                        </div>
                        <div class="irow-unit">
                            <div class="irow-mlabel">Unit <span class="text-danger">*</span></div>
                            <select name="Invoice.InvoiceLines[${itemCount}].UnitOfMeasure" class="form-control form-control-sm" required>
                                <option value="">Unit</option>
                                ${unitOptionsHtml}
                            </select>
                        </div>
                        <div class="irow-qty">
                            <div class="irow-mlabel">Qty <span class="text-danger">*</span></div>
                            <input name="Invoice.InvoiceLines[${itemCount}].Quantity" type="number" class="form-control form-control-sm quantity-input text-center" step="0.01" min="0" required placeholder="0" />
                        </div>
                        <div class="irow-price">
                            <div class="irow-mlabel">Unit Price <span class="text-danger">*</span></div>
                            <input name="Invoice.InvoiceLines[${itemCount}].UnitPrice" type="number" class="form-control form-control-sm price-input text-end" step="0.01" min="0" required placeholder="0.00" />
                        </div>
                        <div class="irow-total">
                            <div class="irow-mlabel">Subtotal</div>
                            <div class="irow-num-val" id="subtotal-${itemCount}">0.00</div>
                            <div class="irow-mlabel">Row Total</div>
                            <div class="irow-num-val irow-total-val" id="rowtotal-${itemCount}">0.00</div>
                        </div>
                        <div class="irow-actions">
                            <button type="button" class="btn btn-sm btn-link text-success p-0" onclick="duplicateItem(this)" title="Duplicate">
                                <i class="ri-file-copy-line"></i>
                            </button>
                            <button type="button" class="btn btn-sm btn-link text-danger p-0" onclick="removeItem(this)" title="Remove">
                                <i class="ri-delete-bin-line"></i>
                            </button>
                        </div>
                    </div>
                    <div class="irow-extras">
                        <div class="irow-extras-toggles">
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#discount-${itemCount}">
                                <i class="ri-price-tag-3-line"></i>Discount
                            </button>
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#fee-${itemCount}">
                                <i class="ri-add-circle-line"></i>Fee / Charge
                            </button>
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#taxesCollapse-${itemCount}">
                                <i class="ri-percent-line"></i>Taxes
                            </button>
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#lineinfo-${itemCount}">
                                <i class="ri-information-line"></i>Additional Information
                            </button>
                        </div>
                        <div class="collapse irow-extras-panel" id="discount-${itemCount}">
                            <div class="row g-2 align-items-end">
                                <div class="col-6 col-md-2">
                                    <label class="irow-mlabel">Rate</label>
                                    <div class="input-group input-group-sm">
                                        <input type="number" class="form-control discount-rate" step="0.01" placeholder="0" data-discount-target="#discount-amount-${itemCount}" data-item-index="${itemCount}" />
                                        <span class="input-group-text">%</span>
                                    </div>
                                </div>
                                <div class="col-6 col-md-3">
                                    <label class="irow-mlabel">Amount</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].DiscountAmount" id="discount-amount-${itemCount}" type="number" class="form-control form-control-sm discount-amount-input" step="0.01" placeholder="0.00" />
                                </div>
                                <div class="col-12 col-md-7">
                                    <label class="irow-mlabel">Description / Reason</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].DiscountReason" class="form-control form-control-sm" maxlength="200" placeholder="Reason for this discount" />
                                </div>
                            </div>
                        </div>
                        <div class="collapse irow-extras-panel" id="fee-${itemCount}">
                            <div class="row g-2 align-items-end">
                                <div class="col-6 col-md-2">
                                    <label class="irow-mlabel">Rate</label>
                                    <div class="input-group input-group-sm">
                                        <input type="number" class="form-control fee-rate" step="0.01" placeholder="0" data-fee-target="#fee-amount-${itemCount}" data-item-index="${itemCount}" />
                                        <span class="input-group-text">%</span>
                                    </div>
                                </div>
                                <div class="col-6 col-md-3">
                                    <label class="irow-mlabel">Amount</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].FeeChargeAmount" id="fee-amount-${itemCount}" type="number" class="form-control form-control-sm fee-amount-input" step="0.01" placeholder="0.00" />
                                </div>
                                <div class="col-12 col-md-7">
                                    <label class="irow-mlabel">Description / Reason</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].FeeChargeReason" class="form-control form-control-sm" maxlength="200" placeholder="Reason for this fee/charge" />
                                </div>
                            </div>
                        </div>
                        <div class="collapse irow-extras-panel" id="taxesCollapse-${itemCount}">
                            <div class="tax-section" id="taxes-${itemCount}"></div>
                            <button type="button" class="btn btn-sm btn-outline-primary mt-1" onclick="addTaxRow(${itemCount})">
                                <i class="ri-add-line me-1"></i>Tax
                            </button>
                        </div>
                        <div class="collapse irow-extras-panel" id="lineinfo-${itemCount}">
                            <div class="row g-2">
                                <div class="col-6 col-md-4">
                                    <label class="irow-mlabel">Product Tariff Code</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].ProductTariffCode" class="form-control form-control-sm" maxlength="50" placeholder="Primarily for goods" />
                                </div>
                                <div class="col-6 col-md-4">
                                    <label class="irow-mlabel">Country of Origin</label>
                                    <select name="Invoice.InvoiceLines[${itemCount}].CountryOfOrigin" class="form-select form-select-sm">
                                        <option value="">-- Select --</option>
                                        ${countryOptionsHtml}
                                    </select>
                                </div>
                            </div>
                        </div>
                    </div>
                `;

                // Add the row to the table
                tbody.appendChild(newRow);
                console.log(`✅ Row added to table, new row count: ${tbody.children.length}`);

                // Update the item count
                if (itemCountInput) {
                    itemCountInput.value = itemCount + 1;
                    console.log(`📊 Updated item count to: ${itemCount + 1}`);
                }

                // Initialize Select2 for the new Unit dropdown
                const newUnitSelect = newRow.querySelector('select[name^="Invoice.InvoiceLines"][name$=".UnitOfMeasure"]');
                if (newUnitSelect && typeof $ !== 'undefined' && $.fn.select2) {
                    $(newUnitSelect).select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Unit Measurement',
                        allowClear: true,
                        minimumResultsForSearch: 0 // Always show search box
                    });

                    // Add Select2 change event for validation
                    $(newUnitSelect).on('change', function() {
                        forceValidationStyling();
                    });
                    console.log('✅ Select2 initialized for new unit dropdown');
                }

                // Initialize Select2 for the new Classification dropdown
                const newClassificationSelect = newRow.querySelector('select[name^="Invoice.InvoiceLines"][name$=".ClassificationCode"]');
                if (newClassificationSelect && typeof $ !== 'undefined' && $.fn.select2) {
                    $(newClassificationSelect).select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Classification Code',
                        allowClear: true,
                        minimumResultsForSearch: 0 // Always show search box
                    });

                    // Add Select2 change event for validation
                    $(newClassificationSelect).on('change', function() {
                        forceValidationStyling();
                    });
                    console.log('✅ Select2 initialized for new classification dropdown');
                }

                // Initialize Select2 for the new Country of Origin dropdown (line-level Additional Info)
                const newCountrySelect = newRow.querySelector('select[name^="Invoice.InvoiceLines"][name$=".CountryOfOrigin"]');
                if (newCountrySelect && typeof $ !== 'undefined' && $.fn.select2) {
                    $(newCountrySelect).select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Country',
                        allowClear: true,
                        minimumResultsForSearch: 0
                    });
                }

                // Wire the Discount/Fee "Rate %" convenience inputs (JS-only, not persisted - the
                // bound Amount field is the actual source of truth, same as every other calculated
                // total in this form)
                bindDiscountFeeRateInputs(newRow);

                // Get references to form elements for event listeners
                const quantityInput = newRow.querySelector('.quantity-input');
                const priceInput = newRow.querySelector('.price-input');
                const classificationSelect = newRow.querySelector('select[name*=".ClassificationCode"]');
                const itemDescriptionTextarea = newRow.querySelector('textarea[name*=".ItemDescription"]');
                const unitSelect = newRow.querySelector('select[name*=".UnitOfMeasure"]');

                // Add event listeners for real-time validation and calculations
                if (quantityInput) {
                    quantityInput.addEventListener('input', () => {
                        calculateSubtotal(quantityInput);
                        forceValidationStyling();
                    });
                }
                if (priceInput) {
                    priceInput.addEventListener('input', () => {
                        calculateSubtotal(priceInput);
                        forceValidationStyling();
                    });
                }
                if (classificationSelect) {
                    classificationSelect.addEventListener('change', () => {
                        forceValidationStyling();
                    });
                }
                if (itemDescriptionTextarea) {
                    itemDescriptionTextarea.addEventListener('input', () => {
                        forceValidationStyling();
                    });
                }
                if (unitSelect) {
                    unitSelect.addEventListener('change', () => {
                        forceValidationStyling();
                    });
                }

                // Update calculations and summary
                updateSummary();
                calculateTotals();

                // Update price validation for the new input field
                const docTypeCode = document.getElementById('docTypeCode')?.value;
                if (docTypeCode) {
                    updatePriceInputValidation(docTypeCode);
                }

                // Automatically add a default tax for the new item
                setTimeout(() => {
                    addTaxRow(itemCount);
                    console.log('✅ Default tax added for item #' + (itemCount + 1));

                    // Auto-set classification code for self-billed documents and disable dropdown
                    const docTypeCode = document.getElementById('docTypeCode')?.value;
                    const isSelfBilled = docTypeCode && ['11', '12', '13', '14'].includes(docTypeCode);

                    const newClassificationSelect = document.querySelector(`select[name="Invoice.InvoiceLines[${itemCount}].ClassificationCode"]`);
                    if (newClassificationSelect) {
                        if (isSelfBilled) {
                            const option004 = newClassificationSelect.querySelector('option[value="004"]');
                            if (option004) {
                                newClassificationSelect.value = '004';

                                // Lock the dropdown for self-billed documents but allow form submission
                                newClassificationSelect.style.backgroundColor = '#f8f9fa';
                                newClassificationSelect.style.cursor = 'not-allowed';
                                newClassificationSelect.style.pointerEvents = 'none';
                                newClassificationSelect.title = 'Classification code is locked to "004" for self-billed documents';
                                newClassificationSelect.setAttribute('data-locked', 'true');

                                console.log(`🔒 Auto-set and LOCKED classification code to "004" for new item in self-billed document`);

                                // Trigger change event for Select2 if initialized
                                if (typeof $ !== 'undefined' && $.fn.select2) {
                                    $(newClassificationSelect).trigger('change');
                                }
                            } else {
                                console.warn(`❌ Option "004" not found in new item classification dropdown`);
                            }
                        } else {
                            // For regular documents, ensure dropdown is enabled
                            newClassificationSelect.style.backgroundColor = '';
                            newClassificationSelect.style.cursor = '';
                            newClassificationSelect.style.pointerEvents = '';
                            newClassificationSelect.title = 'Select appropriate classification code for your business';
                            newClassificationSelect.removeAttribute('data-locked');
                            console.log(`🔓 New item classification dropdown enabled for user selection (regular document)`);
                        }
                    }

                    // Apply validation styling to the new row
                    setTimeout(() => {
                        forceValidationStyling();
                    }, 50);
                }, 100);

            } catch (error) {
                console.error('❌ Error adding item row:', error);
                ErrorHandler.show('Failed to add item. Please try again.', 'error');
            }
        }

        // Remove item row
        function removeItem(button) {
            const itemRow = button.closest('.item-row');
            const itemIndex = itemRow.getAttribute('data-item-index');

            console.log(`🗑️ Removing item #${parseInt(itemIndex) + 1}`);

            itemRow.remove();
            reindexItems();
            updateSummary();
            calculateTotals();

            console.log('✅ Item removed successfully');
        }

        // Duplicate item row
        function duplicateItem(button) {
            console.log('🚀 duplicateItem function called');

            try {
                const sourceRow = button.closest('.item-row');
                const tbody = document.getElementById('lineItems');
                const itemCountInput = document.getElementById('itemCount');
                const itemCount = parseInt(itemCountInput?.value) || tbody.children.length;

                // Create new row
                const newRow = document.createElement('div');
                newRow.className = 'item-row';
                newRow.style.borderLeft = '4px solid var(--einv-primary, #006948)';
                newRow.style.borderBottom = '1px solid var(--einv-border, #e9ecef)';
                newRow.setAttribute('data-item-index', itemCount);

                // Get all form data from source row SAFELY using the classes
                const sourceData = {
                    classificationCode: sourceRow.querySelector('.item-classification')?.value || '',
                    itemCode: sourceRow.querySelector('.item-code')?.value || '',
                    itemDescription: sourceRow.querySelector('.item-description')?.value || '',
                    quantity: sourceRow.querySelector('.quantity-input')?.value || '0',
                    unitOfMeasure: sourceRow.querySelector('select[name*=".UnitOfMeasure"]')?.value || '',
                    unitPrice: sourceRow.querySelector('.price-input')?.value || '0',
                    discountAmount: sourceRow.querySelector('.discount-amount-input')?.value || '',
                    discountReason: sourceRow.querySelector('input[name*=".DiscountReason"]')?.value || '',
                    feeChargeAmount: sourceRow.querySelector('.fee-amount-input')?.value || '',
                    feeChargeReason: sourceRow.querySelector('input[name*=".FeeChargeReason"]')?.value || '',
                    productTariffCode: sourceRow.querySelector('input[name*=".ProductTariffCode"]')?.value || '',
                    countryOfOrigin: sourceRow.querySelector('select[name*=".CountryOfOrigin"]')?.value || ''
                };

                // Get tax data from source row
                const sourceTaxes = [];
                sourceRow.querySelectorAll('.tax-row').forEach(taxRow => {
                    const taxCategory = taxRow.querySelector('select[name*=".TaxCategory"]')?.value || '';
                    const taxPercentage = taxRow.querySelector('input[name*=".TaxPercentage"]')?.value || '';
                    const taxExemptionReason = taxRow.querySelector('input[name*=".TaxExemptionReason"]')?.value || '';

                    if (taxCategory || taxPercentage) {
                        sourceTaxes.push({
                            category: taxCategory,
                            percentage: taxPercentage,
                            exemptionReason: taxExemptionReason
                        });
                    }
                });

                // 🚨 CRITICAL FIX: Create BLANK HTML for new row (NO INJECTED TEXT VALUES)
                // Mirrors _CreateInvoice_Step2Items.cshtml / addItemRow's markup
                newRow.innerHTML = `
                    <div class="irow">
                        <div class="irow-num">${itemCount + 1}</div>
                        <div class="irow-desc">
                            <select class="form-select form-select-sm saved-item-select bg-light border-primary mb-2" onchange="autoFillItem(this, ${itemCount})">
                                <option value="">-- Select Saved Item --</option>
                                ${savedItemsOptionsHtml}
                            </select>
                            <input name="Invoice.InvoiceLines[${itemCount}].ItemCode" class="form-control form-control-sm item-code mb-2" placeholder="Item Code" />
                            <textarea name="Invoice.InvoiceLines[${itemCount}].ItemDescription" class="form-control form-control-sm item-description" rows="1" placeholder="Enter comprehensive item description..." required></textarea>
                        </div>
                        <div class="irow-classification">
                            <div class="irow-mlabel">Classification <span class="text-danger">*</span></div>
                            <select name="Invoice.InvoiceLines[${itemCount}].ClassificationCode" class="form-select form-select-sm item-classification" required>
                                <option value="">Select Classification</option>
                                ${classificationOptionsHtml}
                            </select>
                        </div>
                        <div class="irow-unit">
                            <div class="irow-mlabel">Unit <span class="text-danger">*</span></div>
                            <select name="Invoice.InvoiceLines[${itemCount}].UnitOfMeasure" class="form-control form-control-sm" required>
                                <option value="">Unit</option>
                                ${unitOptionsHtml}
                            </select>
                        </div>
                        <div class="irow-qty">
                            <div class="irow-mlabel">Qty <span class="text-danger">*</span></div>
                            <input name="Invoice.InvoiceLines[${itemCount}].Quantity" type="number" class="form-control form-control-sm quantity-input text-center" step="0.01" min="0" required placeholder="0" />
                        </div>
                        <div class="irow-price">
                            <div class="irow-mlabel">Unit Price <span class="text-danger">*</span></div>
                            <input name="Invoice.InvoiceLines[${itemCount}].UnitPrice" type="number" class="form-control form-control-sm price-input text-end" step="0.01" min="0" required placeholder="0.00" />
                        </div>
                        <div class="irow-total">
                            <div class="irow-mlabel">Subtotal</div>
                            <div class="irow-num-val" id="subtotal-${itemCount}">0.00</div>
                            <div class="irow-mlabel">Row Total</div>
                            <div class="irow-num-val irow-total-val" id="rowtotal-${itemCount}">0.00</div>
                        </div>
                        <div class="irow-actions">
                            <button type="button" class="btn btn-sm btn-link text-success p-0" onclick="duplicateItem(this)" title="Duplicate">
                                <i class="ri-file-copy-line"></i>
                            </button>
                            <button type="button" class="btn btn-sm btn-link text-danger p-0" onclick="removeItem(this)" title="Remove">
                                <i class="ri-delete-bin-line"></i>
                            </button>
                        </div>
                    </div>
                    <div class="irow-extras">
                        <div class="irow-extras-toggles">
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#discount-${itemCount}">
                                <i class="ri-price-tag-3-line"></i>Discount
                            </button>
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#fee-${itemCount}">
                                <i class="ri-add-circle-line"></i>Fee / Charge
                            </button>
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#taxesCollapse-${itemCount}">
                                <i class="ri-percent-line"></i>Taxes
                            </button>
                            <button type="button" class="irow-extras-toggle" data-bs-toggle="collapse" data-bs-target="#lineinfo-${itemCount}">
                                <i class="ri-information-line"></i>Additional Information
                            </button>
                        </div>
                        <div class="collapse irow-extras-panel" id="discount-${itemCount}">
                            <div class="row g-2 align-items-end">
                                <div class="col-6 col-md-2">
                                    <label class="irow-mlabel">Rate</label>
                                    <div class="input-group input-group-sm">
                                        <input type="number" class="form-control discount-rate" step="0.01" placeholder="0" data-discount-target="#discount-amount-${itemCount}" data-item-index="${itemCount}" />
                                        <span class="input-group-text">%</span>
                                    </div>
                                </div>
                                <div class="col-6 col-md-3">
                                    <label class="irow-mlabel">Amount</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].DiscountAmount" id="discount-amount-${itemCount}" type="number" class="form-control form-control-sm discount-amount-input" step="0.01" placeholder="0.00" />
                                </div>
                                <div class="col-12 col-md-7">
                                    <label class="irow-mlabel">Description / Reason</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].DiscountReason" class="form-control form-control-sm" maxlength="200" placeholder="Reason for this discount" />
                                </div>
                            </div>
                        </div>
                        <div class="collapse irow-extras-panel" id="fee-${itemCount}">
                            <div class="row g-2 align-items-end">
                                <div class="col-6 col-md-2">
                                    <label class="irow-mlabel">Rate</label>
                                    <div class="input-group input-group-sm">
                                        <input type="number" class="form-control fee-rate" step="0.01" placeholder="0" data-fee-target="#fee-amount-${itemCount}" data-item-index="${itemCount}" />
                                        <span class="input-group-text">%</span>
                                    </div>
                                </div>
                                <div class="col-6 col-md-3">
                                    <label class="irow-mlabel">Amount</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].FeeChargeAmount" id="fee-amount-${itemCount}" type="number" class="form-control form-control-sm fee-amount-input" step="0.01" placeholder="0.00" />
                                </div>
                                <div class="col-12 col-md-7">
                                    <label class="irow-mlabel">Description / Reason</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].FeeChargeReason" class="form-control form-control-sm" maxlength="200" placeholder="Reason for this fee/charge" />
                                </div>
                            </div>
                        </div>
                        <div class="collapse irow-extras-panel" id="taxesCollapse-${itemCount}">
                            <div class="tax-section" id="taxes-${itemCount}"></div>
                            <button type="button" class="btn btn-sm btn-outline-primary mt-1" onclick="addTaxRow(${itemCount})">
                                <i class="ri-add-line me-1"></i>Tax
                            </button>
                        </div>
                        <div class="collapse irow-extras-panel" id="lineinfo-${itemCount}">
                            <div class="row g-2">
                                <div class="col-6 col-md-4">
                                    <label class="irow-mlabel">Product Tariff Code</label>
                                    <input name="Invoice.InvoiceLines[${itemCount}].ProductTariffCode" class="form-control form-control-sm" maxlength="50" placeholder="Primarily for goods" />
                                </div>
                                <div class="col-6 col-md-4">
                                    <label class="irow-mlabel">Country of Origin</label>
                                    <select name="Invoice.InvoiceLines[${itemCount}].CountryOfOrigin" class="form-select form-select-sm">
                                        <option value="">-- Select --</option>
                                        ${countryOptionsHtml}
                                    </select>
                                </div>
                            </div>
                        </div>
                    </div>
                `;

                // Add the row to the table BEFORE accessing its elements
                tbody.appendChild(newRow);

                // 🚨 SAFELY INJECT DUPLICATED VALUES VIA DOM PROPERTIES
                // This ensures the HTML is perfect and the autoFillItem function can find the inputs!
                const newCodeInput = newRow.querySelector('.item-code');
                const newDescInput = newRow.querySelector('.item-description');
                const newQtyInput = newRow.querySelector('.quantity-input');
                const newPriceInput = newRow.querySelector('.price-input');
                const classificationSelect = newRow.querySelector('.item-classification');
                const unitSelect = newRow.querySelector('select[name*=".UnitOfMeasure"]');

                if (newCodeInput) newCodeInput.value = sourceData.itemCode;
                if (newDescInput) newDescInput.value = sourceData.itemDescription;
                if (newQtyInput) newQtyInput.value = sourceData.quantity;
                if (newPriceInput) newPriceInput.value = sourceData.unitPrice;

                const newDiscountAmountInput = newRow.querySelector('.discount-amount-input');
                const newDiscountReasonInput = newRow.querySelector('input[name*=".DiscountReason"]');
                const newFeeAmountInput = newRow.querySelector('.fee-amount-input');
                const newFeeReasonInput = newRow.querySelector('input[name*=".FeeChargeReason"]');
                const newTariffInput = newRow.querySelector('input[name*=".ProductTariffCode"]');
                const newCountrySelect = newRow.querySelector('select[name*=".CountryOfOrigin"]');
                if (newDiscountAmountInput) newDiscountAmountInput.value = sourceData.discountAmount;
                if (newDiscountReasonInput) newDiscountReasonInput.value = sourceData.discountReason;
                if (newFeeAmountInput) newFeeAmountInput.value = sourceData.feeChargeAmount;
                if (newFeeReasonInput) newFeeReasonInput.value = sourceData.feeChargeReason;
                if (newTariffInput) newTariffInput.value = sourceData.productTariffCode;
                if (newCountrySelect && sourceData.countryOfOrigin) newCountrySelect.value = sourceData.countryOfOrigin;
                bindDiscountFeeRateInputs(newRow);
                if (typeof $ !== 'undefined' && $.fn.select2 && newCountrySelect) {
                    $(newCountrySelect).select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Country',
                        allowClear: true,
                        minimumResultsForSearch: 0
                    });
                }

                // Set the selected values for dropdowns
                if (classificationSelect && sourceData.classificationCode) {
                    classificationSelect.value = sourceData.classificationCode;
                }
                if (unitSelect && sourceData.unitOfMeasure) {
                    unitSelect.value = sourceData.unitOfMeasure;
                }

                // Initialize Select2 for new dropdowns
                if (typeof $ !== 'undefined' && $.fn.select2) {
                    $(classificationSelect).select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Classification Code',
                        allowClear: true,
                        minimumResultsForSearch: 0
                    });

                    $(unitSelect).select2({
                        theme: 'bootstrap-5',
                        width: '100%',
                        placeholder: 'Select Unit Measurement',
                        allowClear: true,
                        minimumResultsForSearch: 0
                    });
                }

                // Apply visual locking if document type is self-billed
                const docTypeCode = document.getElementById('docTypeCode')?.value;
                const isSelfBilled = docTypeCode && ['11', '12', '13', '14'].includes(docTypeCode);

                if (isSelfBilled && classificationSelect) {
                    classificationSelect.value = '004';
                    classificationSelect.style.backgroundColor = '#f8f9fa';
                    classificationSelect.style.cursor = 'not-allowed';
                    classificationSelect.style.pointerEvents = 'none';
                    classificationSelect.title = 'Classification code is locked to "004" for self-billed documents';
                    classificationSelect.setAttribute('data-locked', 'true');

                    if (typeof $ !== 'undefined' && $.fn.select2) {
                        $(classificationSelect).val('004').trigger('change');
                        $(classificationSelect).next('.select2-container').css({
                            'pointer-events': 'none',
                            'opacity': '0.6'
                        });
                    }
                }

                // Add calculation and validation listeners
                if (newQtyInput) {
                    newQtyInput.addEventListener('input', () => {
                        calculateSubtotal(newQtyInput);
                        validateField(newQtyInput);
                    });
                }
                if (newPriceInput) {
                    newPriceInput.addEventListener('input', () => {
                        calculateSubtotal(newPriceInput);
                        validateField(newPriceInput);
                    });
                }
                if (classificationSelect) {
                    classificationSelect.addEventListener('change', () => validateField(classificationSelect));
                }
                if (newDescInput) {
                    newDescInput.addEventListener('input', () => validateField(newDescInput));
                }
                if (unitSelect) {
                    unitSelect.addEventListener('change', () => validateField(unitSelect));
                }

                // Duplicate taxes
                if (sourceTaxes.length > 0) {
                    sourceTaxes.forEach((tax, taxIndex) => {
                        const taxSection = newRow.querySelector('.tax-section');
                        const taxRow = document.createElement('div');
                        taxRow.className = 'tax-row mb-2 p-2 border rounded';
                        taxRow.innerHTML = `
                            <div class="d-flex flex-wrap gap-2 align-items-center">
                                <div class="flex-fill tax-category-wrapper" style="min-width: 100px;">
                                    <select name="Invoice.InvoiceLines[${itemCount}].Taxes[${taxIndex}].TaxCategory" class="form-select form-select-sm tax-category">
                                        <option value="">Tax Category</option>
                                        ${taxCategoryOptionsHtml}
                                    </select>
                                </div>
                                <div class="tax-percentage-wrapper" style="flex: 0 0 90px;">
                                    <input name="Invoice.InvoiceLines[${itemCount}].Taxes[${taxIndex}].TaxPercentage" type="number" class="form-control form-control-sm" placeholder="%" step="0.01" value="${tax.percentage}" />
                                </div>
                                <div class="tax-action-wrapper" style="flex: 0 0 auto;">
                                    <button type="button" class="btn btn-sm btn-outline-danger delete-btn" onclick="removeTaxRow(this)">
                                        <i class="ri-delete-bin-line"></i>
                                    </button>
                                </div>
                            </div>
                            <div class="mt-2 tax-exemption-reason" style="display: ${tax.category === 'E' ? 'block' : 'none'};">
                                <input name="Invoice.InvoiceLines[${itemCount}].Taxes[${taxIndex}].TaxExemptionReason" class="form-control form-control-sm" placeholder="Exemption Reason (Required)" value="${tax.exemptionReason}" />
                            </div>
                        `;
                        taxSection.appendChild(taxRow);

                        const taxCategorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
                        if (taxCategorySelect) {
                            taxCategorySelect.value = tax.category;
                            if (typeof $ !== 'undefined' && $.fn.select2) {
                                $(taxCategorySelect).select2({
                                    theme: 'bootstrap-5',
                                    width: '100%',
                                    placeholder: 'Select Tax Category',
                                    allowClear: true,
                                    minimumResultsForSearch: 0
                                });
                            }
                            taxCategorySelect.addEventListener('change', () => {
                                toggleExemptionReason(taxCategorySelect);
                                validateTaxField(taxCategorySelect);
                            });
                        }

                        const taxPercentageInput = taxRow.querySelector('input[name*=".TaxPercentage"]');
                        if (taxPercentageInput) {
                            taxPercentageInput.addEventListener('input', () => validateTaxField(taxPercentageInput));
                        }

                        const exemptionReasonInput = taxRow.querySelector('input[name*=".TaxExemptionReason"]');
                        if (exemptionReasonInput) {
                            exemptionReasonInput.addEventListener('input', () => validateTaxField(exemptionReasonInput));
                        }
                    });
                } else {
                    setTimeout(() => {
                        addTaxRow(itemCount);
                    }, 100);
                }

                // Update item count
                if (itemCountInput) {
                    itemCountInput.value = itemCount + 1;
                }

                // Update calculations and summary
                if (newQtyInput) calculateSubtotal(newQtyInput);
                updateSummary();
                calculateTotals();

                setTimeout(() => {
                    if (newRow && newRow.parentNode) validateRow(newRow);
                    forceValidationStyling();
                }, 150);

            } catch (error) {
                console.error('❌ Error duplicating item:', error);
                ErrorHandler.show('Failed to duplicate item. Please try again.', 'error');
            }
        }

        // Reindex items after removal
        function reindexItems() {
            const rows = document.querySelectorAll('.item-row');
            rows.forEach((row, index) => {
                row.setAttribute('data-item-index', index);

                // Update the visible row number (Phase 2D dense-grid "#" column)
                const numEl = row.querySelector('.irow-num');
                if (numEl) {
                    numEl.textContent = index + 1;
                }

                // Update all input names
                row.querySelectorAll('input, select, textarea').forEach(input => {
                    const name = input.getAttribute('name');
                    if (name) {
                        input.setAttribute('name', name.replace(/Invoice\.InvoiceLines\[\d+\]/, `Invoice.InvoiceLines[${index}]`));
                    }
                });

                // Update subtotal ID
                const subtotalElement = row.querySelector('[id^="subtotal-"]');
                if (subtotalElement) {
                    subtotalElement.id = `subtotal-${index}`;
                }

                // Update row total ID
                const rowTotalElement = row.querySelector('[id^="rowtotal-"]');
                if (rowTotalElement) {
                    rowTotalElement.id = `rowtotal-${index}`;
                }

                // Update taxes section ID
                const taxesSection = row.querySelector('.tax-section');
                if (taxesSection) {
                    taxesSection.id = `taxes-${index}`;
                }

                // Update add tax button onclick
                const addTaxBtn = row.querySelector('button[onclick^="addTaxRow"]');
                if (addTaxBtn) {
                    addTaxBtn.setAttribute('onclick', `addTaxRow(${index})`);
                }

                // Update the progressive Discount/Fee/Taxes/Additional-Info collapse ids and their
                // toggle buttons' data-bs-target, plus the Discount/Fee Amount input ids and the Rate
                // inputs' data-*-target attributes that point at them.
                const discountPanel = row.querySelector('[id^="discount-"]');
                if (discountPanel) discountPanel.id = `discount-${index}`;
                const feePanel = row.querySelector('[id^="fee-"]');
                if (feePanel) feePanel.id = `fee-${index}`;
                const taxesPanel = row.querySelector('[id^="taxesCollapse-"]');
                if (taxesPanel) taxesPanel.id = `taxesCollapse-${index}`;
                const lineInfoPanel = row.querySelector('[id^="lineinfo-"]');
                if (lineInfoPanel) lineInfoPanel.id = `lineinfo-${index}`;

                const discountAmountEl = row.querySelector('[id^="discount-amount-"]');
                if (discountAmountEl) discountAmountEl.id = `discount-amount-${index}`;
                const feeAmountEl = row.querySelector('[id^="fee-amount-"]');
                if (feeAmountEl) feeAmountEl.id = `fee-amount-${index}`;

                row.querySelectorAll('.irow-extras-toggle').forEach(btn => {
                    const target = btn.getAttribute('data-bs-target');
                    if (target) {
                        btn.setAttribute('data-bs-target', target.replace(/-\d+$/, `-${index}`));
                    }
                });
                const discountRate = row.querySelector('.discount-rate');
                if (discountRate) {
                    discountRate.setAttribute('data-discount-target', `#discount-amount-${index}`);
                    discountRate.setAttribute('data-item-index', index);
                }
                const feeRate = row.querySelector('.fee-rate');
                if (feeRate) {
                    feeRate.setAttribute('data-fee-target', `#fee-amount-${index}`);
                    feeRate.setAttribute('data-item-index', index);
                }
            });

            // Update item count and log for debugging
            const newCount = rows.length;
            document.getElementById('itemCount').value = newCount;
            console.log('Updated item count to:', newCount);
        }







        // Enhanced customer loading function with centralized error handling
        function loadCustomersForSupplier(supplierId) {
            const docTypeCode = document.getElementById('docTypeCode')?.value || '';
            console.log(`🔄 Loading customers for supplier ID: ${supplierId}, docType: ${docTypeCode}`);
            updateDebugInfo('lastCall', `Loading customers for supplier ${supplierId}`);

            $.ajax({
                url: '/Invoices/CreateInvoice?handler=LoadCustomers',
                method: 'GET',
                data: { supplierId: supplierId, docTypeCode: docTypeCode },
                timeout: 10000,
                        success: function(response) {

            console.log('✅ Successfully loaded customers:', response);

            if (Array.isArray(response) && response.length > 0) {

                const buyerSelect = document.getElementById('buyerSelect');

                if (buyerSelect) {
                    const previousBuyerValue = buyerSelect.value;
                    buyerSelect.innerHTML = '<option value="">Select Buyer</option>';

                    response.forEach(c => {
                        buyerSelect.innerHTML += `<option value="${c.value}">${c.text}</option>`;
                    });

                    const isSelfBilled = ['11', '12', '13', '14'].includes(docTypeCode);

                    if (isSelfBilled && window.primaryCompanyId) {
                        const primaryBuyerValue = `PI_${window.primaryCompanyId}`;
                        if (Array.from(buyerSelect.options).some(opt => opt.value === primaryBuyerValue)) {
                            buyerSelect.value = primaryBuyerValue;
                            if (typeof $ !== 'undefined' && $.fn.select2) {
                                $(buyerSelect).trigger('change');
                            } else {
                                buyerSelect.dispatchEvent(new Event('change'));
                            }
                        }
                    } else if (previousBuyerValue && Array.from(buyerSelect.options).some(opt => opt.value === previousBuyerValue)) {
                        buyerSelect.value = previousBuyerValue;
                        if (typeof $ !== 'undefined' && $.fn.select2) {
                            $(buyerSelect).trigger('change');
                        } else {
                            buyerSelect.dispatchEvent(new Event('change'));
                        }
                    }
                }

            } else {
                console.warn('⚠️ No customer data returned');
            }
        },
                error: function(xhr, status, error) {
                    console.error('❌ Failed to load customers:', { xhr, status, error });
                    updateDebugInfo('response', `Customer load error: ${status}`);
                    incrementErrorCount();

                    // Use centralized error handler
                    ErrorHandler.show('Failed to load customers. Please try again.', 'warning');
                }
            });
        }


        // Enhanced loadPartyDetails function with centralized error handling
        function loadPartyDetails(partyId, entityType, role) {
            console.log("🔎 loadPartyDetails called with:", { partyId, entityType, role });

            if (!partyId || !entityType) {
                console.warn("⚠️ Invalid parameters:", { partyId, entityType });
                return;
            }

            showLoadingSpinners(role);

            $.ajax({
                url: '/Invoices/CreateInvoice?handler=LoadPartyDetails',
                method: 'GET',
                data: {
                    partyId: partyId,
                    partyType: entityType   // PI or PC only
                },
                success: function(response) {
                    console.log("📦 FULL API RESPONSE:", response);

                    if (!response.success || !response.data) {
                        console.warn("⚠ API returned unsuccessful or empty data");
                        return;
                    }

                    const data = response.data;
                    console.log("📦 DATA OBJECT:", data);

                    // Helper to get property value regardless of casing
                    const getVal = (key) => data[key] || data[key.charAt(0).toUpperCase() + key.slice(1)] || '';

                    if (role === 'supplier') {
                        $('#bankAccountNo').val(getVal('bankAccountNo'));
                        $('#bankName').val(getVal('bankName'));

                        // Visual feedback
                        if(getVal('bankAccountNo')) showAutoPopulatedIndicator('bankAccountNo');
                        if(getVal('bankName')) showAutoPopulatedIndicator('bankName');

                        // Cache for the Step 3 review's Trading Parties card (see updateSummary()).
                        window.currentSupplierTIN = getVal('tin');
                        window.currentSupplierRegNo = getVal('regNo');
                        window.currentSupplierSST = getVal('sst');
                        updateReviewTradingParties();
                    }

                    if (role === 'buyer') {
                        const attention = getVal('attention');
                        const paymentTerms = getVal('paymentTerms');

                        console.log("📌 Attention found:", attention);
                        console.log("📌 PaymentTerms found:", paymentTerms);

                        // Populate Attention
                        $('#attention').val(attention);
                        if (attention) showAutoPopulatedIndicator('attention');

                        // Populate Payment Terms
                        $('#paymentTerms').val(paymentTerms);
                        // Optional: show indicator
                        // if (paymentTerms) showAutoPopulatedIndicator('paymentTerms');

                        // Cache for the Step 3 review's Trading Parties card (see updateSummary()).
                        // Note: the API's "address" field is deliberately not surfaced here — it
                        // concatenates the encrypted Addr2 column, which isn't decrypted inside this
                        // projection and can leak ciphertext (see PR description / follow-up).
                        window.currentBuyerTIN = getVal('tin');
                        window.currentBuyerRegNo = getVal('regNo');
                        updateReviewTradingParties();
                    }
                },
                error: function(xhr, status, error) {
                    console.error("❌ loadPartyDetails AJAX Error:", error);
                },
                complete: function() {
                    hideLoadingSpinners(role);
                }
            });
        }



                (function () {
          const $buyer = $('#customerId, select[name="Invoice.CustomerId"]');
          const $attention = $('#attention');

        function onBuyerChanged() {
            const value = $buyer.val();

            if (!value) {
                $attention.val('');
                hideAutoPopulatedIndicator?.('attention');
                return;
            }

            const parts = value.split('_');

            if (parts.length === 2) {
                loadPartyDetails(parts[1], parts[0], 'buyer');
            } else {
                console.warn("⚠️ Invalid buyer value:", value);
            }
        }


          // Bind with a namespace to avoid double-binding
          $buyer.off('change.buyerAttention').on('change.buyerAttention', onBuyerChanged);

          // If Select2 is used, this is still the correct event to listen to
          if ($buyer.val()) onBuyerChanged(); // Prefill on page load
        })();


        // Exchange rate update function
        function updateExchangeRate() {
            const currency = document.getElementById('currency').value;
            const foreignCurrency = document.getElementById('foreignCurrency');

            // Set ForeignCurrency to same as Currency
            if (currency) {
                foreignCurrency.value = currency;
            }

            if (currency && currency !== 'MYR') {
                // You can implement API call to get real exchange rates here
                document.getElementById('exchangeRate').value = '1.00';
            } else {
                document.getElementById('exchangeRate').value = '1.00';
            }
        }

        // Step 1 KPI tiles + validation checklist — purely derived from existing form fields, no fabricated status.
        function updateStep1Kpis() {
            const buyerSelect = document.getElementById('buyerSelect');
            const currencySelect = document.getElementById('currency');
            const docTypeSelect = document.getElementById('docTypeCode');
            const poDoNo = document.getElementById('poDoNo');

            const buyerText = buyerSelect && buyerSelect.selectedIndex > 0 ? buyerSelect.options[buyerSelect.selectedIndex].text : '';
            const kpiBuyerStatus = document.getElementById('kpiBuyerStatus');
            const kpiBuyerDot = document.getElementById('kpiBuyerDot');
            if (kpiBuyerStatus) kpiBuyerStatus.textContent = buyerText || 'Not Selected';
            if (kpiBuyerDot) kpiBuyerDot.className = 'rounded-circle ' + (buyerText ? 'bg-success' : 'bg-secondary');

            const currencyText = currencySelect && currencySelect.selectedIndex > 0 ? currencySelect.value : '';
            const kpiCurrency = document.getElementById('kpiCurrency');
            if (kpiCurrency) kpiCurrency.textContent = currencyText || '-';

            const docTypeText = docTypeSelect && docTypeSelect.selectedIndex > 0 ? docTypeSelect.options[docTypeSelect.selectedIndex].text : '';
            const kpiDocType = document.getElementById('kpiDocType');
            if (kpiDocType) kpiDocType.textContent = docTypeText || '-';

            // Validation checklist — each line reflects a real, currently-true fact about the form; no external verification implied.
            const chkBuyerIcon = document.getElementById('chkBuyerIcon');
            const chkBuyerText = document.getElementById('chkBuyerText');
            const chkBuyerDetail = document.getElementById('chkBuyerDetail');
            if (chkBuyerText) {
                if (buyerText) {
                    chkBuyerText.textContent = 'Buyer Selected';
                    chkBuyerDetail.textContent = buyerText;
                    chkBuyerIcon.className = 'ri-checkbox-circle-fill text-success';
                } else {
                    chkBuyerText.textContent = 'Buyer Not Selected';
                    chkBuyerDetail.textContent = 'Select a buyer to continue.';
                    chkBuyerIcon.className = 'ri-error-warning-fill text-danger';
                }
            }

            const chkCurrencyText = document.getElementById('chkCurrencyText');
            const chkCurrencyDetail = document.getElementById('chkCurrencyDetail');
            if (chkCurrencyText) {
                chkCurrencyText.textContent = currencyText ? 'Currency Selected' : 'Currency Not Selected';
                chkCurrencyDetail.textContent = currencyText || '-';
            }

            const chkPoIcon = document.getElementById('chkPoIcon');
            const chkPoText = document.getElementById('chkPoText');
            if (chkPoText) {
                const hasPo = poDoNo && poDoNo.value.trim().length > 0;
                chkPoText.textContent = hasPo ? 'PO/DO Number Set' : 'PO/DO Number Not Set';
                chkPoIcon.className = hasPo ? 'ri-checkbox-circle-fill text-success' : 'ri-error-warning-fill text-warning';
            }
        }

        // Set default currency to MYR if not already selected
        function setDefaultCurrency() {
            const currencySelect = document.getElementById('currency');
            if (currencySelect && !currencySelect.value) {
                currencySelect.value = 'MYR';
                updateExchangeRate();
            }
        }

        document.addEventListener('DOMContentLoaded', function() {
            // buyerSelect/currency/docTypeCode are Select2-enhanced — a pick commits via jQuery's
            // .trigger('change'), which a plain addEventListener('change', ...) never receives
            // (same issue documented where field-level validation binds to Select2 fields).
            ['buyerSelect', 'currency', 'docTypeCode'].forEach(id => {
                const el = document.getElementById(id);
                if (!el) return;
                if (window.jQuery) {
                    jQuery(el).on('change', updateStep1Kpis);
                } else {
                    el.addEventListener('change', updateStep1Kpis);
                }
            });
            const poDoNo = document.getElementById('poDoNo');
            if (poDoNo) poDoNo.addEventListener('input', updateStep1Kpis);
            updateStep1Kpis();
        });

        // Force validation styling for required fields.
        // Called two ways: (a) as a DOM event handler on the single field the user just interacted
        // with (marks it touched, then validates just that field), or (b) with no argument after a
        // structural change elsewhere in the table (row added/removed/cloned) to refresh styling for
        // fields already touched. Untouched fields are deliberately skipped in mode (b) so a freshly
        // added row — or any field the user hasn't reached yet — never appears invalid on its own;
        // previously this ran unconditionally across every required field on page load and after
        // every row mutation, which is what caused untouched fields to render red prematurely.
        function forceValidationStyling(eventOrField) {
            const singleField = eventOrField && eventOrField.target
                ? eventOrField.target
                : (eventOrField instanceof Element ? eventOrField : null);

            if (singleField) {
                singleField.dataset.touched = 'true';
            }

            const requiredFields = singleField
                ? [singleField]
                : Array.from(document.querySelectorAll('#lineItemsTable input[required], #lineItemsTable select[required], #lineItemsTable textarea[required]'))
                    .filter(field => field.dataset.touched === 'true');

            requiredFields.forEach(field => {
                // Skip validation for tax percentage fields that should be exempt
                if (field.name && field.name.includes('.TaxPercentage')) {
                    const taxRow = field.closest('.tax-row');
                    if (taxRow) {
                        const taxCategorySelect = taxRow.querySelector('select[name*=".TaxCategory"]');
                        if (taxCategorySelect) {
                            const categoryText = taxCategorySelect.options[taxCategorySelect.selectedIndex]?.text || '';
                            const isNotApplicable = categoryText.toLowerCase().includes('not applicable');
                            const isExemption = categoryText.toLowerCase().includes('exemption');
                            
                            // Skip validation for exempt categories
                            if (isNotApplicable || isExemption) {
                                console.log('⏭️ Skipping validation for exempt tax percentage field:', categoryText);
                                return; // Skip this field
                            }
                        }
                    }
                }
                
                // More comprehensive empty check
                let isEmpty = false;

                if (field.type === 'number') {
                    // For number fields, check if value is empty, null, undefined, or "0" for quantity/price
                    isEmpty = !field.value || field.value === '' || field.value === null || field.value === undefined;
                } else if (field.tagName === 'SELECT') {
                    // For select fields, check if no option is selected or empty value
                    isEmpty = !field.value || field.value === '' || field.value === null;
                } else {
                    // For text fields and textareas
                    isEmpty = !field.value || field.value.trim() === '' || field.value === null || field.value === undefined;
                }

                if (isEmpty) {
                    // Red border for empty required fields
                    field.style.borderColor = 'var(--einv-error) !important';
                    field.style.boxShadow = '0 0 0 0.2rem rgba(239, 68, 68, 0.25) !important';
                    field.classList.remove('is-valid');
                    field.classList.add('is-invalid');
                } else {
                    // Normal border for filled required fields (remove all validation styling)
                    field.style.borderColor = 'var(--einv-border)';
                    field.style.boxShadow = 'none';
                    field.style.backgroundImage = 'none';
                    field.classList.remove('is-invalid');
                    field.classList.remove('is-valid');
                    // Remove any Bootstrap validation attributes
                    field.removeAttribute('data-bs-valid');
                    field.removeAttribute('data-bs-invalid');
                }
                setFieldAriaValidation(field, !isEmpty);
            });
        }

        // No-arg call below now validates nothing (no field has been touched yet on page load) —
        // kept only so the MutationObserver's later no-arg refresh calls share the same code path.
        document.addEventListener('DOMContentLoaded', function() {
            setTimeout(() => {
                forceValidationStyling();

                // Add event listeners to required fields for real-time validation
                const requiredFields = document.querySelectorAll('#lineItemsTable input[required], #lineItemsTable select[required], #lineItemsTable textarea[required]');
                requiredFields.forEach(field => {
                    field.addEventListener('input', forceValidationStyling);
                    field.addEventListener('blur', forceValidationStyling);
                    field.addEventListener('keyup', forceValidationStyling);
                    // Select2-enhanced fields (classification/unit) commit via jQuery's
                    // .trigger('change'), which a plain addEventListener('change', ...) never
                    // receives — bind through jQuery too so a Select2 pick is actually caught.
                    if (window.jQuery) {
                        jQuery(field).on('change', forceValidationStyling);
                    } else {
                        field.addEventListener('change', forceValidationStyling);
                    }
                });

                // Also observe DOM changes to handle dynamically added fields
                if (typeof MutationObserver !== 'undefined') {
                    const observer = new MutationObserver(function(mutations) {
                        let shouldValidate = false;
                        mutations.forEach(function(mutation) {
                            if (mutation.type === 'childList' && mutation.addedNodes.length > 0) {
                                shouldValidate = true;
                            }
                        });
                        if (shouldValidate) {
                            setTimeout(forceValidationStyling, 100);
                        }
                    });

                    const table = document.getElementById('lineItemsTable');
                    if (table) {
                        observer.observe(table, { childList: true, subtree: true });
                    }
                }
            }, 500);
        });

        // OLD VALIDATION CODE REMOVED - Replaced with comprehensive tax validation system
        // The new system automatically adds taxes to new items and validates before form submission
        // See: validateTaxRequirements(), addTaxToAllItems(), and form validation in InvoiceManager

        // Load available invoices for RefUUID selection (Credit Notes)
        async function loadAvailableInvoicesForReference() {
            console.log('🔍 Loading available invoices for RefUUID selection...');
            
            const refUUIDSelect = document.getElementById('refUUIDSelect');
            if (!refUUIDSelect) {
                console.error('❌ RefUUID select element not found');
                return;
            }

            try {
                // Get current supplier to filter invoices
                const supplierSelect = document.getElementById('supplierSelect');
                const supplierId = supplierSelect?.value;
                
                if (!supplierId) {
                    console.log('⚠️ No supplier selected - cannot load reference invoices yet');
                    refUUIDSelect.innerHTML = '<option value="">Select supplier first</option>';
                    return;
                }
                
                console.log(`🔍 Loading invoices for supplier ID: ${supplierId}`);

                // Call backend to get invoices for this supplier
                const response = await fetch(`/Invoices/CreateInvoice?handler=GetInvoicesForReference&supplierId=${supplierId}`, {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const invoices = await response.json();
                console.log(`✅ Loaded ${invoices.length} invoices for reference`);
                console.log('📋 Invoice data received:', invoices);

                // Clear and populate dropdown
                refUUIDSelect.innerHTML = '<option value="">Select original invoice</option>';
                
                if (invoices.length === 0) {
                    console.log('⚠️ No invoices found - showing empty message');
                    refUUIDSelect.innerHTML = '<option value="">No invoices available for reference</option>';
                } else {
                    invoices.forEach((invoice, index) => {
                        console.log(`📄 Processing invoice ${index + 1}: ${invoice.invoiceNo} (${invoice.uuid})`);
                        const option = document.createElement('option');
                        option.value = invoice.uuid;
                        
                        // Store rich data for dropdown display but use UUID as value
                        const statusBadge = invoice.status === 'Valid' ? '✅' : 
                                          invoice.status === 'Submitted' ? '📤' : 
                                          invoice.status === 'Cancelled' ? '❌' : '📄';
                        
                        // Store rich display data in data attribute for dropdown template
                        option.dataset.displayText = `${invoice.invoiceNo} | ${invoice.customerName} | RM${invoice.totalAmount} | ${statusBadge}${invoice.status} | ${invoice.issueDate}`;
                        option.dataset.invoiceData = JSON.stringify(invoice);
                        
                        // Option text is just the UUID for both dropdown and selection display
                        option.textContent = invoice.uuid;
                        refUUIDSelect.appendChild(option);
                    });
                }

                // Check if there's a pre-set RefUUID value (from Credit Note pre-population)
                const presetRefUUID = refUUIDSelect.value || 
                                     document.querySelector('input[name="Invoice.RefUUID"]')?.value ||
                                     refUUIDSelect.querySelector('option[selected]')?.value;
                console.log(`🔍 Current RefUUID value: ${presetRefUUID || 'None'}`);
                console.log(`🔍 RefUUID select current value: ${refUUIDSelect.value}`);
                console.log(`🔍 Selected option found: ${refUUIDSelect.querySelector('option[selected]')?.value || 'None'}`);
                
                // Auto-select the option if it exists in the dropdown
                if (presetRefUUID) {
                    // Check if there's already a matching option (from server-side rendering)
                    let matchingOption = refUUIDSelect.querySelector(`option[value="${presetRefUUID}"]`);
                    
                    if (matchingOption) {
                        console.log(`🎯 Found existing RefUUID option: ${presetRefUUID}`);
                        refUUIDSelect.value = presetRefUUID;
                        
                        // Show auto-selection badge immediately
                        const autoLabel = document.getElementById('refUUIDAutoLabel');
                        if (autoLabel) {
                            autoLabel.style.display = 'inline-block';
                        }
                        
                        // Trigger change event to ensure form validation is updated
                        refUUIDSelect.dispatchEvent(new Event('change', { bubbles: true }));
                        console.log(`✅ RefUUID pre-selected from server and form updated`);
                    } else {
                        console.log(`⚠️ RefUUID ${presetRefUUID} not found in current dropdown options - will wait for API load`);
                    }
                }

                // Initialize Select2 if available
                if (typeof $ !== 'undefined' && $.fn.select2) {
                    $(refUUIDSelect).select2({
                        placeholder: 'Type to search or enter external RefUUID...',
                        allowClear: true,
                        width: '100%',
                        theme: 'bootstrap-5',
                        tags: true,
                        matcher: function(params, data) {
                            // Default Select2 matching with case-insensitive support
                            if ($.trim(params.term) === '') {
                                return data;
                            }
                            
                            const searchTerm = params.term.toLowerCase();
                            
                            // Match against UUID (exact or partial)
                            if (data.id && data.id.toLowerCase().indexOf(searchTerm) > -1) {
                                return data;
                            }
                            
                            // Match against rich display text if available
                            if (data.element && data.element.dataset.displayText) {
                                const displayText = data.element.dataset.displayText.toLowerCase();
                                if (displayText.indexOf(searchTerm) > -1) {
                                    return data;
                                }
                            }
                            
                            return null;
                        },
                        insertTag: function (data, tag) {
                            // Allow all custom RefUUID entries that look like UUIDs
                            if (tag.text && tag.text.length > 10) {
                                tag.id = tag.text;
                                data.push(tag);
                            }
                        },
                        dropdownCssClass: 'select2-dropdown-custom',
                        templateResult: function(option) {
                            if (!option.id) {
                                return option.text;
                            }
                            
                            // Handle custom/external RefUUID entries with highlighting for existing ones
                            if (option.element && $(option.element).data('select2-tag')) {
                                // Check if this UUID exists in our dropdown options
                                const existingOption = Array.from(refUUIDSelect.options).find(opt => 
                                    opt.value && opt.value.toLowerCase() === option.text.toLowerCase() && !$(opt).data('select2-tag')
                                );
                                
                                if (existingOption) {
                                    // This UUID exists - show with highlighting and "Already exists" indicator
                                    return $(
                                        '<div style="padding: 6px 12px; background-color: #f0f8f4; border-left: 3px solid #006948;">' +
                                        '<div style="font-weight: 600; color: #006948; margin-bottom: 2px;">' +
                                        '<i class="ri-checkbox-circle-line me-1"></i>' + option.text + '</div>' +
                                        '<div style="font-size: 0.8em; color: #666;">✓ Exists in your invoices</div>' +
                                        '</div>'
                                    );
                                } else {
                                    // New UUID - show with different styling
                                    return $(
                                        '<div style="padding: 6px 12px; background-color: #fff8f0; border-left: 3px solid #f59e0b;">' +
                                        '<div style="font-weight: 500; color: #f59e0b; margin-bottom: 2px;">' +
                                        '<i class="ri-add-circle-line me-1"></i>' + option.text + '</div>' +
                                        '<div style="font-size: 0.8em; color: #666;">New RefUUID entry</div>' +
                                        '</div>'
                                    );
                                }
                            }
                            
                            // Use rich display data if available for dropdown options
                            const displayText = option.element && option.element.dataset.displayText;
                            if (displayText) {
                                const parts = displayText.split(' | ');
                                if (parts.length >= 5) {
                                    const $option = $(
                                        '<div style="padding: 8px 0;">' +
                                        '<div style="font-weight: 600; color: #006948; margin-bottom: 4px;">' + parts[0] + '</div>' +
                                        '<div style="color: #666; font-size: 0.9em; margin-bottom: 2px;">' +
                                        '<i class="ri-building-2-line me-1"></i>' + parts[1] + '</div>' +
                                        '<div style="display: flex; justify-content: space-between; font-size: 0.85em; color: #888;">' +
                                        '<span><i class="ri-money-dollar-circle-line me-1"></i>' + parts[2] + '</span>' +
                                        '<span>' + parts[3] + '</span>' +
                                        '<span><i class="ri-calendar-line me-1"></i>' + parts[4] + '</span>' +
                                        '</div>' +
                                        '<div style="margin-top: 4px; font-size: 0.8em; color: #aaa;">UUID: ' + option.text + '</div>' +
                                        '</div>'
                                    );
                                    return $option;
                                }
                            }
                            
                            // Fallback to just UUID
                            return option.text;
                        },
                        templateSelection: function(option) {
                            if (!option.id) {
                                return option.text;
                            }
                            
                            // Always show just the UUID value for all RefUUID entries
                            return option.text;
                        }
                    });
                    
                    // Set Select2 value if pre-selected and option exists
                    if (presetRefUUID && refUUIDSelect.querySelector(`option[value="${presetRefUUID}"]`)) {
                        // Use setTimeout to ensure Select2 is fully initialized
                        setTimeout(() => {
                            $(refUUIDSelect).val(presetRefUUID).trigger('change');
                            console.log(`✅ Auto-selected RefUUID in Select2: ${presetRefUUID}`);
                            
                            // Mark the field as valid to remove any validation styling
                            $(refUUIDSelect).removeClass('is-invalid').addClass('is-valid');
                        }, 100);
                    }
                }

            } catch (error) {
                console.error('❌ Error loading reference invoices:', error);
                refUUIDSelect.innerHTML = '<option value="">Error loading invoices</option>';
            }
        }

        // Handle RefUUID dropdown selection for manual selection mode
        document.addEventListener('DOMContentLoaded', function() {
            const refUUIDSelect = document.getElementById('refUUIDSelect');
            const refUUIDHidden = document.getElementById('refUUIDHidden');
            
            if (refUUIDSelect && refUUIDHidden) {
                refUUIDSelect.addEventListener('change', function() {
                    const selectedUUID = this.value;
                    refUUIDHidden.value = selectedUUID;
                    
                    console.log(`📝 RefUUID manually selected: ${selectedUUID}`);
                    
                    if (selectedUUID) {
                        // Get selected invoice details for logging
                        const selectedOption = this.options[this.selectedIndex];
                        const invoiceData = selectedOption.dataset.invoiceData;
                        
                        if (invoiceData) {
                            try {
                                const invoice = JSON.parse(invoiceData);
                                console.log(`✅ Selected invoice: ${invoice.invoiceNo} (${invoice.uuid})`);
                            } catch (e) {
                                console.log('⚠️ Could not parse invoice data');
                            }
                        }
                    }
                });
            }
        });

                window.autoFillItem = function(selectElement, index) {
            if (!selectElement.value) return; // Ignore if they selected the placeholder

            try {
                // Parse the JSON data stored in the option value
                const itemData = JSON.parse(selectElement.value);
                const row = selectElement.closest('.item-row');

                // Find the input fields in this specific row
                const classificationSelect = row.querySelector('.item-classification');
                const codeInput = row.querySelector('.item-code');
                const descTextarea = row.querySelector('.item-description');
                const unitSelect = row.querySelector('select[name*=".UnitOfMeasure"]');
                const priceInput = row.querySelector('.price-input');

                // Fill the fields
                if (codeInput) codeInput.value = itemData.ItemCode || '';
                if (descTextarea) descTextarea.value = itemData.Description || '';

                // Unit/Unit Price are the catalogue item's own defaults — only prefill when the
                // catalogue actually has them set; never blank out whatever the user already typed.
                if (unitSelect && itemData.UnitCode) {
                    unitSelect.value = itemData.UnitCode;
                    if (typeof $ !== 'undefined' && $(unitSelect).hasClass("select2-hidden-accessible")) {
                        $(unitSelect).trigger('change');
                    } else {
                        unitSelect.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                }
                if (priceInput && itemData.UnitPrice !== null && itemData.UnitPrice !== undefined) {
                    priceInput.value = itemData.UnitPrice;
                    priceInput.dispatchEvent(new Event('input', { bubbles: true }));
                }

                if (classificationSelect && itemData.ClassificationCode) {
                    // Check if the document type is self-billed (which locks it to 004)
                    const docTypeCode = document.getElementById('docTypeCode')?.value;
                    const isSelfBilled = docTypeCode && ['11', '12', '13', '14'].includes(docTypeCode);

                    if (!isSelfBilled) {
                         classificationSelect.value = itemData.ClassificationCode;
                         // If Select2 is applied, trigger the change event to update the UI
                         if (typeof $ !== 'undefined' && $(classificationSelect).hasClass("select2-hidden-accessible")) {
                             $(classificationSelect).trigger('change');
                         }
                    }
                }

                // Trigger validation so the red borders disappear
                if (codeInput) validateField(codeInput);
                if (descTextarea) validateField(descTextarea);
                if (classificationSelect) validateField(classificationSelect);

                // Reset the dropdown back to default so they know they are editing the values locally now
                selectElement.value = "";

            } catch (e) {
                console.error("Error parsing saved item data:", e);
            }
        };
