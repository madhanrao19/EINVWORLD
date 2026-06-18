//// 📦 Modular Edit Invoice JS
//document.addEventListener("DOMContentLoaded", () => {
//    const urlParams = new URLSearchParams(window.location.search);
//    const editSuccess = urlParams.get("editSuccess");
//    const issueDateInput = document.getElementById("issueDate");
//    const submitBtn = document.getElementById("sa-success-submit-lhdn");
//    const draftInput = document.getElementById("draftFilePath");

//    if (issueDateInput && submitBtn) {
//        const issueDateStr = issueDateInput.value;
//        if (!issueDateStr) return;

//        const invoiceDate = new Date(issueDateStr);
//        const now = new Date();
//        const threeDaysAgo = new Date();
//        threeDaysAgo.setDate(now.getDate() - 3);

//        if (invoiceDate < threeDaysAgo) {
//            submitBtn.disabled = true;

//            Swal.fire({
//                icon: 'warning',
//                title: 'Cannot Submit Invoice',
//                html: '🚫 This invoice was issued more than <strong>3 days ago</strong>.<br>LHDN does not allow late submissions.',
//                confirmButtonText: 'OK'
//            });
//        } else {
//            // ✅ Date is within 3 days
//            submitBtn.disabled = !draftInput?.value;
//        }
//    }


//    if (submitBtn) {
//        // Disable initially unless draft exists
//        submitBtn.disabled = !draftInput?.value;
//    }

//    if (editSuccess?.toLowerCase() === "true") {
//        Swal.fire({
//            icon: 'success',
//            title: 'Changes Saved',
//            text: 'The invoice was updated successfully.',
//            timer: 2000,
//            showConfirmButton: false
//        }).then(() => {
//            // Enable button after alert
//            if (submitBtn) submitBtn.disabled = false;

//            // Clean up the URL
//            window.history.replaceState({}, document.title, window.location.pathname);
//        });
//    }

//    // Save Edit button setup
//    const saveEditBtn = document.getElementById("saveEdit");
//    const handlerInput = document.getElementById("handler");
//    const actionInput = document.getElementById("invoiceAction");

//    if (saveEditBtn && actionInput && handlerInput) {
//        saveEditBtn.addEventListener("click", () => {
//            actionInput.value = "saveEdit";
//            handlerInput.value = "";
//        });
//    }

//    // Submit to LHDN
//    const submitBtnClick = document.getElementById("sa-success-submit-lhdn");
//    if (submitBtnClick) {
//        submitBtnClick.addEventListener("click", (e) => {
//            const draftPath = draftInput?.value;
//            if (!draftPath) {
//                e.preventDefault();
//                Swal.fire({
//                    icon: 'warning',
//                    title: 'Save draft first!',
//                    text: 'You must save the draft before submitting to LHDN.'
//                });
//            } else {
//                handlerInput.value = "SubmitDocuments";
//                actionInput.value = "";
//            }
//        });
//    }
//});


// 📦 Modular Edit Invoice JS
document.addEventListener("DOMContentLoaded", () => {
    const urlParams = new URLSearchParams(window.location.search);
    const editSuccess = urlParams.get("editSuccess");
    const issueDateInput = document.getElementById("issueDate");
    const submitBtn = document.getElementById("sa-success-submit-lhdn");
    const draftInput = document.getElementById("draftFilePath");
    const saveEditBtn = document.getElementById("saveEdit");
    const handlerInput = document.getElementById("handler");
    const actionInput = document.getElementById("invoiceAction");

    function shouldEnableSubmitButton() {
        const issueDateStr = issueDateInput?.value;
        const draftExists = !!draftInput?.value;

        if (!issueDateStr || !draftExists) return false;

        const invoiceDate = new Date(issueDateStr);
        const now = new Date();
        const threeDaysAgo = new Date();
        threeDaysAgo.setDate(now.getDate() - 3);

        return invoiceDate >= threeDaysAgo;
    }

    // 🔐 Initial validation of submit button
    if (submitBtn && issueDateInput) {
        if (!shouldEnableSubmitButton()) {
            submitBtn.disabled = true;

            const issueDateStr = issueDateInput.value;
            if (issueDateStr) {
                const invoiceDate = new Date(issueDateStr);
                const now = new Date();
                const threeDaysAgo = new Date();
                threeDaysAgo.setDate(now.getDate() - 3);

                if (invoiceDate < threeDaysAgo) {
                    Swal.fire({
                        icon: 'warning',
                        title: 'Cannot Submit Invoice',
                        html: '🚫 This invoice was issued more than <strong>3 days ago</strong>.<br>LHDN does not allow late submissions.',
                        confirmButtonText: 'OK'
                    });
                }
            }
        } else {
            submitBtn.disabled = false;
        }
    }

    // ✅ After save success - revalidate before enabling submit
    if (editSuccess?.toLowerCase() === "true") {
        Swal.fire({
            icon: 'success',
            title: 'Changes Saved',
            text: 'The invoice was updated successfully.',
            timer: 2000,
            showConfirmButton: false
        }).then(() => {
            if (submitBtn && issueDateInput) {
                submitBtn.disabled = !shouldEnableSubmitButton();
            }

            // Clean up the URL
            window.history.replaceState({}, document.title, window.location.pathname);
        });
    }

    // 💾 Save Edit button handler
    if (saveEditBtn && actionInput && handlerInput) {
        saveEditBtn.addEventListener("click", () => {
            actionInput.value = "saveEdit";
            handlerInput.value = "";
        });
    }

    // 🚀 Submit to LHDN button handler
    if (submitBtn) {
        submitBtn.addEventListener("click", (e) => {
            const draftPath = draftInput?.value;
            if (!draftPath) {
                e.preventDefault();
                Swal.fire({
                    icon: 'warning',
                    title: 'Save draft first!',
                    text: 'You must save the draft before submitting to LHDN.'
                });
            } else {
                handlerInput.value = "SubmitDocuments";
                actionInput.value = "";
            }
        });
    }
});
