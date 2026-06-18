// 📦 File: wwwroot/js/template-invoice.js


document.addEventListener('DOMContentLoaded', () => {
    const saveTemplateBtn = document.getElementById("saveTemplateBtn");
    const templateNameInput = document.getElementById("TemplateName");
    const invoiceForm = document.getElementById("invoice_form");
    const templateNameFeedback = document.getElementById("templateNameFeedback");
    const templateNameMessage = document.getElementById("templateNameMessage");
    const templateNameError = document.getElementById("templateNameError");
    const templateNameErrorMessage = document.getElementById("templateNameErrorMessage");

    // Debug: Check if all elements are found
    console.log('🔍 Template validation elements check:', {
        saveTemplateBtn: !!saveTemplateBtn,
        templateNameInput: !!templateNameInput,
        invoiceForm: !!invoiceForm,
        templateNameFeedback: !!templateNameFeedback,
        templateNameMessage: !!templateNameMessage,
        templateNameError: !!templateNameError,
        templateNameErrorMessage: !!templateNameErrorMessage
    });

    let debounceTimeout;
    let isValidatingName = false;
    let lastValidationResult = false;

    // Real-time template name validation
    if (templateNameInput) {
        templateNameInput.addEventListener('input', () => {
            const templateName = templateNameInput.value.trim();
            
            // Clear previous validation
            clearValidationState();
            lastValidationResult = false; // Reset validation result
            
            if (!templateName) {
                return;
            }

            // Debounce validation to avoid too many API calls
            clearTimeout(debounceTimeout);
            debounceTimeout = setTimeout(() => {
                validateTemplateName(templateName);
            }, 500);
        });
    }

    if (saveTemplateBtn && templateNameInput && invoiceForm) {
        saveTemplateBtn.addEventListener("click", async () => {
            const templateName = templateNameInput.value.trim();

            if (!templateName) {
                showValidationError("Please enter a template name.");
                return;
            }

            // Check if we're currently validating or if last validation failed
            if (isValidatingName) {
                showValidationError("Please wait for validation to complete.");
                return;
            }

            // Use cached validation result if available, otherwise validate again
            let isValid = lastValidationResult;
            if (!isValid) {
                isValid = await validateTemplateName(templateName);
            }
            
            if (!isValid) {
                // Keep button enabled so user can try again with different name
                saveTemplateBtn.disabled = false;
                saveTemplateBtn.innerHTML = 'Save Template';
                return; // Don't proceed if name is invalid
            }

            // Show loading state
            saveTemplateBtn.disabled = true;
            saveTemplateBtn.innerHTML = '<i class="spinner-border spinner-border-sm me-2"></i>Saving...';

            // Close the modal before processing
            const modal = bootstrap.Modal.getInstance(document.getElementById('saveTemplateModal'));
            if (modal) {
                modal.hide();
            }

            // Use AJAX to save template without page reload
            try {
                const formData = new FormData(invoiceForm);
                formData.set('invoiceAction', 'saveAsTemplate');
                formData.set('TemplateName', templateName);
                formData.delete('handler'); // Remove handler to hit OnPostAsync
                
                // Make sure anti-forgery token is included
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                if (token) {
                    formData.set('__RequestVerificationToken', token);
                }

                console.log('📤 Sending AJAX request to save template...');
                console.log('📋 FormData contents:', Array.from(formData.entries()).map(([k, v]) => `${k}: ${typeof v === 'string' ? v.substring(0, 50) : '[File]'}`));
                
                const response = await fetch('/Invoices/CreateInvoice', {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: formData
                });

                console.log('📥 Response status:', response.status, response.statusText);
                console.log('📋 Response headers:', [...response.headers.entries()]);
                
                if (response.ok) {
                    const responseText = await response.text();
                    console.log('📄 Raw response:', responseText.substring(0, 200) + '...');
                    
                    try {
                        const result = JSON.parse(responseText);
                        console.log('✅ Parsed JSON result:', result);
                        
                        if (result.success) {
                            // Show success notification without page reload
                            Swal.fire({
                                icon: 'success',
                                title: 'Template Saved Successfully!',
                                text: 'Your invoice template has been saved and can be reused for future invoices.',
                                confirmButtonColor: '#28a745',
                                timer: 3000,
                                timerProgressBar: true
                            });
                            console.log('✅ Template saved successfully via AJAX');
                        } else {
                            Swal.fire({
                                icon: 'error',
                                title: 'Save Failed',
                                text: result.message || 'Failed to save template. Please try again.',
                                confirmButtonColor: '#dc3545'
                            });
                        }
                    } catch (parseError) {
                        console.error('❌ JSON Parse Error:', parseError);
                        console.log('📄 Response is HTML, not JSON - server returned page instead of JSON');
                        Swal.fire({
                            icon: 'error',
                            title: 'Save Failed',
                            text: 'Server error: Expected JSON response but got HTML page.',
                            confirmButtonColor: '#dc3545'
                        });
                    }
                } else {
                    Swal.fire({
                        icon: 'error',
                        title: 'Save Failed',
                        text: 'Failed to save template. Please try again.',
                        confirmButtonColor: '#dc3545'
                    });
                }
            } catch (error) {
                console.error('❌ Error saving template:', error);
                Swal.fire({
                    icon: 'error',
                    title: 'Save Failed',
                    text: 'An error occurred while saving the template.',
                    confirmButtonColor: '#dc3545'
                });
            }

            // Reset button state
            saveTemplateBtn.disabled = false;
            saveTemplateBtn.innerHTML = 'Save Template';
        });
    }

    async function validateTemplateName(templateName) {
        if (isValidatingName) return false;
        
        isValidatingName = true;
        
        try {
            const response = await fetch(`/Invoices/CreateInvoice?handler=CheckTemplateName&templateName=${encodeURIComponent(templateName)}`);
            const data = await response.json();

            if (response.ok) {
                if (data.exists) {
                    console.log('❌ Template name already exists:', data.message);
                    showValidationError(data.message);
                    lastValidationResult = false;
                    return false;
                } else {
                    console.log('✅ Template name is available:', data.message);
                    showValidationSuccess(data.message);
                    lastValidationResult = true;
                    return true;
                }
            } else {
                showValidationError("Error checking template name. Please try again.");
                lastValidationResult = false;
                return false;
            }
        } catch (error) {
            console.error('Error validating template name:', error);
            showValidationError("Error checking template name. Please try again.");
            lastValidationResult = false;
            return false;
        } finally {
            isValidatingName = false;
        }
    }

    function showValidationError(message) {
        console.log('🚨 Showing validation error:', message);
        
        // Remove success styling
        if (templateNameInput) {
            templateNameInput.classList.add('is-invalid');
            templateNameInput.classList.remove('is-valid');
        }
        
        // Hide success message
        if (templateNameFeedback) {
            templateNameFeedback.style.display = 'none';
        }
        
        // Show error message (same structure as success message)
        if (templateNameError && templateNameErrorMessage) {
            templateNameErrorMessage.textContent = message;
            templateNameError.style.display = 'block';
            console.log('✅ Error message displayed:', message);
        }
        
        // Keep save button enabled so user can try again
        if (saveTemplateBtn) {
            saveTemplateBtn.disabled = false;
        }
    }

    function showValidationSuccess(message) {
        console.log('✅ Showing validation success:', message);
        templateNameInput.classList.add('is-valid');
        templateNameInput.classList.remove('is-invalid');
        
        // Update the message span within the small element
        const messageSpan = templateNameMessage.querySelector('span');
        if (messageSpan) {
            messageSpan.textContent = message;
        } else {
            templateNameMessage.textContent = message;
        }
        
        templateNameMessage.className = 'text-success fw-bold';
        templateNameFeedback.style.display = 'block';
        templateNameError.style.display = 'none';
        
        // Enable save button
        if (saveTemplateBtn) {
            saveTemplateBtn.disabled = false;
        }
    }

    function clearValidationState() {
        if (templateNameInput) {
            templateNameInput.classList.remove('is-valid', 'is-invalid');
        }
        if (templateNameFeedback) {
            templateNameFeedback.style.display = 'none';
        }
        if (templateNameError) {
            templateNameError.style.display = 'none';
        }
        
        // Reset save button
        if (saveTemplateBtn) {
            saveTemplateBtn.disabled = false;
            saveTemplateBtn.innerHTML = 'Save Template';
        }
        
        // Reset validation result
        lastValidationResult = false;
    }

    const openModalBtn = document.querySelector('[data-bs-target="#saveTemplateModal"]');
    if (openModalBtn && templateNameInput) {
        openModalBtn.addEventListener("click", () => {
            const today = new Date().toISOString().split("T")[0];
            templateNameInput.value = `Template_${today}`;
            
            // Reset validation state when modal opens
            clearValidationState();
            
            // Focus on the input field
            setTimeout(() => {
                templateNameInput.focus();
                templateNameInput.select(); // Select the default text for easy replacement
            }, 300);
        });
    }

    // Reset modal state when it's closed
    const modal = document.getElementById('saveTemplateModal');
    if (modal) {
        modal.addEventListener('hidden.bs.modal', () => {
            clearValidationState();
            // Reset button text in case it was changed
            if (saveTemplateBtn) {
                saveTemplateBtn.innerHTML = 'Save Template';
                saveTemplateBtn.disabled = false;
            }
        });
    }

    document.getElementById("updateTemplateBtn")?.addEventListener("click", () => {
        document.getElementById("invoiceAction").value = "updateTemplate";
    });

    // Notification functions
    function showSuccessNotification(title, message) {
        console.log('🔔 Showing success notification:', title, message);
        // Try using SweetAlert2 if available
        if (typeof Swal !== 'undefined') {
            console.log('✅ Using SweetAlert2 for notification');
            Swal.fire({
                title: title,
                text: message,
                icon: 'success',
                confirmButtonClass: 'btn btn-primary w-xs mt-2',
                buttonsStyling: false,
                timer: 3000,
                timerProgressBar: true,
                showConfirmButton: false
            });
        } else {
            console.log('⚠️ SweetAlert2 not available, using browser alert');
            // Fallback to browser alert
            alert(title + ': ' + message);
        }
    }

    function showErrorNotification(title, message) {
        // Try using SweetAlert2 if available
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: title,
                text: message,
                icon: 'error',
                confirmButtonClass: 'btn btn-primary w-xs mt-2',
                buttonsStyling: false
            });
        } else {
            // Fallback to browser alert
            alert(title + ': ' + message);
        }
    }

});
