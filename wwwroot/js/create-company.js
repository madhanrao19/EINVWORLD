
/*create-company.js*/


function nextTab(tabId) {
    document.querySelector(`#${tabId}`).click();
}

function prevTab(tabId) {
    document.querySelector(`#${tabId}`).click();
}

function nextTab(tabId) {
    const currentTab = document.querySelector('.tab-pane.active');

    // Get all inputs inside current tab
    const inputs = currentTab.querySelectorAll('input, select, textarea');

    let isValid = true;
    inputs.forEach(input => {
        if (!input.checkValidity()) {
            input.classList.add('is-invalid'); // Bootstrap style
            isValid = false;
        } else {
            input.classList.remove('is-invalid');
        }
    });

    if (isValid) {
        document.querySelector(`#${tabId}`).click(); // Go to next tab
    } else {
        // Optionally scroll to first invalid field
        const firstInvalid = currentTab.querySelector('.is-invalid');
        if (firstInvalid) firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}
function validateStep2() {
    const step2 = document.querySelector('#step2');
    const inputs = step2.querySelectorAll('input, select, textarea');

    let isValid = true;
    inputs.forEach(input => {
        if (!input.checkValidity()) {
            input.classList.add('is-invalid');
            isValid = false;
        } else {
            input.classList.remove('is-invalid');
        }
    });

    if (!isValid) {
        const firstInvalid = step2.querySelector('.is-invalid');
        if (firstInvalid) firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    return isValid;
}



document.addEventListener("DOMContentLoaded", function () {
    // Logo preview
    const fileInput = document.getElementById("company-logo-input");
    const previewImg = document.getElementById("companylogo-img");

    if (fileInput && previewImg) {
        fileInput.addEventListener("change", function () {
            const file = fileInput.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = function (e) {
                    previewImg.src = e.target.result;
                };
                reader.readAsDataURL(file);
            }
        });
    }
});

$(function () {
    $('.select2').select2({
        theme: 'bootstrap-5',
        placeholder: "-- Select an option --",
        allowClear: true,
        width: '100%'
    });

    function updateBizDescription() {
        const selected = $('#msicSelect').find(':selected');
        const raw = selected.attr('data-description');

        let description = raw || "";

        if (description.includes(" - ")) {
            description = description.split(" - ")[1].trim();
        }

        $('#PartyInfo_BizDescription').val(description);
        $('#bizDescPreview').text(description || "");

    }

    $('#msicSelect').on('change select2:select', updateBizDescription);
    // On load keep a saved/posted-back description (Edit forms, failed POST); only fill when empty.
    const savedDesc = $('#PartyInfo_BizDescription').val();
    if (savedDesc) $('#bizDescPreview').text(savedDesc); else updateBizDescription();
});

// Edit forms only: saved records can pre-date today's checks (e.g. a phone without "+", no email).
// Highlight those fields and show #saved-data-notice on load, instead of surprising the user at Save.
function flagInvalidSavedFields(form) {
    const fields = [...form.querySelectorAll('input, select, textarea')]
        .filter(el => !el.disabled && el.type !== 'hidden' && el.type !== 'file');
    const mark = el => {
        const bad = !el.checkValidity();
        el.classList.toggle('is-invalid', bad);
        // Select2 hides the real <select>; mark its visible box too.
        $(el).next('.select2-container').find('.select2-selection').toggleClass('is-invalid', bad);
        return bad;
    };
    const invalid = fields.filter(mark);
    invalid.forEach(el => $(el).on('input change', () => mark(el)));
    const notice = document.getElementById('saved-data-notice');
    if (notice && invalid.length) {
        const names = invalid.map(el => (form.querySelector(`label[for="${el.id}"]`)?.textContent || el.name).trim());
        notice.querySelector('.saved-data-fields').textContent = names.join(', ');
        notice.classList.remove('d-none');
    }
}
