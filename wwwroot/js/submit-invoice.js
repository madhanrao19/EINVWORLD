import { handleSubmit } from './invoice-submit-core.js';

let submitButton = null;

document.addEventListener("DOMContentLoaded", function () {
    submitButton = document.getElementById("sa-success-submit-lhdn");
    if (submitButton) {
        submitButton.addEventListener("click", (e) => {
            e.preventDefault();
            handleSubmit(submitButton);
        });
    }
});

window.submitFromListnew = function (invoiceNo) {
    document.getElementById("invoiceNo").value = invoiceNo;
    document.getElementById("draftFilePath").value = "dummy-path.json";
    document.getElementById("sa-success-submit-lhdn").click();
};